using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SettingsStoreTests
{
    private readonly InMemorySettingsStore _store = new();

    [Fact]
    public async Task LoadSavePath_NoSettings_ReturnsNull()
    {
        var result = await _store.LoadSavePathAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var settings = new SavePathSettings(SavePathPreset.GameHubDriveMapped, "/sdcard/STS2Saves");

        await _store.SaveSavePathAsync(settings);
        var loaded = await _store.LoadSavePathAsync();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task Save_OverwritesPrevious()
    {
        await _store.SaveSavePathAsync(new SavePathSettings(SavePathPreset.GameHubDriveMapped, "/old"));
        var newSettings = new SavePathSettings(SavePathPreset.Custom, "/custom/path");

        await _store.SaveSavePathAsync(newSettings);
        var loaded = await _store.LoadSavePathAsync();

        Assert.Equal(newSettings, loaded);
    }
}
