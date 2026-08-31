using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyMasteryClaimRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("skills.claim_mastery" or "executor.claim_mastery" or "debug.setup_mastery_claim"))
            return;
        request.MasterySkillId = ReadQueueParameterInt(item, "mastery_skill_id");
        request.MasterySkillKey = ReadQueueParameterString(item, "mastery_skill_key");
        request.MasteryProjectionFingerprint = ReadQueueParameterString(item, "mastery_projection_fingerprint");
        request.MasteryOptionFingerprint = ReadQueueParameterString(item, "mastery_option_fingerprint");
        request.MasteryExperienceBefore = ReadQueueParameterInt(item, "mastery_experience_before");
        request.MasteryLevelBefore = ReadQueueParameterInt(item, "mastery_level_before");
        request.MasteryLevelsSpentBefore = ReadQueueParameterInt(item, "mastery_levels_spent_before");
        request.MasterySkillStatBefore = ReadQueueParameterInt(item, "mastery_skill_stat_before");
        request.MasteryAllSkillStatsBeforeCsv = ReadQueueParameterString(item, "mastery_all_skill_stats_before_csv");
        request.MasteryRecipeRewardsJson = ReadQueueParameterString(item, "mastery_recipe_rewards_json");
        request.MasteryDirectRewardsJson = ReadQueueParameterString(item, "mastery_direct_rewards_json");
        request.MasteryGrantsTrinketSlot = ReadNullableBoolQueueParameter(item, "mastery_grants_trinket_slot");
        request.MasteryTrinketSlotsBefore = ReadQueueParameterInt(item, "mastery_trinket_slots_before");
        request.MasteryActionRaw = ReadQueueParameterString(item, "mastery_action_raw");
        request.MasteryFixtureCase = ReadQueueParameterString(item, "mastery_fixture_case");
    }
}
