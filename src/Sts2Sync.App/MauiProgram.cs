using Sts2Sync.App.Services;
using Sts2Sync.Core;
using Sts2Sync.Core.Services;

namespace Sts2Sync.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Core services — singletons for persistent state
        builder.Services.AddSingleton<ISteamConnectionManager>(sp =>
            new SteamConnectionManager());
        builder.Services.AddSingleton<ISteamAuthService>(sp =>
            new SteamAuthService(sp.GetRequiredService<ISteamConnectionManager>()));
        builder.Services.AddSingleton<ISteamCloudService>(sp =>
            new SteamCloudService(sp.GetRequiredService<ISteamConnectionManager>()));
        builder.Services.AddSingleton<CloudFileCache>(sp =>
            new CloudFileCache(sp.GetRequiredService<ISteamCloudService>(), Constants.Sts2AppId));

        // Credential & settings stores
        builder.Services.AddSingleton<ICredentialStore, SecureCredentialStore>();
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();

        // Logger
        builder.Services.AddSingleton<ISyncLogger, ConsoleSyncLogger>();

        // Transient services — created per use
        builder.Services.AddTransient<IBackupManager>(sp =>
            new BackupManager(sp.GetRequiredService<ILocalSaveStore>()));
        builder.Services.AddTransient<SyncOrchestrator>();

        // LocalSaveStore — registered after settings are loaded (needs basePath)
        // Resolved lazily via factory
        builder.Services.AddSingleton<ILocalSaveStore>(sp =>
        {
            var settings = sp.GetRequiredService<ISettingsStore>();
            var pathSettings = settings.LoadSavePathAsync().GetAwaiter().GetResult();
            var basePath = pathSettings?.BasePath ?? Constants.GameHubDriveMappedPath;
            return new LocalSaveStore(basePath);
        });

        // Pages
        builder.Services.AddTransient<Pages.LoginPage>();
        builder.Services.AddTransient<Pages.SyncPage>();
        builder.Services.AddTransient<Pages.SettingsPage>();

        return builder.Build();
    }
}
