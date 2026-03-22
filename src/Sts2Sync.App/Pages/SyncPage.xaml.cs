using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App.Pages;

public partial class SyncPage : ContentPage
{
    private readonly SyncOrchestrator _orchestrator;
    private readonly ILocalSaveStore _localStore;
    private readonly CloudFileCache _cloudCache;
    private readonly ISteamAuthService _authService;
    private readonly ICredentialStore _credentialStore;

    public SyncPage(
        SyncOrchestrator orchestrator,
        ILocalSaveStore localStore,
        CloudFileCache cloudCache,
        ISteamAuthService authService,
        ICredentialStore credentialStore)
    {
        InitializeComponent();
        _orchestrator = orchestrator;
        _localStore = localStore;
        _cloudCache = cloudCache;
        _authService = authService;
        _credentialStore = credentialStore;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsLoggedIn)
        {
            var credentials = await _credentialStore.LoadAsync();
            if (credentials is null)
            {
                await Shell.Current.GoToAsync("//login");
                return;
            }

            SetSyncing(true, $"Logging in as {credentials.AccountName}...");
            try
            {
                await _authService.LoginWithRefreshTokenAsync(credentials);
                SetSyncing(false, $"Logged in as {credentials.AccountName}");
            }
            catch
            {
                await _credentialStore.ClearAsync();
                await Shell.Current.GoToAsync("//login");
                return;
            }
        }

        await RefreshProfileStatus();
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
        => await RunSync(SyncDirection.Download);

    private async void OnUploadClicked(object? sender, EventArgs e)
        => await RunSync(SyncDirection.Upload);

    private async Task RunSync(SyncDirection direction)
    {
        SetSyncing(true, $"{direction}ing...");
        DownloadButton.IsEnabled = false;
        UploadButton.IsEnabled = false;

        try
        {
            var report = await _orchestrator.SyncAsync(direction);
            ShowReport(report);
            await RefreshProfileStatus();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Sync error: {ex.Message}";
        }
        finally
        {
            SetSyncing(false);
            DownloadButton.IsEnabled = true;
            UploadButton.IsEnabled = true;
        }
    }

    private void ShowReport(SyncReport report)
    {
        ReportFrame.IsVisible = true;
        ReportTitle.Text = report.Success ? "Sync Complete" : "Sync Failed";
        ReportTitle.TextColor = report.Success ? Colors.Green : Colors.Red;

        var details = report.Success
            ? $"Downloaded: {report.Downloaded}  Uploaded: {report.Uploaded}\n" +
              $"Identical: {report.Identical}  Conflicts: {report.Conflicts}\n" +
              $"Run history: {report.RunHistoryDownloaded} down, {report.RunHistoryUploaded} up"
            : $"Error: {report.Error}";

        ReportDetails.Text = details;
        StatusLabel.Text = report.Success ? "Sync completed successfully" : "Sync failed";
    }

    private async Task RefreshProfileStatus()
    {
        try
        {
            if (_cloudCache.IsLoaded || _authService.IsLoggedIn)
            {
                var scanner = new ProfileScanner(_localStore, _cloudCache);
                var profiles = await scanner.ScanAsync();
                RenderProfiles(profiles);
            }
            else
            {
                ProfileList.Children.Clear();
                ProfileList.Children.Add(new Label { Text = "Sync to see profile status", TextColor = Colors.Gray });
            }
        }
        catch
        {
            // Profile scan may fail if path not configured
        }
    }

    private void RenderProfiles(List<ProfileSyncStatus> profiles)
    {
        ProfileList.Children.Clear();

        foreach (var profile in profiles)
        {
            if (!profile.ExistsLocally && !profile.ExistsInCloud)
                continue;

            var frame = new Frame
            {
                Padding = 10,
                BorderColor = Colors.LightGray,
                Content = new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Label
                        {
                            Text = profile.ProfileName.ToUpperInvariant(),
                            FontAttributes = FontAttributes.Bold
                        }
                    }
                }
            };

            var stack = (VerticalStackLayout)frame.Content;

            foreach (var file in profile.Files)
            {
                var status = (file.Local, file.Cloud) switch
                {
                    (not null, not null) when file.ContentMatch => "In sync",
                    (not null, not null) => "Differs",
                    (not null, null) => "Local only",
                    (null, not null) => "Cloud only",
                    _ => "Missing"
                };

                var color = status switch
                {
                    "In sync" => Colors.Green,
                    "Differs" => Colors.Orange,
                    "Local only" => Colors.Blue,
                    "Cloud only" => Colors.Purple,
                    _ => Colors.Gray
                };

                stack.Children.Add(new Label
                {
                    Text = $"  {file.SaveFileName}: {status}",
                    TextColor = color,
                    FontSize = 13
                });
            }

            ProfileList.Children.Add(frame);
        }
    }

    private void SetSyncing(bool syncing, string? message = null)
    {
        SyncIndicator.IsRunning = syncing;
        SyncIndicator.IsVisible = syncing;
        if (message is not null)
            StatusLabel.Text = message;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync("settings");

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        await _authService.DisconnectAsync();
        await _credentialStore.ClearAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
