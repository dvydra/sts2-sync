using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class ProgressSave
{
    [JsonPropertyName("floors_climbed")]
    public int FloorsClimbed { get; set; }

    [JsonPropertyName("total_playtime")]
    public double TotalPlaytime { get; set; }

    [JsonPropertyName("character_stats")]
    public List<CharacterStats>? CharacterStats { get; set; }

    [JsonPropertyName("discovered_cards")]
    public List<string>? DiscoveredCards { get; set; }

    [JsonPropertyName("discovered_relics")]
    public List<string>? DiscoveredRelics { get; set; }

    [JsonPropertyName("discovered_potions")]
    public List<string>? DiscoveredPotions { get; set; }

    [JsonPropertyName("discovered_events")]
    public List<string>? DiscoveredEvents { get; set; }

    [JsonPropertyName("discovered_acts")]
    public List<string>? DiscoveredActs { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
