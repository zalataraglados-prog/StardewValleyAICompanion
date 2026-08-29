using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fair_wheel_projection_fingerprint")]
    public string FairWheelProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("fair_wheel_interaction_tile_x")]
    public int? FairWheelInteractionTileX { get; set; }

    [JsonPropertyName("fair_wheel_interaction_tile_y")]
    public int? FairWheelInteractionTileY { get; set; }

    [JsonPropertyName("fair_wheel_stand_tile_x")]
    public int? FairWheelStandTileX { get; set; }

    [JsonPropertyName("fair_wheel_stand_tile_y")]
    public int? FairWheelStandTileY { get; set; }

    [JsonPropertyName("fair_wheel_festival_score_before")]
    public int? FairWheelFestivalScoreBefore { get; set; }

    [JsonPropertyName("fair_wheel_stardrop_price_star_tokens")]
    public int? FairWheelStardropPriceStarTokens { get; set; }

    [JsonPropertyName("fair_wheel_projected_unclaimed_grange_tokens")]
    public int? FairWheelProjectedUnclaimedGrangeTokens { get; set; }

    [JsonPropertyName("fair_wheel_remaining_star_token_demand")]
    public int? FairWheelRemainingStarTokenDemand { get; set; }

    [JsonPropertyName("fair_wheel_selected_color")]
    public string FairWheelSelectedColor { get; set; } = string.Empty;

    [JsonPropertyName("fair_wheel_wager_star_tokens")]
    public int? FairWheelWagerStarTokens { get; set; }

    [JsonPropertyName("fair_wheel_luck_level")]
    public int? FairWheelLuckLevel { get; set; }

    [JsonPropertyName("fair_wheel_base_green_wins")]
    public int? FairWheelBaseGreenWins { get; set; }

    [JsonPropertyName("fair_wheel_base_orange_wins")]
    public int? FairWheelBaseOrangeWins { get; set; }

    [JsonPropertyName("fair_wheel_base_outcome_count")]
    public int? FairWheelBaseOutcomeCount { get; set; }

    [JsonPropertyName("fair_wheel_prestart_duration_ms")]
    public int? FairWheelPrestartDurationMs { get; set; }

    [JsonPropertyName("fair_wheel_result_duration_ms")]
    public int? FairWheelResultDurationMs { get; set; }

    [JsonPropertyName("fair_wheel_dialogue_key")]
    public string FairWheelDialogueKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_wheel_response_key")]
    public string FairWheelResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_wheel_wager_policy")]
    public string FairWheelWagerPolicy { get; set; } = string.Empty;
}
