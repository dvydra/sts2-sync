namespace Sts2Sync.Core.Models;

public record CloudFileContent(
    string Filename,
    byte[] Data,
    string Sha,
    ulong Timestamp
);
