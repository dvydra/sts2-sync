using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class RunHistoryMergerTests
{
    private readonly InMemoryLocalSaveStore _localStore = new();
    private readonly TestableCloudService _cloudService = new();
    private readonly InMemorySyncLogger _logger = new();

    private async Task<RunMergeResult> MergeAsync(string profile, SyncDirection direction)
    {
        var cloudCache = new CloudFileCache(_cloudService, Constants.Sts2AppId);
        await cloudCache.RefreshAsync();

        await using var uploadQueue = new UploadQueue(_cloudService, Constants.Sts2AppId);
        var merger = new RunHistoryMerger(
            _localStore, _cloudService, cloudCache, uploadQueue, _logger);

        var result = await merger.MergeAsync(profile, direction);
        await uploadQueue.FlushAsync();
        return result;
    }

    [Fact]
    public async Task Merge_CloudOnly_DownloadsToLocal()
    {
        var runData = "run history data"u8.ToArray();
        _cloudService.AddCloudFile("profile1/history/1700000000.run", runData);

        var result = await MergeAsync("profile1", SyncDirection.Download);

        Assert.Equal(1, result.Downloaded);
        Assert.Equal(0, result.Uploaded);
        var local = await _localStore.ReadFileAsync("profile1/history/1700000000.run");
        Assert.Equal(runData, local);
    }

    [Fact]
    public async Task Merge_LocalOnly_UploadsToCloud()
    {
        var runData = "local run data"u8.ToArray();
        _localStore.AddFile("profile1/history/1700001000.run", runData);

        var result = await MergeAsync("profile1", SyncDirection.Upload);

        Assert.Equal(0, result.Downloaded);
        Assert.Equal(1, result.Uploaded);
        Assert.Single(_cloudService.Uploads);
    }

    [Fact]
    public async Task Merge_BothSides_Identical_NoAction()
    {
        var runData = "same run"u8.ToArray();
        _cloudService.AddCloudFile("profile1/history/1700000000.run", runData);
        _localStore.AddFile("profile1/history/1700000000.run", runData);

        var result = await MergeAsync("profile1", SyncDirection.Both);

        Assert.Equal(0, result.Downloaded);
        Assert.Equal(0, result.Uploaded);
        Assert.Equal(1, result.AlreadySynced);
    }

    [Fact]
    public async Task Merge_MixedFiles_MergesCorrectly()
    {
        // Cloud-only
        _cloudService.AddCloudFile("profile1/history/1700000000.run", "cloud1"u8.ToArray());
        // Local-only
        _localStore.AddFile("profile1/history/1700001000.run", "local1"u8.ToArray());
        // Both sides
        _cloudService.AddCloudFile("profile1/history/1700002000.run", "both"u8.ToArray());
        _localStore.AddFile("profile1/history/1700002000.run", "both"u8.ToArray());

        var result = await MergeAsync("profile1", SyncDirection.Both);

        Assert.Equal(1, result.Downloaded); // cloud-only downloaded
        Assert.Equal(1, result.Uploaded);   // local-only uploaded
        Assert.Equal(1, result.AlreadySynced); // both-sides skipped
    }

    [Fact]
    public async Task Merge_DirectionDownload_SkipsUploads()
    {
        _localStore.AddFile("profile1/history/1700001000.run", "local"u8.ToArray());

        var result = await MergeAsync("profile1", SyncDirection.Download);

        Assert.Equal(0, result.Uploaded);
        Assert.Empty(_cloudService.Uploads);
    }

    [Fact]
    public async Task Merge_DirectionUpload_SkipsDownloads()
    {
        _cloudService.AddCloudFile("profile1/history/1700000000.run", "cloud"u8.ToArray());

        var result = await MergeAsync("profile1", SyncDirection.Upload);

        Assert.Equal(0, result.Downloaded);
        // Verify local doesn't have the cloud file
        var info = await _localStore.GetFileInfoAsync("profile1/history/1700000000.run");
        Assert.Null(info);
    }

    [Fact]
    public async Task Merge_EmptyHistory_NoAction()
    {
        var result = await MergeAsync("profile1", SyncDirection.Both);

        Assert.Equal(0, result.Downloaded);
        Assert.Equal(0, result.Uploaded);
        Assert.Equal(0, result.AlreadySynced);
    }
}
