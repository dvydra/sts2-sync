using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public interface ICredentialStore
{
    Task<SteamCredentials?> LoadAsync();
    Task SaveAsync(SteamCredentials credentials);
    Task ClearAsync();
}
