using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("gourmand_action")]
    public string GourmandAction { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_schema_version")]
    public string GourmandSchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_projection_status")]
    public string GourmandProjectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_interaction_stage")]
    public string GourmandInteractionStage { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_request_index_before")]
    public int? GourmandRequestIndexBefore { get; set; }

    [JsonPropertyName("gourmand_request_index_after")]
    public int? GourmandRequestIndexAfter { get; set; }

    [JsonPropertyName("gourmand_required_crop_item_id")]
    public string GourmandRequiredCropItemId { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_required_crop_qualified_item_id")]
    public string GourmandRequiredCropQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_ready_matching_crop_count")]
    public int? GourmandReadyMatchingCropCount { get; set; }

    [JsonPropertyName("gourmand_talked_before")]
    public bool? GourmandTalkedBefore { get; set; }

    [JsonPropertyName("gourmand_talked_after")]
    public bool? GourmandTalkedAfter { get; set; }

    [JsonPropertyName("gourmand_expected_collected_nut_key")]
    public string GourmandExpectedCollectedNutKey { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_walnut_debris_count_before")]
    public int? GourmandWalnutDebrisCountBefore { get; set; }

    [JsonPropertyName("gourmand_walnut_debris_count_after")]
    public int? GourmandWalnutDebrisCountAfter { get; set; }

    [JsonPropertyName("gourmand_expected_walnut_debris_spawn_count")]
    public int? GourmandExpectedWalnutDebrisSpawnCount { get; set; }

    [JsonPropertyName("gourmand_golden_walnuts_found_before")]
    public int? GourmandGoldenWalnutsFoundBefore { get; set; }

    [JsonPropertyName("gourmand_golden_walnuts_found_delta")]
    public int? GourmandGoldenWalnutsFoundDelta { get; set; }

    [JsonPropertyName("gourmand_golden_walnuts_found_after")]
    public int? GourmandGoldenWalnutsFoundAfter { get; set; }

    [JsonPropertyName("gourmand_question_key")]
    public string GourmandQuestionKey { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_response_key")]
    public string GourmandResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("gourmand_output_delivery")]
    public string GourmandOutputDelivery { get; set; } = string.Empty;
}
