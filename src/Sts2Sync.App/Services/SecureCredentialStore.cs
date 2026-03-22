using System.Text.Json;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App.Services;

/// <summary>
/// Credential store using MAUI SecureStorage (Android Keystore backed).
/// </summary>
public class SecureCredentialStore : ICredentialStore
{
    private const string StorageKey = "steam_credentials";

    public async Task<SteamCredentials?> LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SteamCredentials>(json);
        }
        catch (JsonException)
        {
            // Corrupt stored data — clear it and return null
            SecureStorage.Default.Remove(StorageKey);
            return null;
        }
    }

    public async Task SaveAsync(SteamCredentials credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        await SecureStorage.Default.SetAsync(StorageKey, json);
    }

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
