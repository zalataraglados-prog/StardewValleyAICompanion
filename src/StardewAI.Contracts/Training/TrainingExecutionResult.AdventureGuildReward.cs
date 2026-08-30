using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("adventure_guild_reward_batch_fingerprint")]
    public string AdventureGuildRewardBatchFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("adventure_guild_reward_claimed_goal_count")]
    public int? AdventureGuildRewardClaimedGoalCount { get; set; }

    [JsonPropertyName("adventure_guild_reward_collected_item_count")]
    public int? AdventureGuildRewardCollectedItemCount { get; set; }

    [JsonPropertyName("adventure_guild_reward_dialogue_click_count")]
    public int? AdventureGuildRewardDialogueClickCount { get; set; }
}
