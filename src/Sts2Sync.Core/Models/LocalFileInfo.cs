namespace Sts2Sync.Core.Models;

public record LocalFileInfo(
    string RelativePath,
    long FileSize,
    string Sha256,
    string Sha1,
    DateTimeOffset LastModified
);
