using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class PotionEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
