using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fair_slingshot_projection_fingerprint")]
    public string FairSlingshotProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("fair_slingshot_interaction_tile_x")]
    public int? FairSlingshotInteractionTileX { get; set; }

    [JsonPropertyName("fair_slingshot_interaction_tile_y")]
    public int? FairSlingshotInteractionTileY { get; set; }

    [JsonPropertyName("fair_slingshot_stand_tile_x")]
    public int? FairSlingshotStandTileX { get; set; }

    [JsonPropertyName("fair_slingshot_stand_tile_y")]
    public int? FairSlingshotStandTileY { get; set; }

    [JsonPropertyName("fair_slingshot_money_before")]
    public int? FairSlingshotMoneyBefore { get; set; }

    [JsonPropertyName("fair_slingshot_entry_fee_money")]
    public int? FairSlingshotEntryFeeMoney { get; set; }

    [JsonPropertyName("fair_slingshot_festival_score_before")]
    public int? FairSlingshotFestivalScoreBefore { get; set; }

    [JsonPropertyName("fair_slingshot_stardrop_price_star_tokens")]
    public int? FairSlingshotStardropPriceStarTokens { get; set; }

    [JsonPropertyName("fair_slingshot_projected_unclaimed_grange_tokens")]
    public int? FairSlingshotProjectedUnclaimedGrangeTokens { get; set; }

    [JsonPropertyName("fair_slingshot_remaining_star_token_demand")]
    public int? FairSlingshotRemainingStarTokenDemand { get; set; }

    [JsonPropertyName("fair_slingshot_prestart_duration_ms")]
    public int? FairSlingshotPrestartDurationMs { get; set; }

    [JsonPropertyName("fair_slingshot_game_duration_ms")]
    public int? FairSlingshotGameDurationMs { get; set; }

    [JsonPropertyName("fair_slingshot_post_game_delay_ms")]
    public int? FairSlingshotPostGameDelayMs { get; set; }

    [JsonPropertyName("fair_slingshot_results_duration_ms")]
    public int? FairSlingshotResultsDurationMs { get; set; }

    [JsonPropertyName("fair_slingshot_target_count")]
    public int? FairSlingshotTargetCount { get; set; }

    [JsonPropertyName("fair_slingshot_dialogue_key")]
    public string FairSlingshotDialogueKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_slingshot_play_response_key")]
    public string FairSlingshotPlayResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_slingshot_execution_strategy")]
    public string FairSlingshotExecutionStrategy { get; set; } = string.Empty;
}
