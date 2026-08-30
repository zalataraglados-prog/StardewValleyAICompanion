using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplySlotsRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.play_slots" or "debug.setup_slots"))
            return;
        request.SlotsProjectionFingerprint = ReadQueueParameterString(item, "slots_projection_fingerprint");
        request.SlotsActionRaw = ReadQueueParameterString(item, "slots_action_raw");
        request.SlotsActionToken = ReadQueueParameterString(item, "slots_action_token");
        request.SlotsBet = ReadQueueParameterInt(item, "slots_bet");
        request.SlotsClubCoinsBefore = ReadQueueParameterInt(item, "slots_club_coins_before");
        request.SlotsTargetClubCoins = ReadQueueParameterInt(item, "slots_target_club_coins");
        request.SlotsRemainingClubCoinDemand = ReadQueueParameterInt(item, "slots_remaining_club_coin_demand");
        request.SlotsTargetItemId = ReadQueueParameterString(item, "slots_target_item_id");
        request.SlotsTimesPlayedBefore = ReadQueueParameterInt(item, "slots_times_played_before");
        request.SlotsDailyLuck = ReadQueueParameterDouble(item, "slots_daily_luck");
        request.SlotsLuckLevel = ReadQueueParameterInt(item, "slots_luck_level");
        request.SlotsLuckMultiplier = ReadQueueParameterDouble(item, "slots_luck_multiplier");
        request.SlotsExpectedPayoutMultiplier = ReadQueueParameterDouble(item, "slots_expected_payout_multiplier");
        request.SlotsExpectedNetCoinDelta = ReadQueueParameterDouble(item, "slots_expected_net_coin_delta");
        request.SlotsPayoutRowsJson = ReadQueueParameterString(item, "slots_payout_rows_json");
        request.SlotsRngContract = ReadQueueParameterString(item, "slots_rng_contract");
        request.SlotsExitPolicy = ReadQueueParameterString(item, "slots_exit_policy");
        request.SlotsFixtureCase = ReadQueueParameterString(item, "slots_fixture_case");
    }
}
