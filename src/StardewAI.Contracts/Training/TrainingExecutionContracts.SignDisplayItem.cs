using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("sign_display_fixture_family")]
    public string SignDisplayFixtureFamily { get; set; } = string.Empty;

    [JsonPropertyName("sign_display_source_runtime_type")]
    public string SignDisplaySourceRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("sign_display_source_quality")]
    public int? SignDisplaySourceQuality { get; set; }

    [JsonPropertyName("sign_display_source_state_sha256")]
    public string SignDisplaySourceStateSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sign_display_target_projection_fingerprint")]
    public string SignDisplayTargetProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("sign_display_target_qualified_item_id")]
    public string SignDisplayTargetQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("sign_display_target_state_sha256")]
    public string SignDisplayTargetStateSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sign_previous_display_item_qualified_item_id")]
    public string SignPreviousDisplayItemQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("sign_previous_display_item_runtime_type")]
    public string SignPreviousDisplayItemRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("sign_previous_display_item_state_sha256")]
    public string SignPreviousDisplayItemStateSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sign_previous_display_type")]
    public int? SignPreviousDisplayType { get; set; }

    [JsonPropertyName("sign_replace_existing_display")]
    public bool? SignReplaceExistingDisplay { get; set; }

    [JsonPropertyName("sign_allow_replace_existing_display")]
    public bool? SignAllowReplaceExistingDisplay { get; set; }
}
