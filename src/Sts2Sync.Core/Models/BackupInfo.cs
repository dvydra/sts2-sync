namespace Sts2Sync.Core.Models;

public record BackupInfo(
    string OriginalPath,
    string BackupPath,
    long UnixTimestamp,
    string Source
);
