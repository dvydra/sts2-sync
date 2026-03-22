using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class CardEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("current_upgrade_level")]
    public int CurrentUpgradeLevel { get; set; }

    [JsonPropertyName("floor_added_to_deck")]
    public int FloorAddedToDeck { get; set; }

    [JsonPropertyName("props")]
    public Dictionary<string, object?>? Props { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
