namespace Sts2Sync.Core.Models;

public record RunMergeResult(
    int Downloaded,
    int Uploaded,
    int AlreadySynced
);
