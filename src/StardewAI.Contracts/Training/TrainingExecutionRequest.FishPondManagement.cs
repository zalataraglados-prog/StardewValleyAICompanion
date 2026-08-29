using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("management_operation")]
    public string ManagementOperation { get; set; } = string.Empty;

    [JsonPropertyName("fish_pond_management_reason")]
    public string FishPondManagementReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_empty_pond")]
    public bool? ConfirmEmptyPond { get; set; }

    [JsonPropertyName("expected_fish_count_after")]
    public int? ExpectedFishCountAfter { get; set; }

    [JsonPropertyName("expected_needed_item_qualified_item_id_before")]
    public string ExpectedNeededItemQualifiedItemIdBefore { get; set; } = string.Empty;

    [JsonPropertyName("expected_needed_item_count_before")]
    public int? ExpectedNeededItemCountBefore { get; set; }

    [JsonPropertyName("expected_has_completed_request_before")]
    public int? ExpectedHasCompletedRequestBefore { get; set; }

    [JsonPropertyName("expected_golden_animal_cracker_before")]
    public int? ExpectedGoldenAnimalCrackerBefore { get; set; }

    [JsonPropertyName("expected_golden_animal_cracker_after")]
    public int? ExpectedGoldenAnimalCrackerAfter { get; set; }

    [JsonPropertyName("expected_has_spawned_fish_before")]
    public int? ExpectedHasSpawnedFishBefore { get; set; }

    [JsonPropertyName("expected_has_spawned_fish_after")]
    public int? ExpectedHasSpawnedFishAfter { get; set; }

    [JsonPropertyName("expected_netting_style_before")]
    public int? ExpectedNettingStyleBefore { get; set; }

    [JsonPropertyName("expected_netting_style_after")]
    public int? ExpectedNettingStyleAfter { get; set; }

    [JsonPropertyName("expected_fish_debris_qualified_item_id")]
    public string ExpectedFishDebrisQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("expected_fish_debris_count")]
    public int? ExpectedFishDebrisCount { get; set; }

    [JsonPropertyName("expected_sign_qualified_item_id_before")]
    public string ExpectedSignQualifiedItemIdBefore { get; set; } = string.Empty;

    [JsonPropertyName("expected_output_qualified_item_id_before")]
    public string ExpectedOutputQualifiedItemIdBefore { get; set; } = string.Empty;

    [JsonPropertyName("expected_override_water_color_packed_before")]
    public long? ExpectedOverrideWaterColorPackedBefore { get; set; }
}
