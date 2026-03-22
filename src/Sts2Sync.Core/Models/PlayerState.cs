using System.Text.Json.Serialization;

namespace Sts2Sync.Core.Models;

public class PlayerState
{
    [JsonPropertyName("character_id")]
    public string? CharacterId { get; set; }

    [JsonPropertyName("current_hp")]
    public int CurrentHp { get; set; }

    [JsonPropertyName("max_hp")]
    public int MaxHp { get; set; }

    [JsonPropertyName("gold")]
    public int Gold { get; set; }

    [JsonPropertyName("max_energy")]
    public int MaxEnergy { get; set; }

    [JsonPropertyName("max_potion_slot_count")]
    public int MaxPotionSlotCount { get; set; }

    [JsonPropertyName("base_orb_slot_count")]
    public int BaseOrbSlotCount { get; set; }

    [JsonPropertyName("deck")]
    public List<CardEntry>? Deck { get; set; }

    [JsonPropertyName("relics")]
    public List<RelicEntry>? Relics { get; set; }

    [JsonPropertyName("potions")]
    public List<PotionEntry>? Potions { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? ExtensionData { get; set; }
}
