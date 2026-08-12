using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("appearance_reason")]
    public string AppearanceReason { get; set; } = string.Empty;

    [JsonPropertyName("building_identity")]
    public string BuildingIdentity { get; set; } = string.Empty;

    [JsonPropertyName("building_location_id")]
    public string BuildingLocationId { get; set; } = string.Empty;

    [JsonPropertyName("building_type")]
    public string BuildingType { get; set; } = string.Empty;

    [JsonPropertyName("current_skin_key")]
    public string CurrentSkinKey { get; set; } = string.Empty;

    [JsonPropertyName("current_skin_id")]
    public string CurrentSkinId { get; set; } = string.Empty;

    [JsonPropertyName("current_skin_index")]
    public int? CurrentSkinIndex { get; set; }

    [JsonPropertyName("target_skin_key")]
    public string TargetSkinKey { get; set; } = string.Empty;

    [JsonPropertyName("target_skin_id")]
    public string TargetSkinId { get; set; } = string.Empty;

    [JsonPropertyName("target_skin_index")]
    public int? TargetSkinIndex { get; set; }

    [JsonPropertyName("available_skin_count")]
    public int? AvailableSkinCount { get; set; }

    [JsonPropertyName("available_skin_keys_json")]
    public string AvailableSkinKeysJson { get; set; } = string.Empty;

    [JsonPropertyName("shortest_click_direction")]
    public string ShortestClickDirection { get; set; } = string.Empty;

    [JsonPropertyName("shortest_click_count")]
    public int? ShortestClickCount { get; set; }

    [JsonPropertyName("entry_route")]
    public string EntryRoute { get; set; } = string.Empty;

    [JsonPropertyName("skin_change_resets_all_paint_colors_to_default")]
    public bool SkinChangeResetsAllPaintColorsToDefault { get; set; }
}
