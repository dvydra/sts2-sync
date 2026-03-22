using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class ActEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
