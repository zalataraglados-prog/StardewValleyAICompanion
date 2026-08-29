using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFairFishingGameRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.play_fair_fishing_game", StringComparison.Ordinal))
            return;
        request.FairFishingProjectionFingerprint = ReadQueueParameterString(item, "fair_fishing_projection_fingerprint");
        request.FairFishingInteractionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        request.FairFishingInteractionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
        request.FairFishingStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        request.FairFishingStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        request.FairFishingMoneyBefore = ReadQueueParameterInt(item, "money_before");
        request.FairFishingEntryFeeMoney = ReadQueueParameterInt(item, "entry_fee_money");
        request.FairFishingFestivalScoreBefore = ReadQueueParameterInt(item, "festival_score_before");
        request.FairFishingStardropPriceStarTokens = ReadQueueParameterInt(item, "stardrop_price_star_tokens");
        request.FairFishingProjectedUnclaimedGrangeTokens = ReadQueueParameterInt(item, "projected_unclaimed_grange_tokens");
        request.FairFishingRemainingStarTokenDemand = ReadQueueParameterInt(item, "remaining_star_token_demand");
        request.FairFishingGameDurationMs = ReadQueueParameterInt(item, "game_duration_ms");
        request.FairFishingResultsDurationMs = ReadQueueParameterInt(item, "results_duration_ms");
        request.FairFishingDialogueKey = ReadQueueParameterString(item, "dialogue_key");
        request.FairFishingPlayResponseKey = ReadQueueParameterString(item, "play_response_key");
        request.FairFishingExecutionStrategy = ReadQueueParameterString(item, "execution_strategy");
        request.NativeContract = ReadQueueParameterString(item, "native_contract");
    }
}
