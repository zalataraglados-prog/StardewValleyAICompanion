using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("construction_building_type")]
    public string ConstructionBuildingType { get; set; } = string.Empty;

    [JsonPropertyName("construction_build_days")]
    public int? ConstructionBuildDays { get; set; }

    [JsonPropertyName("construction_materials_json")]
    public string ConstructionMaterialsJson { get; set; } = string.Empty;

    [JsonPropertyName("placement_location_id")]
    public string PlacementLocationId { get; set; } = string.Empty;

    [JsonPropertyName("placement_verification")]
    public string PlacementVerification { get; set; } = string.Empty;
}
