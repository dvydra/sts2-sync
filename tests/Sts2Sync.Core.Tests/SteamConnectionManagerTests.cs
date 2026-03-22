using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SteamConnectionManagerTests
{
    [Fact]
    public void InitialState_IsIdle()
    {
        using var manager = new SteamConnectionManager();
        Assert.Equal(ConnectionState.Idle, manager.State);
        Assert.False(manager.IsConnected);
    }

    [Fact]
    public void Client_IsNotNull()
    {
        using var manager = new SteamConnectionManager();
        Assert.NotNull(manager.Client);
        Assert.NotNull(manager.User);
        Assert.NotNull(manager.UnifiedMessages);
    }

    [Fact]
    public async Task DisconnectAsync_WhenIdle_IsNoOp()
    {
        await using var manager = new SteamConnectionManager();
        // Should not throw
        await manager.DisconnectAsync();
        Assert.Equal(ConnectionState.Idle, manager.State);
    }

    [Fact]
    public async Task ConnectAsync_WhenCancelled_ThrowsAndReturnsToIdle()
    {
        await using var manager = new SteamConnectionManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => manager.ConnectAsync(cts.Token));
        Assert.Equal(ConnectionState.Idle, manager.State);
    }

    [Fact]
    public void SuspendAndResumeIdle_DoesNotThrow()
    {
        using var manager = new SteamConnectionManager();
        // Should not throw even when not connected
        manager.SuspendIdle();
        manager.SuspendIdle();
        manager.ResumeIdle();
        manager.ResumeIdle();
    }

    [Fact]
    public async Task DisposeAsync_CleansUp()
    {
        var manager = new SteamConnectionManager();
        await manager.DisposeAsync();
        Assert.Equal(ConnectionState.Idle, manager.State);
    }
}
