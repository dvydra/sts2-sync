namespace Sts2Sync.Core.Models;

/// <summary>
/// Aggregated conflict-relevant fields extracted from a progress.save file.
/// All fields are monotonically increasing during normal gameplay.
/// </summary>
public record ProgressAnalysis(
    int FloorsClimbed,
    int TotalGamesPlayed,
    int TotalDiscoveredItems,
    double TotalPlaytime
);

/// <summary>
/// Aggregated conflict-relevant fields extracted from a current_run.save file.
/// </summary>
public record RunAnalysis(
    int FloorCount,
    int ActCount,
    int DeckSize,
    int RelicCount,
    int PlayerCount,
    double RunTime
);

public enum CompareResult
{
    Equal,
    LeftWins,
    RightWins
}
