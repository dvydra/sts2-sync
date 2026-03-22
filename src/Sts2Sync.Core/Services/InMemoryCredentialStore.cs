using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

/// <summary>
/// In-memory credential store for testing and development.
/// </summary>
public class InMemoryCredentialStore : ICredentialStore
{
    private SteamCredentials? _credentials;

    public Task<SteamCredentials?> LoadAsync()
        => Task.FromResult(_credentials);

    public Task SaveAsync(SteamCredentials credentials)
    {
        _credentials = credentials;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _credentials = null;
        return Task.CompletedTask;
    }
}
