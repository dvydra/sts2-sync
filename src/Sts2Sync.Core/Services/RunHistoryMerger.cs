using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class RunHistoryMerger
{
    private readonly ILocalSaveStore _localStore;
    private readonly ISteamCloudService _cloudService;
    private readonly CloudFileCache _cloudCache;
    private readonly UploadQueue _uploadQueue;
    private readonly ISyncLogger _logger;
    private readonly uint _appId;

    public RunHistoryMerger(
        ILocalSaveStore localStore,
        ISteamCloudService cloudService,
        CloudFileCache cloudCache,
        UploadQueue uploadQueue,
        ISyncLogger logger,
        uint appId = Constants.Sts2AppId)
    {
        _localStore = localStore;
        _cloudService = cloudService;
        _cloudCache = cloudCache;
        _uploadQueue = uploadQueue;
        _logger = logger;
        _appId = appId;
    }

    public async Task<RunMergeResult> MergeAsync(string profile, SyncDirection direction, CancellationToken ct = default)
    {
        int downloaded = 0, uploaded = 0, alreadySynced = 0;

        var historyPrefix = $"{profile}/history";

        // Get cloud .run files (filenames from cache)
        var cloudFiles = _cloudCache.GetFilesInDirectory(historyPrefix);
        var cloudFileNames = new HashSet<string>(cloudFiles.Select(f => f.Filename));

        // Get local .run files
        var localFiles = await _localStore.ListFilesAsync(historyPrefix, ".run", ct);
        var localFileNames = new HashSet<string>(localFiles);

        // Cloud-only: download to local
        if (direction != SyncDirection.Upload)
        {
            foreach (var cloudFile in cloudFiles)
            {
                if (localFileNames.Contains(cloudFile.Filename))
                {
                    alreadySynced++;
                    continue;
                }

                var content = await _cloudService.DownloadFileAsync(_appId, cloudFile.Filename, ct);
                await _localStore.WriteFileAsync(cloudFile.Filename, content.Data, ct);
                _logger.Info($"Downloaded run history: {cloudFile.Filename}");
                downloaded++;
            }
        }

        // Local-only: upload to cloud
        if (direction != SyncDirection.Download)
        {
            foreach (var localFile in localFiles)
            {
                if (cloudFileNames.Contains(localFile))
                {
                    if (direction == SyncDirection.Upload)
                        alreadySynced++;
                    continue;
                }

                var data = await _localStore.ReadFileAsync(localFile, ct);
                await _uploadQueue.EnqueueAsync(localFile, data, ct);
                _logger.Info($"Queued run history upload: {localFile}");
                uploaded++;
            }
        }

        return new RunMergeResult(downloaded, uploaded, alreadySynced);
    }
}
