using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class CurrentRunSave
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("ascension")]
    public int Ascension { get; set; }

    [JsonPropertyName("current_act_index")]
    public int CurrentActIndex { get; set; }

    [JsonPropertyName("run_time")]
    public double RunTime { get; set; }

    [JsonPropertyName("acts")]
    public List<ActEntry>? Acts { get; set; }

    [JsonPropertyName("map_point_history")]
    public List<List<JsonElement>>? MapPointHistory { get; set; }

    [JsonPropertyName("players")]
    public List<PlayerState>? Players { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
