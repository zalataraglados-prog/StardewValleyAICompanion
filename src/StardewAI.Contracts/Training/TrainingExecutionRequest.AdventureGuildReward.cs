using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("adventure_guild_reward_batch_fingerprint")]
    public string AdventureGuildRewardBatchFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("adventure_guild_reward_goals_json")]
    public string AdventureGuildRewardGoalsJson { get; set; } = string.Empty;

    [JsonPropertyName("adventure_guild_reward_pending_goal_count")]
    public int? AdventureGuildRewardPendingGoalCount { get; set; }

    [JsonPropertyName("adventure_guild_reward_item_count")]
    public int? AdventureGuildRewardItemCount { get; set; }

    [JsonPropertyName("adventure_guild_reward_dialogue_count")]
    public int? AdventureGuildRewardDialogueCount { get; set; }

    [JsonPropertyName("adventure_guild_reward_inventory_max_items")]
    public int? AdventureGuildRewardInventoryMaxItems { get; set; }

    [JsonPropertyName("adventure_guild_reward_inventory_occupied_slots")]
    public int? AdventureGuildRewardInventoryOccupiedSlots { get; set; }

    [JsonPropertyName("adventure_guild_reward_inventory_capacity_sufficient")]
    public bool? AdventureGuildRewardInventoryCapacitySufficient { get; set; }

    [JsonPropertyName("adventure_guild_reward_action_tile_index")]
    public int? AdventureGuildRewardActionTileIndex { get; set; }

    [JsonPropertyName("adventure_guild_reward_fixture_case")]
    public string AdventureGuildRewardFixtureCase { get; set; } = string.Empty;
}
