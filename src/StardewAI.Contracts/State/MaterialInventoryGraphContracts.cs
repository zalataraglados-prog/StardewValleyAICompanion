using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class MaterialInventoryGraph
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_inventory_graph.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("player_id")]
    public long PlayerId { get; set; }

    [JsonPropertyName("inventory_nodes")]
    public MaterialInventoryNode[] InventoryNodes { get; set; } = Array.Empty<MaterialInventoryNode>();

    [JsonPropertyName("access_points")]
    public MaterialInventoryAccessPoint[] AccessPoints { get; set; } = Array.Empty<MaterialInventoryAccessPoint>();

    [JsonPropertyName("workbench_links")]
    public MaterialWorkbenchLink[] WorkbenchLinks { get; set; } = Array.Empty<MaterialWorkbenchLink>();

    [JsonPropertyName("quantity_rows")]
    public MaterialQuantityRow[] QuantityRows { get; set; } = Array.Empty<MaterialQuantityRow>();

    [JsonPropertyName("physical_inventory_count")]
    public int PhysicalInventoryCount { get; set; }

    [JsonPropertyName("access_point_count")]
    public int AccessPointCount { get; set; }

    [JsonPropertyName("deduplicated_access_point_count")]
    public int DeduplicatedAccessPointCount { get; set; }
}

public sealed class MaterialInventoryNode
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("inventory_kind")]
    public string InventoryKind { get; set; } = string.Empty;

    [JsonPropertyName("supply_state")]
    public string SupplyState { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int? TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int? TileY { get; set; }

    [JsonPropertyName("owner_player_id")]
    public long OwnerPlayerId { get; set; }

    [JsonPropertyName("global_inventory_id")]
    public string GlobalInventoryId { get; set; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    [JsonPropertyName("slots")]
    public MaterialInventorySlot[] Slots { get; set; } = Array.Empty<MaterialInventorySlot>();
}

public sealed class MaterialInventorySlot
{
    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("runtime_type")]
    public string RuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("stack")]
    public int Stack { get; set; }

    [JsonPropertyName("maximum_stack_size")]
    public int MaximumStackSize { get; set; }

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("sale_price")]
    public int SalePrice { get; set; }
}

public sealed class MaterialInventoryAccessPoint
{
    [JsonPropertyName("access_point_id")]
    public string AccessPointId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("access_kind")]
    public string AccessKind { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("location_kind")]
    public string LocationKind { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int? TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int? TileY { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("special_chest_type")]
    public string SpecialChestType { get; set; } = string.Empty;

    [JsonPropertyName("locked_by_other_player")]
    public bool LockedByOtherPlayer { get; set; }
}

public sealed class MaterialWorkbenchLink
{
    [JsonPropertyName("workbench_access_point_id")]
    public string WorkbenchAccessPointId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("connected_node_ids")]
    public string[] ConnectedNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("native_container_node_ids")]
    public string[] NativeContainerNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("locked_by_other_player")]
    public bool LockedByOtherPlayer { get; set; }

    [JsonPropertyName("native_rule")]
    public string NativeRule { get; set; } = "eight_adjacent_none_or_big_chests";
}

public sealed class MaterialQuantityRow
{
    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("available_quantity")]
    public int AvailableQuantity { get; set; }

    [JsonPropertyName("ready_output_quantity")]
    public int ReadyOutputQuantity { get; set; }

    [JsonPropertyName("in_process_quantity")]
    public int InProcessQuantity { get; set; }

    [JsonPropertyName("source_slot_count")]
    public int SourceSlotCount { get; set; }
}
