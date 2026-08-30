using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("prairie_king_projection_fingerprint")]
    public string PrairieKingProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_action_raw")]
    public string PrairieKingActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_action_token")]
    public string PrairieKingActionToken { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_dialogue_key")]
    public string PrairieKingDialogueKey { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_dialogue_response_key")]
    public string PrairieKingDialogueResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_completed_before")]
    public long? PrairieKingCompletedBefore { get; set; }

    [JsonPropertyName("prairie_king_completed_without_dying_before")]
    public long? PrairieKingCompletedWithoutDyingBefore { get; set; }

    [JsonPropertyName("prairie_king_completion_goal")]
    public string PrairieKingCompletionGoal { get; set; } = string.Empty;

    [JsonPropertyName("prairie_king_equivalent_duration_ticks")]
    public int? PrairieKingEquivalentDurationTicks { get; set; }

    [JsonPropertyName("prairie_king_equivalent_acceleration")]
    public int? PrairieKingEquivalentAcceleration { get; set; }

    [JsonPropertyName("prairie_king_equivalent_contract")]
    public string PrairieKingEquivalentContract { get; set; } = string.Empty;
}
