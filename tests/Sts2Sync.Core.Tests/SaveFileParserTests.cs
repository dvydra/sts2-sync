using Sts2Sync.Core.Services;

namespace Sts2Sync.Core.Tests;

public class SaveFileParserTests
{
    [Fact]
    public void ParseProgressSave_ValidJson_ReturnsSuccess()
    {
        var json = TestHelpers.LoadTestData("sample_progress.json");
        var result = SaveFileParser.ParseProgressSave(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1247, result.Value!.FloorsClimbed);
        Assert.Equal(182340.5, result.Value.TotalPlaytime);
    }

    [Fact]
    public void ParseProgressSave_ParsesCharacterStats()
    {
        var json = TestHelpers.LoadTestData("sample_progress.json");
        var result = SaveFileParser.ParseProgressSave(json);

        Assert.True(result.IsSuccess);
        var stats = result.Value!.CharacterStats;
        Assert.NotNull(stats);
        Assert.Equal(5, stats!.Count);

        var ironclad = stats.First(s => s.Id == "CHARACTER.IRONCLAD");
        Assert.Equal(12, ironclad.TotalWins);
        Assert.Equal(38, ironclad.TotalLosses);
        Assert.Equal(3, ironclad.BestWinStreak);
        Assert.Equal(8, ironclad.MaxAscension);
        Assert.Equal(520, ironclad.FloorsClimbed);
    }

    [Fact]
    public void ParseProgressSave_ParsesDiscoveredItems()
    {
        var json = TestHelpers.LoadTestData("sample_progress.json");
        var result = SaveFileParser.ParseProgressSave(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value!.DiscoveredCards!.Count);
        Assert.Equal(11, result.Value.DiscoveredRelics!.Count);
        Assert.Equal(6, result.Value.DiscoveredPotions!.Count);
        Assert.Equal(8, result.Value.DiscoveredEvents!.Count);
        Assert.Equal(3, result.Value.DiscoveredActs!.Count);
    }

    [Fact]
    public void ParseProgressSave_EmptyInput_ReturnsError()
    {
        var result = SaveFileParser.ParseProgressSave("");
        Assert.False(result.IsSuccess);
        Assert.Equal("Empty input", result.Error);
    }

    [Fact]
    public void ParseProgressSave_WhitespaceOnly_ReturnsError()
    {
        var result = SaveFileParser.ParseProgressSave("   \t\n  ");
        Assert.False(result.IsSuccess);
        Assert.Equal("Empty input", result.Error);
    }

    [Fact]
    public void ParseProgressSave_CorruptJson_ReturnsError()
    {
        var result = SaveFileParser.ParseProgressSave("not json at all");
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid JSON", result.Error);
    }

    [Fact]
    public void ParseProgressSave_MalformedJson_ReturnsError()
    {
        var result = SaveFileParser.ParseProgressSave("{\"floors_climbed\": }");
        Assert.False(result.IsSuccess);
        Assert.Contains("JSON parse error", result.Error);
    }

    [Fact]
    public void ParseProgressSave_PreservesUnknownFields()
    {
        var json = """{"floors_climbed": 100, "some_new_field": "hello"}""";
        var result = SaveFileParser.ParseProgressSave(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ExtensionData);
        Assert.True(result.Value.ExtensionData!.ContainsKey("some_new_field"));
    }

    [Fact]
    public void ParseCurrentRun_ValidJson_ReturnsSuccess()
    {
        var json = TestHelpers.LoadTestData("sample_current_run.json");
        var result = SaveFileParser.ParseCurrentRun(json);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value!.SchemaVersion);
        Assert.Equal(5, result.Value.Ascension);
        Assert.Equal(1, result.Value.CurrentActIndex);
    }

    [Fact]
    public void ParseCurrentRun_ParsesPlayers()
    {
        var json = TestHelpers.LoadTestData("sample_current_run.json");
        var result = SaveFileParser.ParseCurrentRun(json);

        Assert.True(result.IsSuccess);
        var players = result.Value!.Players;
        Assert.NotNull(players);
        Assert.Single(players!);

        var player = players[0];
        Assert.Equal("CHARACTER.IRONCLAD", player.CharacterId);
        Assert.Equal(52, player.CurrentHp);
        Assert.Equal(80, player.MaxHp);
        Assert.Equal(234, player.Gold);
        Assert.Equal(12, player.Deck!.Count);
        Assert.Equal(3, player.Relics!.Count);
        Assert.Equal(2, player.Potions!.Count);
    }

    [Fact]
    public void ParseCurrentRun_ParsesMapPointHistory()
    {
        var json = TestHelpers.LoadTestData("sample_current_run.json");
        var result = SaveFileParser.ParseCurrentRun(json);

        Assert.True(result.IsSuccess);
        var history = result.Value!.MapPointHistory;
        Assert.NotNull(history);
        Assert.Equal(2, history!.Count);
        Assert.Equal(15, history[0].Count); // act 1: 15 floors
        Assert.Equal(8, history[1].Count);  // act 2: 8 floors
    }

    [Fact]
    public void ParseCurrentRun_ParsesCardDetails()
    {
        var json = TestHelpers.LoadTestData("sample_current_run.json");
        var result = SaveFileParser.ParseCurrentRun(json);

        var deck = result.Value!.Players![0].Deck!;
        var upgraded = deck.First(c => c.CurrentUpgradeLevel > 0);
        Assert.Equal("CARD.Strike", upgraded.Id);
        Assert.Equal(1, upgraded.CurrentUpgradeLevel);
    }

    [Fact]
    public void ParseCurrentRun_EmptyInput_ReturnsError()
    {
        var result = SaveFileParser.ParseCurrentRun("");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseCurrentRun_CorruptJson_ReturnsError()
    {
        var result = SaveFileParser.ParseCurrentRun("corrupt data");
        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid JSON", result.Error);
    }
}
