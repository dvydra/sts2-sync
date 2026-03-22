using SteamKit2;

namespace Sts2Sync.Core.Services;

public enum ConnectionState
{
    Idle,
    Connecting,
    Connected,
    Draining
}

public interface ISteamConnectionManager : IAsyncDisposable
{
    ConnectionState State { get; }
    bool IsConnected { get; }
    SteamClient Client { get; }
    SteamUser User { get; }
    SteamUnifiedMessages UnifiedMessages { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task LogOnWithRefreshTokenAsync(string username, string refreshToken, CancellationToken ct = default);
    Task DisconnectAsync();
    void SuspendIdle();
    void ResumeIdle();
}
