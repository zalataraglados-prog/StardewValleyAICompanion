using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("tailoring_candidate_id")]
    public string TailoringCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_operation")]
    public string TailoringOperation { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_purpose")]
    public string TailoringPurpose { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_recipe_id")]
    public string TailoringRecipeId { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_source_id")]
    public string TailoringSourceId { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_source_kind")]
    public string TailoringSourceKind { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_spend_left_count")]
    public int? TailoringSpendLeftCount { get; set; }

    [JsonPropertyName("tailoring_spend_right_count")]
    public int? TailoringSpendRightCount { get; set; }

    [JsonPropertyName("tailoring_output_contract_kind")]
    public string TailoringOutputContractKind { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_tailored_counts_before_json")]
    public string TailoringTailoredCountsBeforeJson { get; set; } = string.Empty;

    [JsonPropertyName("tailoring_marks_tailored_item")]
    public bool? TailoringMarksTailoredItem { get; set; }

    [JsonPropertyName("tailoring_native_contract")]
    public string TailoringNativeContract { get; set; } = string.Empty;
}
