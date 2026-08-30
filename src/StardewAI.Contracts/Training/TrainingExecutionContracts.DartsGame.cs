using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("darts_projection_fingerprint")]
    public string DartsProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("darts_action_raw")]
    public string DartsActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("darts_action_token")]
    public string DartsActionToken { get; set; } = string.Empty;
    [JsonPropertyName("darts_yes_response_key")]
    public string DartsYesResponseKey { get; set; } = string.Empty;
    [JsonPropertyName("darts_limited_nut_key")]
    public string DartsLimitedNutKey { get; set; } = string.Empty;
    [JsonPropertyName("darts_limited_nut_limit")]
    public int? DartsLimitedNutLimit { get; set; }
    [JsonPropertyName("darts_limited_nut_dropped_before")]
    public int? DartsLimitedNutDroppedBefore { get; set; }
    [JsonPropertyName("darts_limited_nut_dropped_after")]
    public int? DartsLimitedNutDroppedAfter { get; set; }
    [JsonPropertyName("darts_starting_dart_count")]
    public int? DartsStartingDartCount { get; set; }
    [JsonPropertyName("darts_starting_points")]
    public int? DartsStartingPoints { get; set; }
    [JsonPropertyName("darts_perfect_victory_max_throws")]
    public int? DartsPerfectVictoryMaxThrows { get; set; }
    [JsonPropertyName("darts_perfect_score_plan")]
    public string DartsPerfectScorePlan { get; set; } = string.Empty;
    [JsonPropertyName("darts_charge_release_threshold")]
    public double? DartsChargeReleaseThreshold { get; set; }
}
