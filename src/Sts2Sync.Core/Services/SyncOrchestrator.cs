using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class SyncOrchestrator
{
    private readonly ISteamAuthService _authService;
    private readonly ISteamCloudService _cloudService;
    private readonly CloudFileCache _cloudCache;
    private readonly ILocalSaveStore _localStore;
    private readonly IBackupManager _backupManager;
    private readonly ICredentialStore _credentialStore;

    public SyncOrchestrator(
        ISteamAuthService authService,
        ISteamCloudService cloudService,
        CloudFileCache cloudCache,
        ILocalSaveStore localStore,
        IBackupManager backupManager,
        ICredentialStore credentialStore)
    {
        _authService = authService;
        _cloudService = cloudService;
        _cloudCache = cloudCache;
        _localStore = localStore;
        _backupManager = backupManager;
        _credentialStore = credentialStore;
    }

    public async Task<SyncReport> SyncAsync(SyncDirection direction, CancellationToken ct = default)
    {
        var actions = new List<FileSyncAction>();
        int downloaded = 0, uploaded = 0, identical = 0, conflicts = 0;

        try
        {
            // 1. Ensure authenticated
            if (!_authService.IsLoggedIn)
            {
                var credentials = await _credentialStore.LoadAsync();
                if (credentials is null)
                    return ErrorReport("Not logged in");

                try
                {
                    await _authService.LoginWithRefreshTokenAsync(credentials, ct);
                }
                catch (Exception ex)
                {
                    return ErrorReport($"Authentication failed: {ex.Message}");
                }
            }

            // 2. Refresh cloud cache
            await _cloudCache.RefreshAsync(ct);

            // 3. Scan profiles
            var scanner = new ProfileScanner(_localStore, _cloudCache);
            var profiles = await scanner.ScanAsync(ct);

            // 4. Process each file
            await using var uploadQueue = new UploadQueue(_cloudService, Constants.Sts2AppId);

            foreach (var profile in profiles)
            {
                foreach (var file in profile.Files)
                {
                    if (file.ContentMatch)
                    {
                        identical++;
                        continue;
                    }

                    if (file.Local is null && file.Cloud is null)
                    {
                        identical++;
                        continue;
                    }

                    var relativePath = $"{profile.ProfileName}/{file.SaveFileName}";

                    // Read bytes from both sides
                    byte[]? localBytes = null;
                    if (file.Local is not null)
                        localBytes = await _localStore.ReadFileAsync(relativePath, ct);

                    byte[]? cloudBytes = null;
                    if (file.Cloud is not null)
                    {
                        var content = await _cloudService.DownloadFileAsync(
                            Constants.Sts2AppId, relativePath, ct);
                        cloudBytes = content.Data;
                    }

                    var result = SaveConflictResolver.Resolve(file.SaveFileName, localBytes, cloudBytes);
                    actions.Add(new FileSyncAction(
                        profile.ProfileName, file.SaveFileName, result.Outcome, result.Reason));

                    switch (result.Outcome)
                    {
                        case ResolutionOutcome.Identical:
                            identical++;
                            break;

                        case ResolutionOutcome.CloudWins:
                            if (direction == SyncDirection.Upload)
                                break; // User only wants to upload
                            if (localBytes is not null)
                                await _backupManager.BackupAsync(relativePath, localBytes, "local", ct);
                            await _localStore.WriteFileAsync(relativePath, cloudBytes!, ct);
                            downloaded++;
                            break;

                        case ResolutionOutcome.LocalWins:
                            if (direction == SyncDirection.Download)
                                break; // User only wants to download
                            if (cloudBytes is not null)
                                await _backupManager.BackupAsync(relativePath, cloudBytes, "cloud", ct);
                            await uploadQueue.EnqueueAsync(relativePath, localBytes!, ct);
                            uploaded++;
                            break;

                        case ResolutionOutcome.Conflict:
                            conflicts++;
                            break;
                    }
                }
            }

            // 5. Flush uploads
            await uploadQueue.FlushAsync(ct);

            // 6. Prune backups
            foreach (var profile in Constants.ProfileNames)
                await _backupManager.PruneAsync(profile, ct);
        }
        catch (Exception ex)
        {
            return ErrorReport(ex.Message, actions, downloaded, uploaded, identical, conflicts);
        }

        return new SyncReport(
            DateTimeOffset.UtcNow, true, null, actions,
            downloaded, uploaded, identical, conflicts);
    }

    private static SyncReport ErrorReport(string error,
        IReadOnlyList<FileSyncAction>? actions = null,
        int downloaded = 0, int uploaded = 0, int identical = 0, int conflicts = 0)
    {
        return new SyncReport(
            DateTimeOffset.UtcNow, false, error,
            actions ?? Array.Empty<FileSyncAction>(),
            downloaded, uploaded, identical, conflicts);
    }
}
