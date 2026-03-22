using System.Text.Json;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App.Services;

public class PreferencesSettingsStore : ISettingsStore
{
    private const string SavePathKey = "save_path_settings";

    public Task<SavePathSettings?> LoadSavePathAsync()
    {
        var json = Preferences.Default.Get<string?>(SavePathKey, null);
        if (string.IsNullOrEmpty(json))
            return Task.FromResult<SavePathSettings?>(null);

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<SavePathSettings>(json));
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(SavePathKey);
            return Task.FromResult<SavePathSettings?>(null);
        }
    }

    public Task SaveSavePathAsync(SavePathSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        Preferences.Default.Set(SavePathKey, json);
        return Task.CompletedTask;
    }
}
