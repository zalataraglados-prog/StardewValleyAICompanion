using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fair_strength_projection_fingerprint")]
    public string FairStrengthProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("fair_strength_interaction_tile_x")]
    public int? FairStrengthInteractionTileX { get; set; }

    [JsonPropertyName("fair_strength_interaction_tile_y")]
    public int? FairStrengthInteractionTileY { get; set; }

    [JsonPropertyName("fair_strength_stand_tile_x")]
    public int? FairStrengthStandTileX { get; set; }

    [JsonPropertyName("fair_strength_stand_tile_y")]
    public int? FairStrengthStandTileY { get; set; }

    [JsonPropertyName("fair_strength_festival_score_before")]
    public int? FairStrengthFestivalScoreBefore { get; set; }

    [JsonPropertyName("fair_strength_stardrop_price_star_tokens")]
    public int? FairStrengthStardropPriceStarTokens { get; set; }

    [JsonPropertyName("fair_strength_projected_unclaimed_grange_tokens")]
    public int? FairStrengthProjectedUnclaimedGrangeTokens { get; set; }

    [JsonPropertyName("fair_strength_remaining_star_token_demand")]
    public int? FairStrengthRemainingStarTokenDemand { get; set; }

    [JsonPropertyName("fair_strength_entry_fee_money")]
    public int? FairStrengthEntryFeeMoney { get; set; }

    [JsonPropertyName("fair_strength_expected_reward_star_tokens")]
    public int? FairStrengthExpectedRewardStarTokens { get; set; }

    [JsonPropertyName("fair_strength_perfect_power_minimum")]
    public double? FairStrengthPerfectPowerMinimum { get; set; }

    [JsonPropertyName("fair_strength_power_maximum")]
    public double? FairStrengthPowerMaximum { get; set; }

    [JsonPropertyName("fair_strength_required_player_tile_x")]
    public int? FairStrengthRequiredPlayerTileX { get; set; }

    [JsonPropertyName("fair_strength_swing_start_frame")]
    public int? FairStrengthSwingStartFrame { get; set; }

    [JsonPropertyName("fair_strength_swing_interval_ms")]
    public double? FairStrengthSwingIntervalMs { get; set; }

    [JsonPropertyName("fair_strength_swing_frame_count")]
    public int? FairStrengthSwingFrameCount { get; set; }

    [JsonPropertyName("fair_strength_perfect_result_delay_ms")]
    public double? FairStrengthPerfectResultDelayMs { get; set; }

    [JsonPropertyName("fair_strength_execution_strategy")]
    public string FairStrengthExecutionStrategy { get; set; } = string.Empty;
}
