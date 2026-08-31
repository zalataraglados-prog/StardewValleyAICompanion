using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("prize_ticket_stage")]
    public string PrizeTicketStage { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_reward_fingerprint")]
    public string PrizeTicketRewardFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("prize_ticket_inventory_count_after")]
    public int? PrizeTicketInventoryCountAfter { get; set; }

    [JsonPropertyName("prize_ticket_pending_count_after")]
    public int? PrizeTicketPendingCountAfter { get; set; }

    [JsonPropertyName("prize_ticket_claimed_count_after")]
    public int? PrizeTicketClaimedCountAfter { get; set; }

    [JsonPropertyName("prize_ticket_reward_total_delta")]
    public int? PrizeTicketRewardTotalDelta { get; set; }
}
