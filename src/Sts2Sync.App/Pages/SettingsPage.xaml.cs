using Sts2Sync.Core;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ISettingsStore _settingsStore;

    public SettingsPage(ISettingsStore settingsStore)
    {
        InitializeComponent();
        _settingsStore = settingsStore;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var settings = await _settingsStore.LoadSavePathAsync();
        if (settings is not null)
        {
            PresetPicker.SelectedIndex = (int)settings.Preset;
            UpdateVisibility(settings.Preset);

            if (settings.Preset == SavePathPreset.Custom)
                CustomPathEntry.Text = settings.BasePath;
        }
        else
        {
            PresetPicker.SelectedIndex = 0; // Default to drive-mapped
        }

        UpdateResolvedPath();
    }

    private void OnPresetChanged(object? sender, EventArgs e)
    {
        if (PresetPicker.SelectedIndex < 0) return;
        var preset = (SavePathPreset)PresetPicker.SelectedIndex;
        UpdateVisibility(preset);
        UpdateResolvedPath();
    }

    private void UpdateVisibility(SavePathPreset preset)
    {
        RootPathSection.IsVisible = preset == SavePathPreset.GameHubRoot;
        CustomPathEntry.IsVisible = preset == SavePathPreset.Custom;
    }

    private void UpdateResolvedPath()
    {
        var path = ResolvePath();
        ResolvedPathLabel.Text = path is not null ? $"Path: {path}" : "";
    }

    private string? ResolvePath()
    {
        if (PresetPicker.SelectedIndex < 0) return null;

        return (SavePathPreset)PresetPicker.SelectedIndex switch
        {
            SavePathPreset.GameHubDriveMapped => Constants.GameHubDriveMappedPath,
            SavePathPreset.GameHubRoot => Constants.GameHubRootPathTemplate
                .Replace("{containerId}", ContainerIdEntry.Text ?? "")
                .Replace("{steamId}", SteamIdEntry.Text ?? ""),
            SavePathPreset.Custom => CustomPathEntry.Text,
            _ => null
        };
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var path = ResolvePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusLabel.Text = "Please enter a valid path.";
            return;
        }

        var preset = (SavePathPreset)PresetPicker.SelectedIndex;
        await _settingsStore.SaveSavePathAsync(new SavePathSettings(preset, path));
        StatusLabel.Text = "Settings saved. Restart app to apply.";
    }
}
