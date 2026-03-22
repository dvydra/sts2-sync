namespace Sts2Sync.Core.Models;

public record FileSyncStatus(
    string SaveFileName,
    LocalFileInfo? Local,
    CloudFileInfo? Cloud,
    bool ContentMatch
);

public record ProfileSyncStatus(
    string ProfileName,
    bool ExistsLocally,
    bool ExistsInCloud,
    IReadOnlyList<FileSyncStatus> Files
);
