using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Execution;

public sealed class MaterialTransferReservationProjection
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_transfer_reservation_projection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("ledger_id")]
    public string LedgerId { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("reservation_transitions")]
    public MaterialTransferReservationTransition[] ReservationTransitions { get; set; } =
        Array.Empty<MaterialTransferReservationTransition>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class MaterialTransferReservationTransition
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("expected_reservation_revision")]
    public int ExpectedReservationRevision { get; set; }

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_slot_index")]
    public int SourceSlotIndex { get; set; }

    [JsonPropertyName("destination_node_id")]
    public string DestinationNodeId { get; set; } = string.Empty;

    [JsonPropertyName("destination_slot_index")]
    public int DestinationSlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
