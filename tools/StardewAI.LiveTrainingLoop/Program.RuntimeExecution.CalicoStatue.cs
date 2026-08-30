using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyCalicoStatueRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.activate_calico_statue" or
            "mining.activate_calico_statue" or "debug.setup_calico_statue"))
        {
            return;
        }

        request.CalicoStatueProjectionFingerprint = ReadQueueParameterString(item, "calico_statue_projection_fingerprint");
        request.CalicoStatueAcceptedEffectId = ReadQueueParameterInt(item, "calico_statue_accepted_effect_id");
        request.CalicoStatueEffectKey = ReadQueueParameterString(item, "calico_statue_effect_key");
        request.CalicoStatueStrategyPolarity = ReadQueueParameterString(item, "calico_statue_strategy_polarity");
        request.CalicoStatueExactEffect = ReadQueueParameterString(item, "calico_statue_exact_effect");
        request.CalicoStatueCalicoEggReward = ReadQueueParameterInt(item, "calico_statue_calico_egg_reward");
        request.CalicoStatueCurrentEffectsCsv = ReadQueueParameterString(item, "calico_statue_current_effects_csv");
        request.CalicoStatueExpectedEffectsAfterCsv = ReadQueueParameterString(item, "calico_statue_expected_effects_after_csv");
        request.CalicoStatueTotalActivatedBefore = ReadQueueParameterInt(item, "calico_statue_total_activated_before");
        request.CalicoStatueNextActivationNumber = ReadQueueParameterInt(item, "calico_statue_next_activation_number");
        request.CalicoStatueRatingBefore = ReadQueueParameterInt(item, "calico_statue_rating_before");
        request.CalicoStatueExpectedRatingAfter = ReadQueueParameterInt(item, "calico_statue_expected_rating_after");
        request.CalicoStatueAverageDailyLuck = ReadQueueParameterDouble(item, "calico_statue_average_daily_luck");
        request.CalicoStatueDaysPlayed = ReadQueueParameterInt(item, "calico_statue_days_played");
        request.CalicoStatueUniqueGameIdHalf = ReadQueueParameterString(item, "calico_statue_unique_game_id_half");
        request.CalicoStatueUseLegacyRandom = ReadQueueParameterBool(item, "calico_statue_use_legacy_random");
        request.CalicoStatueMineLevel = ReadQueueParameterInt(item, "calico_statue_mine_level");
        request.CalicoStatueFestivalDay = ReadQueueParameterInt(item, "calico_statue_festival_day");
        request.CalicoStatueTileIndexBefore = ReadQueueParameterInt(item, "calico_statue_tile_index_before");
        request.CalicoStatueTileIndexAfter = ReadQueueParameterInt(item, "calico_statue_tile_index_after");
        request.CalicoStatueEggsBefore = ReadQueueParameterInt(item, "calico_statue_eggs_before");
        request.CalicoStatueHealthBefore = ReadQueueParameterInt(item, "calico_statue_health_before");
        request.CalicoStatueMaxHealth = ReadQueueParameterInt(item, "calico_statue_max_health");
        request.CalicoStatueStaminaBefore = ReadQueueParameterDouble(item, "calico_statue_stamina_before");
        request.CalicoStatueMaxStamina = ReadQueueParameterDouble(item, "calico_statue_max_stamina");
        request.CalicoStatueFixtureEffectId = ReadQueueParameterInt(item, "calico_statue_fixture_effect_id");
    }
}
