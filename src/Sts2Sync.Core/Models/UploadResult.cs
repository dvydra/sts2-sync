namespace Sts2Sync.Core.Models;

public record UploadResult(
    string Filename,
    bool Success,
    string? Error
);
