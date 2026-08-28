using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("feed_hopper_safe_slot_kind")]
    public string FeedHopperSafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("feed_hopper_hay_qualified_item_id")]
    public string FeedHopperHayQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("feed_hopper_root_location_id")]
    public string FeedHopperRootLocationId { get; set; } = string.Empty;

    [JsonPropertyName("feed_hopper_silo_hay_before")]
    public int? FeedHopperSiloHayBefore { get; set; }

    [JsonPropertyName("feed_hopper_animal_count")]
    public int? FeedHopperAnimalCount { get; set; }

    [JsonPropertyName("feed_hopper_animal_limit")]
    public int? FeedHopperAnimalLimit { get; set; }

    [JsonPropertyName("feed_hopper_placed_hay_count")]
    public int? FeedHopperPlacedHayCount { get; set; }

    [JsonPropertyName("feed_hopper_unfed_animal_count")]
    public int? FeedHopperUnfedAnimalCount { get; set; }

    [JsonPropertyName("feed_hopper_expected_withdrawal_quantity")]
    public int? FeedHopperExpectedWithdrawalQuantity { get; set; }

    [JsonPropertyName("feed_hopper_expected_silo_hay_after")]
    public int? FeedHopperExpectedSiloHayAfter { get; set; }

    [JsonPropertyName("feed_hopper_expected_location_action_return")]
    public bool? FeedHopperExpectedLocationActionReturn { get; set; }
}
