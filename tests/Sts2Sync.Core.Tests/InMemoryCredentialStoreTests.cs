using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class InMemoryCredentialStoreTests
{
    [Fact]
    public async Task Load_WhenEmpty_ReturnsNull()
    {
        var store = new InMemoryCredentialStore();
        var result = await store.LoadAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var store = new InMemoryCredentialStore();
        var creds = new SteamCredentials("testuser", "refresh_token_123", "guard_data_abc");

        await store.SaveAsync(creds);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("testuser", loaded!.AccountName);
        Assert.Equal("refresh_token_123", loaded.RefreshToken);
        Assert.Equal("guard_data_abc", loaded.GuardData);
    }

    [Fact]
    public async Task Save_OverwritesPrevious()
    {
        var store = new InMemoryCredentialStore();
        await store.SaveAsync(new SteamCredentials("user1", "token1", null));
        await store.SaveAsync(new SteamCredentials("user2", "token2", "guard2"));

        var loaded = await store.LoadAsync();
        Assert.Equal("user2", loaded!.AccountName);
        Assert.Equal("token2", loaded.RefreshToken);
    }

    [Fact]
    public async Task Clear_RemovesCredentials()
    {
        var store = new InMemoryCredentialStore();
        await store.SaveAsync(new SteamCredentials("testuser", "token", null));
        await store.ClearAsync();

        var loaded = await store.LoadAsync();
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveWithNullGuardData_RoundTrips()
    {
        var store = new InMemoryCredentialStore();
        var creds = new SteamCredentials("testuser", "refresh_token", null);

        await store.SaveAsync(creds);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Null(loaded!.GuardData);
    }
}
