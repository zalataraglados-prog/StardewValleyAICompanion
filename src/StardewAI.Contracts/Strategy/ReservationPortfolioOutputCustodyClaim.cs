using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class ReservationPortfolioOutputCustodyClaim
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;
    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }
    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
    [JsonPropertyName("consumer_option_id")]
    public string ConsumerOptionId { get; set; } = string.Empty;
}
