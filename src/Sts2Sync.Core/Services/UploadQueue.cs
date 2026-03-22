using System.Threading.Channels;
using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public class UploadQueue : IAsyncDisposable
{
    private readonly ISteamCloudService _cloudService;
    private readonly uint _appId;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private readonly Channel<UploadRequest> _channel;
    private readonly Task _consumerTask;
    private int _pendingCount;

    public int PendingCount => _pendingCount;
    public event EventHandler<UploadResult>? OnUploadComplete;

    public UploadQueue(ISteamCloudService cloudService, uint appId,
        int maxRetries = 3, int retryDelayMs = 1000)
    {
        _cloudService = cloudService;
        _appId = appId;
        _maxRetries = maxRetries;
        _retryDelayMs = retryDelayMs;
        _channel = Channel.CreateUnbounded<UploadRequest>();
        _consumerTask = Task.Run(ProcessQueueAsync);
    }

    public async Task EnqueueAsync(string filename, byte[] data, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _pendingCount);
        await _channel.Writer.WriteAsync(new UploadRequest(filename, data), ct);
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        _channel.Writer.Complete();
        await _consumerTask.WaitAsync(ct);
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            var result = await ProcessUploadAsync(request);
            Interlocked.Decrement(ref _pendingCount);
            OnUploadComplete?.Invoke(this, result);
        }
    }

    private async Task<UploadResult> ProcessUploadAsync(UploadRequest request)
    {
        var delay = _retryDelayMs;

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                await _cloudService.UploadFileAsync(_appId, request.Filename, request.Data);
                return new UploadResult(request.Filename, true, null);
            }
            catch (Exception) when (attempt < _maxRetries)
            {
                await Task.Delay(delay);
                delay *= 2; // Exponential backoff
            }
            catch (Exception ex)
            {
                return new UploadResult(request.Filename, false, ex.Message);
            }
        }

        // Should not reach here, but just in case
        return new UploadResult(request.Filename, false, "Max retries exceeded");
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _consumerTask;
    }

    private record UploadRequest(string Filename, byte[] Data);
}
