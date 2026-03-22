using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class CharacterStats
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("total_wins")]
    public int TotalWins { get; set; }

    [JsonPropertyName("total_losses")]
    public int TotalLosses { get; set; }

    [JsonPropertyName("best_win_streak")]
    public int BestWinStreak { get; set; }

    [JsonPropertyName("current_streak")]
    public int CurrentStreak { get; set; }

    [JsonPropertyName("fastest_win_time")]
    public double FastestWinTime { get; set; }

    [JsonPropertyName("max_ascension")]
    public int MaxAscension { get; set; }

    [JsonPropertyName("playtime")]
    public double Playtime { get; set; }

    [JsonPropertyName("preferred_ascension")]
    public int PreferredAscension { get; set; }

    [JsonPropertyName("floors_climbed")]
    public int FloorsClimbed { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
