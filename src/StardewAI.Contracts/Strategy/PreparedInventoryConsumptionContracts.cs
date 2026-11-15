using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Contracts.Strategy;

public sealed class InventoryConsumptionPreparationRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("consumer_option_id")] public string ConsumerOptionId { get; set; } = string.Empty;
    [JsonPropertyName("route_source_decision_id")] public string RouteSourceDecisionId { get; set; } = string.Empty;
    [JsonPropertyName("recipe_name")] public string RecipeName { get; set; } = string.Empty;
    [JsonPropertyName("cooking_source_id")] public string CookingSourceId { get; set; } = string.Empty;
}

// Actual free-unit consumption, not consumption or relocation of another goal's reservation.
public sealed class InventorySlotConsumption
{
    [JsonPropertyName("source_node_id")] public string NodeId { get; set; } = string.Empty;
    [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("amount")] public int Quantity { get; set; }

    public InventorySlotConsumption() { }
    public InventorySlotConsumption(string nodeId, int slotIndex, string qualifiedItemId, int quantity)
    { NodeId = nodeId; SlotIndex = slotIndex; QualifiedItemId = qualifiedItemId; Quantity = quantity; }
}

public sealed class InventoryConsumptionProjection
{
    [JsonPropertyName("status")] public string Status { get; set; } = "blocked";
    [JsonPropertyName("blocking_reasons")] public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    [JsonPropertyName("native_slot_transitions")] public MaterialTransferNativeSlotTransition[] NativeSlotTransitions { get; set; } = Array.Empty<MaterialTransferNativeSlotTransition>();
    [JsonPropertyName("passive_reservation_transitions")] public MaterialTransferReservationTransition[] PassiveReservationTransitions { get; set; } = Array.Empty<MaterialTransferReservationTransition>();
}

public sealed class PreparedInventoryConsumption
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("ledger_id")] public string LedgerId { get; set; } = string.Empty;
    [JsonPropertyName("save_id")] public string SaveId { get; set; } = string.Empty;
    [JsonPropertyName("player_id")] public string PlayerId { get; set; } = string.Empty;
    [JsonPropertyName("portfolio_id")] public string PortfolioId { get; set; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("route_source_decision_id")] public string RouteSourceDecisionId { get; set; } = string.Empty;
    [JsonPropertyName("source_state_hash")] public string SourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("source_game_tick")] public long SourceGameTick { get; set; }
    [JsonPropertyName("expected_ledger_revision")] public int ExpectedLedgerRevision { get; set; }
    [JsonPropertyName("material_reservations_sha256")] public string MaterialReservationsSha256 { get; set; } = string.Empty;
    [JsonPropertyName("intent")] public InventoryConsumptionPreparationRequest Intent { get; set; } = new();
    [JsonPropertyName("source_recipe")] public JsonElement SourceRecipe { get; set; }
    [JsonPropertyName("consumptions")] public InventorySlotConsumption[] Consumptions { get; set; } = Array.Empty<InventorySlotConsumption>();
    [JsonPropertyName("before_inventory_graph")] public MaterialInventoryGraph BeforeInventoryGraph { get; set; } = new();
    [JsonPropertyName("projection")] public InventoryConsumptionProjection Projection { get; set; } = new();
    [JsonPropertyName("binding_sha256")] public string BindingSha256 { get; set; } = string.Empty;
}
