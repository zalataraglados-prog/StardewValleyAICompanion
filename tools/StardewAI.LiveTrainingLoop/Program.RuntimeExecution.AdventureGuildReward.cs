using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyAdventureGuildRewardRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("rewards.claim_adventure_guild_reward" or
            "executor.claim_adventure_guild_reward" or "debug.setup_adventure_guild_reward"))
            return;
        request.AdventureGuildRewardBatchFingerprint = ReadQueueParameterString(item, "adventure_guild_reward_batch_fingerprint");
        request.AdventureGuildRewardGoalsJson = ReadQueueParameterString(item, "adventure_guild_reward_goals_json");
        request.AdventureGuildRewardPendingGoalCount = ReadQueueParameterInt(item, "adventure_guild_reward_pending_goal_count");
        request.AdventureGuildRewardItemCount = ReadQueueParameterInt(item, "adventure_guild_reward_item_count");
        request.AdventureGuildRewardDialogueCount = ReadQueueParameterInt(item, "adventure_guild_reward_dialogue_count");
        request.AdventureGuildRewardInventoryMaxItems = ReadQueueParameterInt(item, "adventure_guild_reward_inventory_max_items");
        request.AdventureGuildRewardInventoryOccupiedSlots = ReadQueueParameterInt(item, "adventure_guild_reward_inventory_occupied_slots");
        request.AdventureGuildRewardInventoryCapacitySufficient = ReadNullableBoolQueueParameter(item, "adventure_guild_reward_inventory_capacity_sufficient");
        request.AdventureGuildRewardActionTileIndex = ReadQueueParameterInt(item, "adventure_guild_reward_action_tile_index");
        request.AdventureGuildRewardFixtureCase = ReadQueueParameterString(item, "adventure_guild_reward_fixture_case");
    }
}
