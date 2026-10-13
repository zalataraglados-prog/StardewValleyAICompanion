using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("acquisition_route_kind")]
    public string AcquisitionRouteKind { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_route_occurrence_id")]
    public string AcquisitionRouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("deferred_pickup_source_kind")]
    public string DeferredPickupSourceKind { get; set; } = string.Empty;

    [JsonPropertyName("deferred_pickup_debris_item_total_before")]
    public int? DeferredPickupDebrisItemTotalBefore { get; set; }

    [JsonPropertyName("deferred_pickup_guaranteed_minimum_quantity")]
    public int? DeferredPickupGuaranteedMinimumQuantity { get; set; }

    [JsonPropertyName("deferred_pickup_minimum_quality")]
    public int? DeferredPickupMinimumQuality { get; set; }
}
