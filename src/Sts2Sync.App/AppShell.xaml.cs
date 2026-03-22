namespace Sts2Sync.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("sync", typeof(Pages.SyncPage));
        Routing.RegisterRoute("settings", typeof(Pages.SettingsPage));
    }
}
