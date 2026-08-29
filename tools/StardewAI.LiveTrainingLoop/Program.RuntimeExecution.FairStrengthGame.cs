using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFairStrengthGameRequestFields(TrainingExecutionRequest request, System.Text.Json.Nodes.JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.play_fair_strength_game", StringComparison.Ordinal))
            return;
        request.FairStrengthProjectionFingerprint = ReadQueueParameterString(item, "fair_strength_projection_fingerprint");
        request.FairStrengthInteractionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        request.FairStrengthInteractionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
        request.FairStrengthStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        request.FairStrengthStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        request.FairStrengthFestivalScoreBefore = ReadQueueParameterInt(item, "festival_score_before");
        request.FairStrengthStardropPriceStarTokens = ReadQueueParameterInt(item, "stardrop_price_star_tokens");
        request.FairStrengthProjectedUnclaimedGrangeTokens = ReadQueueParameterInt(item, "projected_unclaimed_grange_tokens");
        request.FairStrengthRemainingStarTokenDemand = ReadQueueParameterInt(item, "remaining_star_token_demand");
        request.FairStrengthEntryFeeMoney = ReadQueueParameterInt(item, "entry_fee_money");
        request.FairStrengthExpectedRewardStarTokens = ReadQueueParameterInt(item, "expected_reward_star_tokens");
        request.FairStrengthPerfectPowerMinimum = ReadQueueParameterDouble(item, "perfect_power_minimum");
        request.FairStrengthPowerMaximum = ReadQueueParameterDouble(item, "power_maximum");
        request.FairStrengthRequiredPlayerTileX = ReadQueueParameterInt(item, "required_player_tile_x");
        request.FairStrengthSwingStartFrame = ReadQueueParameterInt(item, "swing_start_frame");
        request.FairStrengthSwingIntervalMs = ReadQueueParameterDouble(item, "swing_interval_ms");
        request.FairStrengthSwingFrameCount = ReadQueueParameterInt(item, "swing_frame_count");
        request.FairStrengthPerfectResultDelayMs = ReadQueueParameterDouble(item, "perfect_result_delay_ms");
        request.FairStrengthExecutionStrategy = ReadQueueParameterString(item, "execution_strategy");
        request.NativeContract = ReadQueueParameterString(item, "native_contract");
    }
}
