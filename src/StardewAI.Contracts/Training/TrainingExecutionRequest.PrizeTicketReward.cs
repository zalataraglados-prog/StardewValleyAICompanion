using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("prize_ticket_stage")]
    public string PrizeTicketStage { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_projection_fingerprint")]
    public string PrizeTicketProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_current_reward_fingerprint")]
    public string PrizeTicketCurrentRewardFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_preview_json")]
    public string PrizeTicketPreviewJson { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_inventory_count_before")]
    public int? PrizeTicketInventoryCountBefore { get; set; }

    [JsonPropertyName("prize_ticket_pending_count_before")]
    public int? PrizeTicketPendingCountBefore { get; set; }

    [JsonPropertyName("prize_ticket_claimed_count_before")]
    public int? PrizeTicketClaimedCountBefore { get; set; }

    [JsonPropertyName("prize_ticket_prize_level")]
    public int? PrizeTicketPrizeLevel { get; set; }

    [JsonPropertyName("prize_ticket_reward_qualified_item_id")]
    public string PrizeTicketRewardQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_reward_item_id")]
    public string PrizeTicketRewardItemId { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_reward_stack")]
    public int? PrizeTicketRewardStack { get; set; }

    [JsonPropertyName("prize_ticket_reward_quality")]
    public int? PrizeTicketRewardQuality { get; set; }

    [JsonPropertyName("prize_ticket_reward_runtime_type")]
    public string PrizeTicketRewardRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_inventory_max_items")]
    public int? PrizeTicketInventoryMaxItems { get; set; }

    [JsonPropertyName("prize_ticket_inventory_occupied_slots")]
    public int? PrizeTicketInventoryOccupiedSlots { get; set; }

    [JsonPropertyName("prize_ticket_pending_capacity_sufficient")]
    public bool? PrizeTicketPendingCapacitySufficient { get; set; }

    [JsonPropertyName("prize_ticket_action_raw")]
    public string PrizeTicketActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_fixture_case")]
    public string PrizeTicketFixtureCase { get; set; } = string.Empty;
}
