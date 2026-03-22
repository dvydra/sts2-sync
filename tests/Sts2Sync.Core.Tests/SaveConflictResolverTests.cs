using System.Text;
using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SaveConflictResolverTests
{
    // Minimal valid progress JSON with configurable fields
    private static byte[] ProgressJson(int floors = 0, double playtime = 0,
        int wins = 0, int losses = 0, int cards = 0)
    {
        var cardList = string.Join(",", Enumerable.Range(0, cards).Select(i => $"\"card{i}\""));
        var json = $$"""
            {
                "floors_climbed": {{floors}},
                "total_playtime": {{playtime}},
                "character_stats": [{"total_wins": {{wins}}, "total_losses": {{losses}}}],
                "discovered_cards": [{{cardList}}]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    // Minimal valid current_run JSON with configurable fields
    private static byte[] RunJson(int floors = 0, int deckSize = 0, int relicCount = 0)
    {
        var deck = string.Join(",", Enumerable.Range(0, deckSize).Select(i => $"{{\"id\":\"c{i}\"}}"));
        var relics = string.Join(",", Enumerable.Range(0, relicCount).Select(i => $"{{\"id\":\"r{i}\"}}"));
        // map_point_history: one act with `floors` entries
        var mapEntries = string.Join(",", Enumerable.Range(0, floors).Select(_ => "1"));
        var json = $$"""
            {
                "map_point_history": [[{{mapEntries}}]],
                "players": [{"deck": [{{deck}}], "relics": [{{relics}}]}],
                "acts": [{"id": "act1"}],
                "run_time": 100.0
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    // --- Identical ---

    [Fact]
    public void Resolve_BothNull_ReturnsIdentical()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save", null, null);

        Assert.Equal(ResolutionOutcome.Identical, result.Outcome);
        Assert.Equal(WinReason.ShaMatch, result.Reason);
    }

    [Fact]
    public void Resolve_SameBytes_ReturnsIdentical()
    {
        var data = ProgressJson(floors: 10);

        var result = SaveConflictResolver.Resolve("saves/progress.save", data, data);

        Assert.Equal(ResolutionOutcome.Identical, result.Outcome);
        Assert.Equal(WinReason.ShaMatch, result.Reason);
    }

    // --- One side missing ---

    [Fact]
    public void Resolve_LocalNull_CloudExists_ReturnsCloudWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save", null, ProgressJson());

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.LocalMissing, result.Reason);
    }

    [Fact]
    public void Resolve_CloudNull_LocalExists_ReturnsLocalWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save", ProgressJson(), null);

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.CloudMissing, result.Reason);
    }

    // --- Corruption ---

    [Fact]
    public void Resolve_LocalCorrupt_CloudValid_ReturnsCloudWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            "CORRUPT DATA"u8.ToArray(), ProgressJson(floors: 5));

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.LocalCorrupt, result.Reason);
    }

    [Fact]
    public void Resolve_CloudCorrupt_LocalValid_ReturnsLocalWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 5), "CORRUPT DATA"u8.ToArray());

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.CloudCorrupt, result.Reason);
    }

    [Fact]
    public void Resolve_BothCorrupt_ReturnsCloudWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            "BAD LOCAL"u8.ToArray(), "BAD CLOUD"u8.ToArray());

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.CloudCorrupt, result.Reason);
    }

    // --- Progress heuristics ---

    [Fact]
    public void Resolve_Progress_LocalMoreFloors_ReturnsLocalWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 100), ProgressJson(floors: 50));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_Progress_CloudMoreFloors_ReturnsCloudWins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 50), ProgressJson(floors: 100));

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_Progress_SameFloors_MoreGames_Wins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 50, wins: 10, losses: 5),
            ProgressJson(floors: 50, wins: 5, losses: 3));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_Progress_SameFloors_SameGames_MoreItems_Wins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 50, wins: 5, losses: 5, cards: 10),
            ProgressJson(floors: 50, wins: 5, losses: 5, cards: 5));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_Progress_SameFloors_SameGames_SameItems_MorePlaytime_Wins()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 50, wins: 5, losses: 5, cards: 5, playtime: 1000),
            ProgressJson(floors: 50, wins: 5, losses: 5, cards: 5, playtime: 500));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_Progress_AllEqual_ReturnsCloudWins_TieBreak()
    {
        // Same stats but different bytes (e.g. different extension data)
        var localJson = """{"floors_climbed": 50, "total_playtime": 100, "extra_local": true}""";
        var cloudJson = """{"floors_climbed": 50, "total_playtime": 100, "extra_cloud": true}""";

        var result = SaveConflictResolver.Resolve("saves/progress.save",
            Encoding.UTF8.GetBytes(localJson), Encoding.UTF8.GetBytes(cloudJson));

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.TieBreakCloudWins, result.Reason);
    }

    // --- Current run heuristics ---

    [Fact]
    public void Resolve_CurrentRun_LocalMoreFloors_ReturnsLocalWins()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            RunJson(floors: 15), RunJson(floors: 5));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_CurrentRun_CloudMoreFloors_ReturnsCloudWins()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            RunJson(floors: 5), RunJson(floors: 15));

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_CurrentRun_SameFloors_MoreDeck_Wins()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            RunJson(floors: 10, deckSize: 15), RunJson(floors: 10, deckSize: 8));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_CurrentRun_SameFloors_SameDeck_MoreRelics_Wins()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            RunJson(floors: 10, deckSize: 10, relicCount: 5),
            RunJson(floors: 10, deckSize: 10, relicCount: 2));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    [Fact]
    public void Resolve_CurrentRun_AllEqual_ReturnsCloudWins_TieBreak()
    {
        var localJson = """{"map_point_history": [[1,2]], "players": [{"deck": [{"id":"c1"}], "relics": [{"id":"r1"}]}], "acts": [], "run_time": 100, "extra_local": 1}""";
        var cloudJson = """{"map_point_history": [[1,2]], "players": [{"deck": [{"id":"c1"}], "relics": [{"id":"r1"}]}], "acts": [], "run_time": 100, "extra_cloud": 1}""";

        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            Encoding.UTF8.GetBytes(localJson), Encoding.UTF8.GetBytes(cloudJson));

        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
        Assert.Equal(WinReason.TieBreakCloudWins, result.Reason);
    }

    // --- current_run_mp uses run comparison ---

    [Fact]
    public void Resolve_CurrentRunMp_UsesRunComparison()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run_mp.save",
            RunJson(floors: 20), RunJson(floors: 5));

        Assert.Equal(ResolutionOutcome.LocalWins, result.Outcome);
        Assert.Equal(WinReason.HeuristicWin, result.Reason);
    }

    // --- Parse failure treated as corrupt ---

    [Fact]
    public void Resolve_LocalParseFailure_CloudValid_ReturnsCloudWins()
    {
        // Valid JSON but not a valid progress save structure
        var badLocal = """{"not_a_progress_field": true}"""u8.ToArray();

        var result = SaveConflictResolver.Resolve("saves/progress.save",
            badLocal, ProgressJson(floors: 10));

        // Should still resolve (parser returns defaults for missing fields)
        // The local will have 0 floors, cloud has 10 → cloud wins via heuristic
        Assert.Equal(ResolutionOutcome.CloudWins, result.Outcome);
    }

    // --- Analysis populated in result ---

    [Fact]
    public void Resolve_Progress_ResultContainsAnalysis()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save",
            ProgressJson(floors: 100, wins: 5, losses: 3),
            ProgressJson(floors: 50));

        Assert.NotNull(result.LocalProgress);
        Assert.NotNull(result.CloudProgress);
        Assert.Equal(100, result.LocalProgress!.FloorsClimbed);
        Assert.Equal(50, result.CloudProgress!.FloorsClimbed);
        Assert.Equal(8, result.LocalProgress.TotalGamesPlayed); // 5 wins + 3 losses
        Assert.Null(result.LocalRun);
        Assert.Null(result.CloudRun);
    }

    [Fact]
    public void Resolve_CurrentRun_ResultContainsRunAnalysis()
    {
        var result = SaveConflictResolver.Resolve("saves/current_run.save",
            RunJson(floors: 15, deckSize: 10, relicCount: 3),
            RunJson(floors: 5));

        Assert.NotNull(result.LocalRun);
        Assert.NotNull(result.CloudRun);
        Assert.Equal(15, result.LocalRun!.FloorCount);
        Assert.Equal(10, result.LocalRun.DeckSize);
        Assert.Equal(5, result.CloudRun!.FloorCount);
        Assert.Null(result.LocalProgress);
        Assert.Null(result.CloudProgress);
    }

    [Fact]
    public void Resolve_OneSideMissing_NoAnalysis()
    {
        var result = SaveConflictResolver.Resolve("saves/progress.save", null, ProgressJson());

        Assert.Null(result.LocalProgress);
        Assert.Null(result.CloudProgress);
        Assert.Null(result.LocalRun);
        Assert.Null(result.CloudRun);
    }
}
