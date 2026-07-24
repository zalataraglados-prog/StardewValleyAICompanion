using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class MaterialReservation
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = StrategyCommitmentStatuses.Active;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("owner_player_id")]
    public long OwnerPlayerId { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("cancel_reason")]
    public string CancelReason { get; set; } = string.Empty;
}

public sealed class MaterialReservationUpsertRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;
}

public sealed class MaterialSupplyProjectionResult
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_supply_projection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("slots")]
    public MaterialSupplySlot[] Slots { get; set; } = Array.Empty<MaterialSupplySlot>();

    [JsonPropertyName("quantities")]
    public MaterialSupplyQuantity[] Quantities { get; set; } = Array.Empty<MaterialSupplyQuantity>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("excluded_node_ids")]
    public string[] ExcludedNodeIds { get; set; } = Array.Empty<string>();
}

public sealed class MaterialSupplySlot
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("stack")]
    public int Stack { get; set; }

    [JsonPropertyName("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    [JsonPropertyName("available_quantity")]
    public int AvailableQuantity { get; set; }
}

public sealed class MaterialSupplyQuantity
{
    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("total_quantity")]
    public int TotalQuantity { get; set; }

    [JsonPropertyName("reserved_quantity")]
    public int ReservedQuantity { get; set; }

    [JsonPropertyName("available_quantity")]
    public int AvailableQuantity { get; set; }
}
