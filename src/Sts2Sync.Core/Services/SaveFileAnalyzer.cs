using Sts2Sync.Core.Models;

namespace Sts2Sync.Core.Services;

public static class SaveFileAnalyzer
{
    public static ProgressAnalysis AnalyzeProgress(ProgressSave save)
    {
        var totalGames = save.CharacterStats?
            .Sum(cs => cs.TotalWins + cs.TotalLosses) ?? 0;

        var discoveredItems =
            (save.DiscoveredCards?.Count ?? 0) +
            (save.DiscoveredRelics?.Count ?? 0) +
            (save.DiscoveredPotions?.Count ?? 0) +
            (save.DiscoveredEvents?.Count ?? 0) +
            (save.DiscoveredActs?.Count ?? 0);

        return new ProgressAnalysis(
            FloorsClimbed: save.FloorsClimbed,
            TotalGamesPlayed: totalGames,
            TotalDiscoveredItems: discoveredItems,
            TotalPlaytime: save.TotalPlaytime
        );
    }

    public static RunAnalysis AnalyzeCurrentRun(CurrentRunSave save)
    {
        // Floor count: sum of inner array lengths in map_point_history
        // Falls back to acts count if map_point_history is missing or empty
        var mapFloors = save.MapPointHistory is { Count: > 0 }
            ? save.MapPointHistory.Sum(act => act.Count)
            : 0;

        var actCount = save.Acts?.Count ?? 0;
        var floorCount = mapFloors > 0 ? mapFloors : actCount;

        var deckSize = save.Players?
            .Sum(p => p.Deck?.Count ?? 0) ?? 0;

        var relicCount = save.Players?
            .Sum(p => p.Relics?.Count ?? 0) ?? 0;

        return new RunAnalysis(
            FloorCount: floorCount,
            ActCount: actCount,
            DeckSize: deckSize,
            RelicCount: relicCount,
            PlayerCount: save.Players?.Count ?? 0,
            RunTime: save.RunTime
        );
    }

    /// <summary>
    /// Compare two progress saves using the cascading heuristic from StS2-Launcher.
    /// Returns LeftWins if left is more progressed, RightWins if right is, Equal if tied.
    /// </summary>
    public static CompareResult CompareProgress(ProgressAnalysis left, ProgressAnalysis right)
    {
        // 1. Floors climbed — higher wins
        if (left.FloorsClimbed != right.FloorsClimbed)
            return left.FloorsClimbed > right.FloorsClimbed
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 2. Total games played — higher wins
        if (left.TotalGamesPlayed != right.TotalGamesPlayed)
            return left.TotalGamesPlayed > right.TotalGamesPlayed
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 3. Total discovered items — higher wins
        if (left.TotalDiscoveredItems != right.TotalDiscoveredItems)
            return left.TotalDiscoveredItems > right.TotalDiscoveredItems
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 4. Total playtime — higher wins
        if (left.TotalPlaytime != right.TotalPlaytime)
            return left.TotalPlaytime > right.TotalPlaytime
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 5. All equal
        return CompareResult.Equal;
    }

    /// <summary>
    /// Compare two current run saves. Cascading: floor count → deck size → relic count.
    /// </summary>
    public static CompareResult CompareCurrentRun(RunAnalysis left, RunAnalysis right)
    {
        // 1. Floor count — higher wins
        if (left.FloorCount != right.FloorCount)
            return left.FloorCount > right.FloorCount
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 2. Deck size — more cards picked up = more progress at same floor
        if (left.DeckSize != right.DeckSize)
            return left.DeckSize > right.DeckSize
                ? CompareResult.LeftWins : CompareResult.RightWins;

        // 3. Relic count
        if (left.RelicCount != right.RelicCount)
            return left.RelicCount > right.RelicCount
                ? CompareResult.LeftWins : CompareResult.RightWins;

        return CompareResult.Equal;
    }
}
