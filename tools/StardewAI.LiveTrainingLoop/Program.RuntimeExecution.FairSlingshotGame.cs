using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyFairSlingshotGameRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.play_fair_slingshot_game", StringComparison.Ordinal))
            return;
        request.FairSlingshotProjectionFingerprint = ReadQueueParameterString(item, "fair_slingshot_projection_fingerprint");
        request.FairSlingshotInteractionTileX = ReadQueueParameterInt(item, "interaction_tile_x");
        request.FairSlingshotInteractionTileY = ReadQueueParameterInt(item, "interaction_tile_y");
        request.FairSlingshotStandTileX = ReadQueueParameterInt(item, "stand_tile_x");
        request.FairSlingshotStandTileY = ReadQueueParameterInt(item, "stand_tile_y");
        request.FairSlingshotMoneyBefore = ReadQueueParameterInt(item, "money_before");
        request.FairSlingshotEntryFeeMoney = ReadQueueParameterInt(item, "entry_fee_money");
        request.FairSlingshotFestivalScoreBefore = ReadQueueParameterInt(item, "festival_score_before");
        request.FairSlingshotStardropPriceStarTokens = ReadQueueParameterInt(item, "stardrop_price_star_tokens");
        request.FairSlingshotProjectedUnclaimedGrangeTokens = ReadQueueParameterInt(item, "projected_unclaimed_grange_tokens");
        request.FairSlingshotRemainingStarTokenDemand = ReadQueueParameterInt(item, "remaining_star_token_demand");
        request.FairSlingshotPrestartDurationMs = ReadQueueParameterInt(item, "prestart_duration_ms");
        request.FairSlingshotGameDurationMs = ReadQueueParameterInt(item, "game_duration_ms");
        request.FairSlingshotPostGameDelayMs = ReadQueueParameterInt(item, "post_game_delay_ms");
        request.FairSlingshotResultsDurationMs = ReadQueueParameterInt(item, "results_duration_ms");
        request.FairSlingshotTargetCount = ReadQueueParameterInt(item, "target_count");
        request.FairSlingshotDialogueKey = ReadQueueParameterString(item, "dialogue_key");
        request.FairSlingshotPlayResponseKey = ReadQueueParameterString(item, "play_response_key");
        request.FairSlingshotExecutionStrategy = ReadQueueParameterString(item, "execution_strategy");
        request.NativeContract = ReadQueueParameterString(item, "native_contract");
    }
}
