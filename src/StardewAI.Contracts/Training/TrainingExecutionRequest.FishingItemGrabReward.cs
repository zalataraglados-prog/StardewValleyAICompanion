using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fishing_reward_menu_source")]
    public int? FishingRewardMenuSource { get; set; }

    [JsonPropertyName("fishing_reward_menu_context_type")]
    public string FishingRewardMenuContextType { get; set; } = string.Empty;

    [JsonPropertyName("fishing_reward_menu_slot_index")]
    public int? FishingRewardMenuSlotIndex { get; set; }

    [JsonPropertyName("fishing_reward_qualified_item_id")]
    public string FishingRewardQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("fishing_reward_stack_before")]
    public int? FishingRewardStackBefore { get; set; }

    [JsonPropertyName("fishing_reward_quality")]
    public int? FishingRewardQuality { get; set; }

    [JsonPropertyName("fishing_reward_pending_count_before")]
    public int? FishingRewardPendingCountBefore { get; set; }

    [JsonPropertyName("fishing_reward_requires_discard")]
    public bool? FishingRewardRequiresDiscard { get; set; }

    [JsonPropertyName("fishing_reward_discard_slot_index")]
    public int? FishingRewardDiscardSlotIndex { get; set; }

    [JsonPropertyName("fishing_reward_discard_qualified_item_id")]
    public string FishingRewardDiscardQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("fishing_reward_discard_stack_before")]
    public int? FishingRewardDiscardStackBefore { get; set; }

    [JsonPropertyName("fishing_reward_discard_quality")]
    public int? FishingRewardDiscardQuality { get; set; }

    [JsonPropertyName("fishing_reward_discard_authorization")]
    public string FishingRewardDiscardAuthorization { get; set; } = string.Empty;
}
