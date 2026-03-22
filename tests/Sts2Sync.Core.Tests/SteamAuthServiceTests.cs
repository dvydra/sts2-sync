using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SteamAuthServiceTests
{
    [Fact]
    public void InitialState_NotLoggedIn()
    {
        using var connection = new SteamConnectionManager();
        var auth = new SteamAuthService(connection);
        Assert.False(auth.IsLoggedIn);
    }

    [Fact]
    public async Task LoginAsync_WhenCancelled_Throws()
    {
        await using var connection = new SteamConnectionManager();
        var auth = new SteamAuthService(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => auth.LoginAsync("user", "pass",
                _ => Task.FromResult("code"), cts.Token));
    }

    [Fact]
    public async Task LoginWithRefreshTokenAsync_WhenCancelled_Throws()
    {
        await using var connection = new SteamConnectionManager();
        var auth = new SteamAuthService(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var creds = new SteamCredentials("user", "token", null);
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => auth.LoginWithRefreshTokenAsync(creds, cts.Token));
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_IsNoOp()
    {
        await using var connection = new SteamConnectionManager();
        var auth = new SteamAuthService(connection);

        // Should not throw
        await auth.DisconnectAsync();
        Assert.False(auth.IsLoggedIn);
    }

    [Fact]
    public void AuthCodeRequest_DeviceCode_HasCorrectType()
    {
        var request = new AuthCodeRequest(AuthCodeType.DeviceCode, PreviousCodeWasIncorrect: true);
        Assert.Equal(AuthCodeType.DeviceCode, request.Type);
        Assert.True(request.PreviousCodeWasIncorrect);
        Assert.Null(request.EmailHint);
    }

    [Fact]
    public void AuthCodeRequest_EmailCode_HasEmailHint()
    {
        var request = new AuthCodeRequest(AuthCodeType.EmailCode, EmailHint: "t***@example.com");
        Assert.Equal(AuthCodeType.EmailCode, request.Type);
        Assert.Equal("t***@example.com", request.EmailHint);
    }

    [Fact]
    public void SteamLogonException_ContainsResult()
    {
        var ex = new SteamLogonException(SteamKit2.EResult.InvalidPassword);
        Assert.Equal(SteamKit2.EResult.InvalidPassword, ex.Result);
        Assert.Contains("InvalidPassword", ex.Message);
    }

    [Fact]
    public void AuthResult_RecordEquality()
    {
        var a = new AuthResult("user", "token", "guard");
        var b = new AuthResult("user", "token", "guard");
        Assert.Equal(a, b);
    }

    [Fact]
    public void SteamCredentials_RecordEquality()
    {
        var a = new SteamCredentials("user", "token", null);
        var b = new SteamCredentials("user", "token", null);
        Assert.Equal(a, b);
    }
}
