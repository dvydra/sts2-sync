namespace Sts2Sync.Core;

public static class Constants
{
    public const uint Sts2AppId = 2868840;

    public static readonly string[] ProfileNames = ["profile1", "profile2", "profile3"];

    public static readonly string[] SaveFileNames =
    [
        "saves/progress.save",
        "saves/current_run.save",
        "saves/current_run_mp.save"
    ];

    public const int MaxBackupsPerProfile = 50;

    public const string GameHubDriveMappedPath = "/sdcard/STS2Saves";

    public const string GameHubRootPathTemplate =
        "/data/user/0/com.xiaoji.egggame/files/containers/{containerId}/.wine/drive_c/users/user/AppData/Roaming/SlayTheSpire2/steam/{steamId}";
}
