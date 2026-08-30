using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyCalicoJackRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.play_calico_jack" or "debug.setup_calico_jack"))
            return;
        request.CalicoProjectionFingerprint = ReadQueueParameterString(item, "calico_projection_fingerprint");
        request.CalicoActionRaw = ReadQueueParameterString(item, "calico_action_raw");
        request.CalicoActionToken = ReadQueueParameterString(item, "calico_action_token");
        request.CalicoTableKind = ReadQueueParameterString(item, "calico_table_kind");
        request.CalicoBet = ReadQueueParameterInt(item, "calico_bet");
        request.CalicoDialogueKey = ReadQueueParameterString(item, "calico_dialogue_key");
        request.CalicoPlayResponseKey = ReadQueueParameterString(item, "calico_play_response_key");
        request.CalicoClubCoinsBefore = ReadQueueParameterInt(item, "calico_club_coins_before");
        request.CalicoTargetClubCoins = ReadQueueParameterInt(item, "calico_target_club_coins");
        request.CalicoRemainingClubCoinDemand = ReadQueueParameterInt(item, "calico_remaining_club_coin_demand");
        request.CalicoTargetItemId = ReadQueueParameterString(item, "calico_target_item_id");
        request.CalicoTimesPlayedSeed = ReadQueueParameterInt(item, "calico_times_played_seed");
        request.CalicoDaysPlayedSeed = ReadQueueParameterInt(item, "calico_days_played_seed");
        request.CalicoUniqueGameIdSeed = ReadQueueParameterString(item, "calico_unique_game_id_seed");
        request.CalicoDailyLuck = ReadQueueParameterDouble(item, "calico_daily_luck");
        request.CalicoLuckLevel = ReadQueueParameterInt(item, "calico_luck_level");
        request.CalicoPlayerCardsJson = ReadQueueParameterString(item, "calico_player_cards_json");
        request.CalicoDealerCardsJson = ReadQueueParameterString(item, "calico_dealer_cards_json");
        request.CalicoRecommendedFirstAction = ReadQueueParameterString(item, "calico_recommended_first_action");
        request.CalicoProjectedNextHitCard = ReadQueueParameterInt(item, "calico_projected_next_hit_card");
        request.CalicoCoinDeltaPerLowBet = ReadQueueParameterInt(item, "calico_coin_delta_per_low_bet");
        request.CalicoExpectedCoinDelta = ReadQueueParameterInt(item, "calico_expected_coin_delta");
        request.CalicoProjectedOutcome = ReadQueueParameterString(item, "calico_projected_outcome");
        request.CalicoDecisionPolicy = ReadQueueParameterString(item, "calico_decision_policy");
        request.CalicoExitPolicy = ReadQueueParameterString(item, "calico_exit_policy");
        request.CalicoFixtureCase = ReadQueueParameterString(item, "calico_fixture_case");
    }
}
