namespace Sts2Sync.Core.Models;

public enum SyncDirection
{
    Download,
    Upload,
    Both
}

public record FileSyncAction(
    string ProfileName,
    string SaveFileName,
    ResolutionOutcome Outcome,
    WinReason Reason
);

public record SyncReport(
    DateTimeOffset Timestamp,
    bool Success,
    string? Error,
    IReadOnlyList<FileSyncAction> Actions,
    int Downloaded,
    int Uploaded,
    int Identical,
    int Conflicts,
    int RunHistoryDownloaded,
    int RunHistoryUploaded
);
