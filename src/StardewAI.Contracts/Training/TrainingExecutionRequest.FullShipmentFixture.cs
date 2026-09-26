using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("full_shipment_expected_eligible_item_count")]
    public int? FullShipmentExpectedEligibleItemCount { get; set; }
}
