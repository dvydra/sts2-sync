using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class ProfileScanner
{
    private readonly ILocalSaveStore _localStore;
    private readonly CloudFileCache _cloudCache;

    public ProfileScanner(ILocalSaveStore localStore, CloudFileCache cloudCache)
    {
        _localStore = localStore;
        _cloudCache = cloudCache;
    }

    public async Task<List<ProfileSyncStatus>> ScanAsync(CancellationToken ct = default)
    {
        var localProfiles = await _localStore.DetectProfilesAsync(ct);
        var results = new List<ProfileSyncStatus>();

        foreach (var profile in Constants.ProfileNames)
        {
            var existsLocally = localProfiles.Contains(profile);
            var cloudFiles = _cloudCache.GetFilesInDirectory($"{profile}/saves");
            var existsInCloud = cloudFiles.Count > 0;

            var localFiles = existsLocally
                ? await _localStore.GetFilesAsync(profile, ct)
                : [];

            var fileStatuses = new List<FileSyncStatus>();

            foreach (var saveFile in Constants.SaveFileNames)
            {
                var relativePath = $"{profile}/{saveFile}";
                var local = localFiles.FirstOrDefault(f => f.RelativePath == relativePath);
                var cloud = cloudFiles.FirstOrDefault(f => f.Filename == relativePath);

                var contentMatch = local is not null && cloud is not null
                    && string.Equals(local.Sha1, cloud.FileSha,
                        StringComparison.OrdinalIgnoreCase);

                fileStatuses.Add(new FileSyncStatus(saveFile, local, cloud, contentMatch));
            }

            results.Add(new ProfileSyncStatus(profile, existsLocally, existsInCloud, fileStatuses));
        }

        return results;
    }
}
