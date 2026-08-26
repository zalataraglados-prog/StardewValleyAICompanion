using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("text_sign_fixture_initial_text")]
    public string TextSignFixtureInitialText { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_target_projection_fingerprint")]
    public string TextSignTargetProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_target_qualified_item_id")]
    public string TextSignTargetQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_target_state_sha256")]
    public string TextSignTargetStateSha256 { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_raw_before")]
    public string TextSignRawBefore { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_display_before")]
    public string TextSignDisplayBefore { get; set; } = string.Empty;

    [JsonPropertyName("text_sign_show_next_index_before")]
    public bool? TextSignShowNextIndexBefore { get; set; }

    [JsonPropertyName("text_sign_replaces_existing_text")]
    public bool? TextSignReplacesExistingText { get; set; }

    [JsonPropertyName("text_sign_allow_replace_existing_text")]
    public bool? TextSignAllowReplaceExistingText { get; set; }

    [JsonPropertyName("text_sign_requested_text")]
    public string? TextSignRequestedText { get; set; }
}
