using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("crane_projection_fingerprint")]
    public string CraneProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("crane_action_raw")]
    public string CraneActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("crane_action_token")]
    public string CraneActionToken { get; set; } = string.Empty;
    [JsonPropertyName("crane_yes_response_key")]
    public string CraneYesResponseKey { get; set; } = string.Empty;
    [JsonPropertyName("crane_fee_gold")]
    public int? CraneFeeGold { get; set; }
    [JsonPropertyName("crane_money_before")]
    public int? CraneMoneyBefore { get; set; }
    [JsonPropertyName("crane_empty_slots_before")]
    public int? CraneEmptySlotsBefore { get; set; }
    [JsonPropertyName("crane_attempts")]
    public int? CraneAttempts { get; set; }
    [JsonPropertyName("crane_timer_ticks_per_attempt")]
    public int? CraneTimerTicksPerAttempt { get; set; }
    [JsonPropertyName("crane_selection_policy")]
    public string CraneSelectionPolicy { get; set; } = string.Empty;
    [JsonPropertyName("crane_exit_policy")]
    public string CraneExitPolicy { get; set; } = string.Empty;
}
