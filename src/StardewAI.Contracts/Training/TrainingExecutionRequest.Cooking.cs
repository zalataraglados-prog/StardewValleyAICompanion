using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("craft_count")]
    public int? CraftCount { get; set; }

    [JsonPropertyName("cooking_reason")]
    public string CookingReason { get; set; } = string.Empty;

    [JsonPropertyName("cooking_source_id")]
    public string CookingSourceId { get; set; } = string.Empty;

    [JsonPropertyName("cooking_source_kind")]
    public string CookingSourceKind { get; set; } = string.Empty;

    [JsonPropertyName("recipes_cooked_before")]
    public int? RecipesCookedBefore { get; set; }

    [JsonPropertyName("seasoning_rows_json")]
    public string SeasoningRowsJson { get; set; } = string.Empty;

    [JsonPropertyName("material_container_ids_json")]
    public string MaterialContainerIdsJson { get; set; } = string.Empty;

    [JsonPropertyName("expected_output_order_data")]
    public string ExpectedOutputOrderData { get; set; } = string.Empty;
}
