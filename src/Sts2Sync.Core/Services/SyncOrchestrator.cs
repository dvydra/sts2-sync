using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class SyncOrchestrator
{
    private readonly ISteamAuthService _authService;
    private readonly ISteamConnectionManager _connectionManager;
    private readonly ISteamCloudService _cloudService;
    private readonly CloudFileCache _cloudCache;
    private readonly ILocalSaveStore _localStore;
    private readonly IBackupManager _backupManager;
    private readonly ICredentialStore _credentialStore;
    private readonly ISyncLogger _logger;

    public SyncOrchestrator(
        ISteamAuthService authService,
        ISteamConnectionManager connectionManager,
        ISteamCloudService cloudService,
        CloudFileCache cloudCache,
        ILocalSaveStore localStore,
        IBackupManager backupManager,
        ICredentialStore credentialStore,
        ISyncLogger? logger = null)
    {
        _authService = authService;
        _connectionManager = connectionManager;
        _cloudService = cloudService;
        _cloudCache = cloudCache;
        _localStore = localStore;
        _backupManager = backupManager;
        _credentialStore = credentialStore;
        _logger = logger ?? new ConsoleSyncLogger();
    }

    public async Task<SyncReport> SyncAsync(SyncDirection direction, CancellationToken ct = default)
    {
        var actions = new List<FileSyncAction>();
        int downloaded = 0, uploaded = 0, identical = 0, conflicts = 0;
        int runHistoryDownloaded = 0, runHistoryUploaded = 0;

        try
        {
            // 0. Suspend idle timer during sync
            _connectionManager.SuspendIdle();
            _logger.Info("Idle timer suspended for sync");

            // 1. Ensure authenticated
            if (!_authService.IsLoggedIn)
            {
                var credentials = await _credentialStore.LoadAsync();
                if (credentials is null)
                    return ErrorReport("Not logged in");

                try
                {
                    _logger.Info("Logging in with refresh token...");
                    await _authService.LoginWithRefreshTokenAsync(credentials, ct);
                    _logger.Info("Authenticated successfully");
                }
                catch (Exception ex)
                {
                    _logger.Error("Authentication failed", ex);
                    return ErrorReport($"Authentication failed: {ex.Message}");
                }
            }

            // 2. Refresh cloud cache
            _logger.Info("Refreshing cloud file cache...");
            await _cloudCache.RefreshAsync(ct);
            _logger.Info($"Cloud cache loaded: {_cloudCache.FileCount} files");

            // 3. Scan profiles
            var scanner = new ProfileScanner(_localStore, _cloudCache);
            var profiles = await scanner.ScanAsync(ct);

            // 4. Process each save file
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
                                break;
                            if (localBytes is not null)
                                await _backupManager.BackupAsync(relativePath, localBytes, "local", ct);
                            await _localStore.WriteFileAsync(relativePath, cloudBytes!, ct);
                            _logger.Info($"Downloaded: {relativePath} ({result.Reason})");
                            downloaded++;
                            break;

                        case ResolutionOutcome.LocalWins:
                            if (direction == SyncDirection.Download)
                                break;
                            if (cloudBytes is not null)
                                await _backupManager.BackupAsync(relativePath, cloudBytes, "cloud", ct);
                            await uploadQueue.EnqueueAsync(relativePath, localBytes!, ct);
                            _logger.Info($"Queued upload: {relativePath} ({result.Reason})");
                            uploaded++;
                            break;

                        case ResolutionOutcome.Conflict:
                            _logger.Warn($"Conflict: {relativePath}");
                            conflicts++;
                            break;
                    }
                }
            }

            // 5. Merge run history files
            var merger = new RunHistoryMerger(
                _localStore, _cloudService, _cloudCache, uploadQueue, _logger);

            foreach (var profileName in Constants.ProfileNames)
            {
                var mergeResult = await merger.MergeAsync(profileName, direction, ct);
                runHistoryDownloaded += mergeResult.Downloaded;
                runHistoryUploaded += mergeResult.Uploaded;
            }

            // 6. Flush uploads
            _logger.Info("Flushing upload queue...");
            await uploadQueue.FlushAsync(ct);

            // 7. Prune backups
            foreach (var profile in Constants.ProfileNames)
                await _backupManager.PruneAsync(profile, ct);

            _logger.Info($"Sync complete: {downloaded} downloaded, {uploaded} uploaded, " +
                         $"{identical} identical, {conflicts} conflicts, " +
                         $"{runHistoryDownloaded} run history downloaded, {runHistoryUploaded} run history uploaded");
        }
        catch (Exception ex)
        {
            _logger.Error("Sync failed", ex);
            return ErrorReport(ex.Message, actions, downloaded, uploaded, identical, conflicts,
                runHistoryDownloaded, runHistoryUploaded);
        }
        finally
        {
            _connectionManager.ResumeIdle();
            _logger.Info("Idle timer resumed");
        }

        return new SyncReport(
            DateTimeOffset.UtcNow, true, null, actions,
            downloaded, uploaded, identical, conflicts,
            runHistoryDownloaded, runHistoryUploaded);
    }

    private static SyncReport ErrorReport(string error,
        IReadOnlyList<FileSyncAction>? actions = null,
        int downloaded = 0, int uploaded = 0, int identical = 0, int conflicts = 0,
        int runHistoryDownloaded = 0, int runHistoryUploaded = 0)
    {
        return new SyncReport(
            DateTimeOffset.UtcNow, false, error,
            actions ?? Array.Empty<FileSyncAction>(),
            downloaded, uploaded, identical, conflicts,
            runHistoryDownloaded, runHistoryUploaded);
    }
}
