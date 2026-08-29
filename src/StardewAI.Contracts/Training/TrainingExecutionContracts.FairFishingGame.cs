using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fair_fishing_projection_fingerprint")]
    public string FairFishingProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("fair_fishing_interaction_tile_x")]
    public int? FairFishingInteractionTileX { get; set; }

    [JsonPropertyName("fair_fishing_interaction_tile_y")]
    public int? FairFishingInteractionTileY { get; set; }

    [JsonPropertyName("fair_fishing_stand_tile_x")]
    public int? FairFishingStandTileX { get; set; }

    [JsonPropertyName("fair_fishing_stand_tile_y")]
    public int? FairFishingStandTileY { get; set; }

    [JsonPropertyName("fair_fishing_money_before")]
    public int? FairFishingMoneyBefore { get; set; }

    [JsonPropertyName("fair_fishing_entry_fee_money")]
    public int? FairFishingEntryFeeMoney { get; set; }

    [JsonPropertyName("fair_fishing_festival_score_before")]
    public int? FairFishingFestivalScoreBefore { get; set; }

    [JsonPropertyName("fair_fishing_stardrop_price_star_tokens")]
    public int? FairFishingStardropPriceStarTokens { get; set; }

    [JsonPropertyName("fair_fishing_projected_unclaimed_grange_tokens")]
    public int? FairFishingProjectedUnclaimedGrangeTokens { get; set; }

    [JsonPropertyName("fair_fishing_remaining_star_token_demand")]
    public int? FairFishingRemainingStarTokenDemand { get; set; }

    [JsonPropertyName("fair_fishing_game_duration_ms")]
    public int? FairFishingGameDurationMs { get; set; }

    [JsonPropertyName("fair_fishing_results_duration_ms")]
    public int? FairFishingResultsDurationMs { get; set; }

    [JsonPropertyName("fair_fishing_dialogue_key")]
    public string FairFishingDialogueKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_fishing_play_response_key")]
    public string FairFishingPlayResponseKey { get; set; } = string.Empty;

    [JsonPropertyName("fair_fishing_execution_strategy")]
    public string FairFishingExecutionStrategy { get; set; } = string.Empty;
}
