namespace Sts2Sync.Core.Models;

public record CloudFileInfo(
    string Filename,
    uint FileSize,
    uint RawFileSize,
    ulong Timestamp,
    string FileSha,
    string? Url
);
