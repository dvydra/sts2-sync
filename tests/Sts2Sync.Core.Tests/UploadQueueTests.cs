using System.Threading.Channels;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class UploadQueueTests
{
    private readonly FakeCloudService _cloudService = new();

    [Fact]
    public async Task Enqueue_SingleFile_UploadsSuccessfully()
    {
        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId);

        await queue.EnqueueAsync("profile1/saves/progress.save", "data"u8.ToArray());
        await queue.FlushAsync();

        Assert.Single(_cloudService.Uploads);
        Assert.Equal("profile1/saves/progress.save", _cloudService.Uploads[0].Filename);
    }

    [Fact]
    public async Task Enqueue_MultipleFiles_ProcessedSequentially()
    {
        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId);

        await queue.EnqueueAsync("file1.save", "d1"u8.ToArray());
        await queue.EnqueueAsync("file2.save", "d2"u8.ToArray());
        await queue.EnqueueAsync("file3.save", "d3"u8.ToArray());
        await queue.FlushAsync();

        Assert.Equal(3, _cloudService.Uploads.Count);
        // Verify order preserved
        Assert.Equal("file1.save", _cloudService.Uploads[0].Filename);
        Assert.Equal("file2.save", _cloudService.Uploads[1].Filename);
        Assert.Equal("file3.save", _cloudService.Uploads[2].Filename);
    }

    [Fact]
    public async Task Enqueue_UploadFailure_Retries()
    {
        _cloudService.FailCount = 2; // Fail first 2 attempts, succeed on 3rd

        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId, retryDelayMs: 10);

        await queue.EnqueueAsync("test.save", "data"u8.ToArray());
        await queue.FlushAsync();

        // 2 failures + 1 success = 3 attempts
        Assert.Equal(3, _cloudService.AttemptCount);
        Assert.Single(_cloudService.Uploads); // 1 successful upload
    }

    [Fact]
    public async Task Enqueue_AllRetriesFail_ReportsError()
    {
        _cloudService.FailCount = 10; // Always fail

        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId,
            maxRetries: 3, retryDelayMs: 10);

        var results = new List<UploadResult>();
        queue.OnUploadComplete += (_, r) => results.Add(r);

        await queue.EnqueueAsync("test.save", "data"u8.ToArray());
        await queue.FlushAsync();

        Assert.Equal(3, _cloudService.AttemptCount);
        Assert.Empty(_cloudService.Uploads);
        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.NotNull(results[0].Error);
    }

    [Fact]
    public async Task Flush_WaitsForPendingUploads()
    {
        _cloudService.UploadDelay = TimeSpan.FromMilliseconds(50);

        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId);

        await queue.EnqueueAsync("file1.save", "d1"u8.ToArray());
        await queue.EnqueueAsync("file2.save", "d2"u8.ToArray());

        Assert.True(queue.PendingCount >= 0); // May have started processing

        await queue.FlushAsync();

        Assert.Equal(2, _cloudService.Uploads.Count);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public async Task OnUploadComplete_FiredForSuccess()
    {
        await using var queue = new UploadQueue(_cloudService, Constants.Sts2AppId);

        var results = new List<UploadResult>();
        queue.OnUploadComplete += (_, r) => results.Add(r);

        await queue.EnqueueAsync("test.save", "data"u8.ToArray());
        await queue.FlushAsync();

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("test.save", results[0].Filename);
    }
}

internal class FakeCloudService : ISteamCloudService
{
    public List<(string Filename, byte[] Data)> Uploads { get; } = [];
    public int FailCount { get; set; }
    public int AttemptCount { get; set; }
    public TimeSpan UploadDelay { get; set; }

    public Task<List<CloudFileInfo>> EnumerateFilesAsync(uint appId, CancellationToken ct = default)
        => Task.FromResult(new List<CloudFileInfo>());

    public Task<CloudFileContent> DownloadFileAsync(uint appId, string filename, CancellationToken ct = default)
        => throw new NotImplementedException();

    public async Task UploadFileAsync(uint appId, string filename, byte[] data, CancellationToken ct = default)
    {
        AttemptCount++;

        if (UploadDelay > TimeSpan.Zero)
            await Task.Delay(UploadDelay, ct);

        if (AttemptCount <= FailCount)
            throw new InvalidOperationException("Simulated upload failure");

        Uploads.Add((filename, data));
    }
}
