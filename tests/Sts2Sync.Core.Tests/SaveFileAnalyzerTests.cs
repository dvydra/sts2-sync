using Sts2Sync.Core.Models;
using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SaveFileAnalyzerTests
{
    [Fact]
    public void AnalyzeProgress_ExtractsCorrectValues()
    {
        var json = TestHelpers.LoadTestData("sample_progress.json");
        var save = SaveFileParser.ParseProgressSave(json).Value!;
        var analysis = SaveFileAnalyzer.AnalyzeProgress(save);

        Assert.Equal(1247, analysis.FloorsClimbed);
        // 12+38 + 8+25 + 3+15 + 1+8 + 0+3 = 113
        Assert.Equal(113, analysis.TotalGamesPlayed);
        // 20 + 11 + 6 + 8 + 3 = 48
        Assert.Equal(48, analysis.TotalDiscoveredItems);
        Assert.Equal(182340.5, analysis.TotalPlaytime);
    }

    [Fact]
    public void AnalyzeCurrentRun_ExtractsCorrectValues()
    {
        var json = TestHelpers.LoadTestData("sample_current_run.json");
        var save = SaveFileParser.ParseCurrentRun(json).Value!;
        var analysis = SaveFileAnalyzer.AnalyzeCurrentRun(save);

        Assert.Equal(23, analysis.FloorCount); // 15 + 8
        Assert.Equal(2, analysis.ActCount);
        Assert.Equal(12, analysis.DeckSize);
        Assert.Equal(3, analysis.RelicCount);
        Assert.Equal(1, analysis.PlayerCount);
        Assert.Equal(1523.7, analysis.RunTime);
    }

    [Fact]
    public void AnalyzeCurrentRun_FallsBackToActCount_WhenNoMapHistory()
    {
        var save = new CurrentRunSave
        {
            Acts = [new ActEntry { Id = "ACT.ExordiaCrypt" }, new ActEntry { Id = "ACT.TheCity" }],
            MapPointHistory = null,
            Players = []
        };
        var analysis = SaveFileAnalyzer.AnalyzeCurrentRun(save);
        Assert.Equal(2, analysis.FloorCount);
    }

    [Fact]
    public void CompareProgress_FloorsClimbedWins()
    {
        var left = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 10, TotalDiscoveredItems: 20, TotalPlaytime: 5000);
        var right = new ProgressAnalysis(FloorsClimbed: 50, TotalGamesPlayed: 10, TotalDiscoveredItems: 20, TotalPlaytime: 5000);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareProgress(left, right));
        Assert.Equal(CompareResult.RightWins, SaveFileAnalyzer.CompareProgress(right, left));
    }

    [Fact]
    public void CompareProgress_TotalGamesBreaksTie()
    {
        var left = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 20, TotalPlaytime: 5000);
        var right = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 15, TotalDiscoveredItems: 20, TotalPlaytime: 5000);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareProgress(left, right));
    }

    [Fact]
    public void CompareProgress_DiscoveredItemsBreaksTie()
    {
        var left = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 30, TotalPlaytime: 5000);
        var right = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 25, TotalPlaytime: 5000);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareProgress(left, right));
    }

    [Fact]
    public void CompareProgress_PlaytimeBreaksTie()
    {
        var left = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 30, TotalPlaytime: 6000);
        var right = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 30, TotalPlaytime: 5000);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareProgress(left, right));
    }

    [Fact]
    public void CompareProgress_AllEqual_ReturnsEqual()
    {
        var both = new ProgressAnalysis(FloorsClimbed: 100, TotalGamesPlayed: 20, TotalDiscoveredItems: 30, TotalPlaytime: 5000);

        Assert.Equal(CompareResult.Equal, SaveFileAnalyzer.CompareProgress(both, both));
    }

    [Fact]
    public void CompareCurrentRun_HigherFloorCountWins()
    {
        var left = new RunAnalysis(FloorCount: 23, ActCount: 2, DeckSize: 12, RelicCount: 3, PlayerCount: 1, RunTime: 1500);
        var right = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 10, RelicCount: 2, PlayerCount: 1, RunTime: 800);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareCurrentRun(left, right));
        Assert.Equal(CompareResult.RightWins, SaveFileAnalyzer.CompareCurrentRun(right, left));
    }

    [Fact]
    public void CompareCurrentRun_SameFloors_DeckSizeBreaksTie()
    {
        var left = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 12, RelicCount: 3, PlayerCount: 1, RunTime: 1500);
        var right = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 10, RelicCount: 3, PlayerCount: 1, RunTime: 800);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareCurrentRun(left, right));
    }

    [Fact]
    public void CompareCurrentRun_SameFloorsSameDeck_RelicCountBreaksTie()
    {
        var left = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 12, RelicCount: 4, PlayerCount: 1, RunTime: 1500);
        var right = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 12, RelicCount: 3, PlayerCount: 1, RunTime: 800);

        Assert.Equal(CompareResult.LeftWins, SaveFileAnalyzer.CompareCurrentRun(left, right));
    }

    [Fact]
    public void CompareCurrentRun_AllEqual_ReturnsEqual()
    {
        var both = new RunAnalysis(FloorCount: 15, ActCount: 1, DeckSize: 12, RelicCount: 3, PlayerCount: 1, RunTime: 1500);

        Assert.Equal(CompareResult.Equal, SaveFileAnalyzer.CompareCurrentRun(both, both));
    }

    [Fact]
    public void AnalyzeCurrentRun_FallsBackToActCount_WhenMapHistoryIsEmptyList()
    {
        var save = new CurrentRunSave
        {
            Acts = [new ActEntry { Id = "ACT.ExordiaCrypt" }],
            MapPointHistory = [],
            Players = []
        };
        var analysis = SaveFileAnalyzer.AnalyzeCurrentRun(save);
        Assert.Equal(1, analysis.FloorCount);
    }

    [Fact]
    public void AnalyzeProgress_EmptySave_HandlesGracefully()
    {
        var save = new ProgressSave();
        var analysis = SaveFileAnalyzer.AnalyzeProgress(save);

        Assert.Equal(0, analysis.FloorsClimbed);
        Assert.Equal(0, analysis.TotalGamesPlayed);
        Assert.Equal(0, analysis.TotalDiscoveredItems);
        Assert.Equal(0, analysis.TotalPlaytime);
    }

    [Fact]
    public void AnalyzeCurrentRun_EmptySave_HandlesGracefully()
    {
        var save = new CurrentRunSave();
        var analysis = SaveFileAnalyzer.AnalyzeCurrentRun(save);

        Assert.Equal(0, analysis.FloorCount);
        Assert.Equal(0, analysis.ActCount);
        Assert.Equal(0, analysis.DeckSize);
        Assert.Equal(0, analysis.RelicCount);
        Assert.Equal(0, analysis.PlayerCount);
    }
}
