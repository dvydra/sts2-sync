namespace Sts2Sync.Core.Models;

public enum ResolutionOutcome
{
    Identical,
    CloudWins,
    LocalWins,
    Conflict
}

public enum WinReason
{
    ShaMatch,
    LocalMissing,
    CloudMissing,
    LocalCorrupt,
    CloudCorrupt,
    HeuristicWin,
    TieBreakCloudWins,
    ManualSelection
}

public record ConflictResult(
    string SaveFileName,
    ResolutionOutcome Outcome,
    WinReason Reason,
    ProgressAnalysis? LocalProgress,
    ProgressAnalysis? CloudProgress,
    RunAnalysis? LocalRun,
    RunAnalysis? CloudRun
);
