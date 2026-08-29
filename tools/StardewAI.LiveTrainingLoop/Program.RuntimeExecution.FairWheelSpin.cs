using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFairWheelSpinRequestFields(TrainingExecutionRequest request, System.Text.Json.Nodes.JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.spin_fair_wheel", StringComparison.Ordinal))
            return;
        request.FairWheelProjectionFingerprint = ReadQueueParameterString(item, "fair_wheel_projection_fingerprint");
        request.FairWheelInteractionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        request.FairWheelInteractionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
        request.FairWheelStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        request.FairWheelStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        request.FairWheelFestivalScoreBefore = ReadQueueParameterInt(item, "festival_score_before");
        request.FairWheelStardropPriceStarTokens = ReadQueueParameterInt(item, "stardrop_price_star_tokens");
        request.FairWheelProjectedUnclaimedGrangeTokens = ReadQueueParameterInt(item, "projected_unclaimed_grange_tokens");
        request.FairWheelRemainingStarTokenDemand = ReadQueueParameterInt(item, "remaining_star_token_demand");
        request.FairWheelSelectedColor = ReadQueueParameterString(item, "selected_color");
        request.FairWheelWagerStarTokens = ReadQueueParameterInt(item, "wager_star_tokens");
        request.FairWheelLuckLevel = ReadQueueParameterInt(item, "luck_level");
        request.FairWheelBaseGreenWins = ReadQueueParameterInt(item, "base_green_wins");
        request.FairWheelBaseOrangeWins = ReadQueueParameterInt(item, "base_orange_wins");
        request.FairWheelBaseOutcomeCount = ReadQueueParameterInt(item, "base_outcome_count");
        request.FairWheelPrestartDurationMs = ReadQueueParameterInt(item, "prestart_duration_ms");
        request.FairWheelResultDurationMs = ReadQueueParameterInt(item, "result_duration_ms");
        request.FairWheelDialogueKey = ReadQueueParameterString(item, "dialogue_key");
        request.FairWheelResponseKey = ReadQueueParameterString(item, "response_key");
        request.FairWheelWagerPolicy = ReadQueueParameterString(item, "wager_policy");
        request.NativeContract = ReadQueueParameterString(item, "native_contract");
    }
}
