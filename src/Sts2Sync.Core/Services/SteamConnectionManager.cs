using SteamKit2;

namespace Sts2Sync.Core.Services;

public class SteamConnectionManager : ISteamConnectionManager, IDisposable
{
    private readonly SteamClient _client;
    private readonly CallbackManager _callbackManager;
    private readonly SteamUser _user;
    private readonly SteamUnifiedMessages _unifiedMessages;
    private readonly object _lock = new();

    private CancellationTokenSource? _callbackCts;
    private Task? _callbackTask;
    private TaskCompletionSource<bool>? _connectTcs;
    private TaskCompletionSource<bool>? _logonTcs;
    private TaskCompletionSource<bool>? _disconnectTcs;

    private Timer? _idleTimer;
    private int _idleSuspendCount;
    private readonly TimeSpan _idleTimeout;
    private volatile ConnectionState _state = ConnectionState.Idle;

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(32)
    ];

    public ConnectionState State => _state;
    public bool IsConnected => _state == ConnectionState.Connected;
    public SteamClient Client => _client;
    public SteamUser User => _user;
    public SteamUnifiedMessages UnifiedMessages => _unifiedMessages;

    public SteamConnectionManager(TimeSpan? idleTimeout = null)
    {
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);

        var config = SteamConfiguration.Create(b =>
            b.WithProtocolTypes(ProtocolTypes.WebSocket));

        _client = new SteamClient(config);
        _callbackManager = new CallbackManager(_client);

        _user = _client.GetHandler<SteamUser>()!;
        _unifiedMessages = _client.GetHandler<SteamUnifiedMessages>()!;

        _callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        _callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        _callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        _callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected)
            return;

        _state = ConnectionState.Connecting;
        StartCallbackPump();

        for (var attempt = 0; attempt <= BackoffDelays.Length; attempt++)
        {
            TaskCompletionSource<bool> tcs;
            lock (_lock)
            {
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _connectTcs = tcs;
            }

            _client.Connect();

            using var registration = ct.Register(() => tcs.TrySetCanceled());

            try
            {
                var connected = await tcs.Task;
                if (connected)
                {
                    _state = ConnectionState.Connected;
                    ResetIdleTimer();
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                _state = ConnectionState.Idle;
                throw;
            }

            // Connection failed — backoff and retry
            if (attempt < BackoffDelays.Length)
            {
                await Task.Delay(BackoffDelays[attempt], ct);
            }
        }

        _state = ConnectionState.Idle;
        throw new InvalidOperationException("Failed to connect to Steam after maximum retries");
    }

    /// <summary>
    /// Log on with a refresh token. Must be connected first.
    /// </summary>
    public async Task LogOnWithRefreshTokenAsync(string username, string refreshToken, CancellationToken ct = default)
    {
        if (_state != ConnectionState.Connected)
            throw new InvalidOperationException($"Cannot log on in state {_state}");

        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _logonTcs = tcs;
        }

        _user.LogOn(new SteamUser.LogOnDetails
        {
            Username = username,
            AccessToken = refreshToken,
            ShouldRememberPassword = true
        });

        using var registration = ct.Register(() => tcs.TrySetCanceled());
        await tcs.Task;

        lock (_lock) { _logonTcs = null; }
    }

    public async Task DisconnectAsync()
    {
        if (_state == ConnectionState.Idle)
            return;

        _state = ConnectionState.Draining;
        _idleTimer?.Dispose();
        _idleTimer = null;

        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _disconnectTcs = tcs;
        }

        _user.LogOff();
        _client.Disconnect();

        // Wait for disconnect callback with timeout
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        // Proceed regardless — we're shutting down

        await StopCallbackPumpAsync();
        _state = ConnectionState.Idle;
    }

    /// <summary>
    /// Suspend idle timer during long operations (e.g., file transfers).
    /// </summary>
    public void SuspendIdle()
    {
        lock (_lock)
        {
            _idleSuspendCount++;
            _idleTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void ResumeIdle()
    {
        lock (_lock)
        {
            _idleSuspendCount = Math.Max(0, _idleSuspendCount - 1);
            if (_idleSuspendCount == 0)
                ResetIdleTimer();
        }
    }

    private void ResetIdleTimer()
    {
        // Caller should hold _lock if thread safety is needed,
        // but this is also called from ConnectAsync on the caller's thread
        _idleTimer?.Dispose();
        _idleTimer = new Timer(_ =>
        {
            if (_state == ConnectionState.Connected)
            {
                _ = DisconnectAsync();
            }
        }, null, _idleTimeout, Timeout.InfiniteTimeSpan);
    }

    private void StartCallbackPump()
    {
        if (_callbackTask is not null) return;

        _callbackCts = new CancellationTokenSource();
        var ct = _callbackCts.Token;
        _callbackTask = Task.Run(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                _callbackManager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            }
        }, ct);
    }

    private async Task StopCallbackPumpAsync()
    {
        _callbackCts?.Cancel();
        if (_callbackTask is not null)
        {
            try { await _callbackTask; }
            catch (OperationCanceledException) { }
        }
        _callbackTask = null;
        _callbackCts?.Dispose();
        _callbackCts = null;
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock) { tcs = _connectTcs; }
        tcs?.TrySetResult(true);
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        TaskCompletionSource<bool>? connectTcs;
        TaskCompletionSource<bool>? logonTcs;
        TaskCompletionSource<bool>? disconnectTcs;
        lock (_lock)
        {
            connectTcs = _connectTcs;
            logonTcs = _logonTcs;
            disconnectTcs = _disconnectTcs;
            _disconnectTcs = null;
        }

        if (_state == ConnectionState.Connecting)
        {
            connectTcs?.TrySetResult(false);
        }
        else if (_state != ConnectionState.Draining)
        {
            _state = ConnectionState.Idle;
        }

        logonTcs?.TrySetException(new InvalidOperationException("Disconnected from Steam"));
        disconnectTcs?.TrySetResult(true);
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        TaskCompletionSource<bool>? tcs;
        lock (_lock) { tcs = _logonTcs; }

        if (callback.Result == EResult.OK)
        {
            tcs?.TrySetResult(true);
        }
        else
        {
            tcs?.TrySetException(new SteamLogonException(callback.Result));
        }
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        // Expected during disconnect — no action needed
    }

    public void Dispose()
    {
        // Non-blocking best-effort cleanup for sync contexts
        _idleTimer?.Dispose();
        _callbackCts?.Cancel();
        _client.Disconnect();
        _callbackCts?.Dispose();
        _state = ConnectionState.Idle;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}

public class SteamLogonException(EResult result) : Exception($"Steam logon failed: {result}")
{
    public EResult Result { get; } = result;
}
