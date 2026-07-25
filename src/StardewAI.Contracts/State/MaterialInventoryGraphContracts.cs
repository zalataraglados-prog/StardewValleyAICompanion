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

    [JsonPropertyName("default_shared_resource_policy")]
    public string DefaultSharedResourcePolicy { get; set; } = "deny_without_explicit_authorization";
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

    [JsonPropertyName("ownership_class")]
    public string OwnershipClass { get; set; } = "unknown";

    [JsonPropertyName("actor_use_authorized")]
    public bool ActorUseAuthorized { get; set; }

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

    [JsonPropertyName("root_location_id")]
    public string RootLocationId { get; set; } = string.Empty;

    [JsonPropertyName("parent_building_runtime_type")]
    public string ParentBuildingRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("location_is_player_controlled")]
    public bool LocationIsPlayerControlled { get; set; }

    [JsonPropertyName("location_is_current")]
    public bool LocationIsCurrent { get; set; }

    [JsonPropertyName("tile_x")]
    public int? TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int? TileY { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("special_chest_type")]
    public string SpecialChestType { get; set; } = string.Empty;

    [JsonPropertyName("owner_player_id")]
    public long OwnerPlayerId { get; set; }

    [JsonPropertyName("ownership_class")]
    public string OwnershipClass { get; set; } = "unknown";

    [JsonPropertyName("global_inventory_id")]
    public string GlobalInventoryId { get; set; } = string.Empty;

    [JsonPropertyName("capacity")]
    public int Capacity { get; set; }

    [JsonPropertyName("occupied_slot_count")]
    public int OccupiedSlotCount { get; set; }

    [JsonPropertyName("free_slot_count")]
    public int FreeSlotCount { get; set; }

    [JsonPropertyName("is_player_chest")]
    public bool IsPlayerChest { get; set; }

    [JsonPropertyName("is_fridge")]
    public bool IsFridge { get; set; }

    [JsonPropertyName("is_giftbox")]
    public bool IsGiftbox { get; set; }

    [JsonPropertyName("is_starter_gift")]
    public bool IsStarterGift { get; set; }

    [JsonPropertyName("giftbox_index")]
    public int GiftboxIndex { get; set; }

    [JsonPropertyName("big_craftable_sprite_index")]
    public int BigCraftableSpriteIndex { get; set; }

    [JsonPropertyName("is_synchronized")]
    public bool IsSynchronized { get; set; }

    [JsonPropertyName("drop_contents")]
    public bool DropContents { get; set; }

    [JsonPropertyName("player_choice_color_rgba")]
    public int[] PlayerChoiceColorRgba { get; set; } =
        Array.Empty<int>();

    [JsonPropertyName("tint_rgba")]
    public int[] TintRgba { get; set; } =
        Array.Empty<int>();

    [JsonPropertyName("mail_on_item_dump")]
    public string MailOnItemDump { get; set; } = string.Empty;

    [JsonPropertyName("mutex_locked")]
    public bool MutexLocked { get; set; }

    [JsonPropertyName("mutex_held_by_actor")]
    public bool MutexHeldByActor { get; set; }

    [JsonPropertyName("locked_by_other_player")]
    public bool LockedByOtherPlayer { get; set; }

    [JsonPropertyName("actor_use_authorized")]
    public bool ActorUseAuthorized { get; set; }

    [JsonPropertyName("native_hit_behavior")]
    public string NativeHitBehavior { get; set; } = "not_applicable";

    [JsonPropertyName("native_swap_status")]
    public string NativeSwapStatus { get; set; } = "not_supported";

    [JsonPropertyName("relocation_heavy_tool_slot_indices")]
    public int[] RelocationHeavyToolSlotIndices { get; set; } =
        Array.Empty<int>();

    [JsonPropertyName("relocation_kick_armed")]
    public bool RelocationKickArmed { get; set; }

    [JsonPropertyName("relocation_in_progress")]
    public bool RelocationInProgress { get; set; }

    [JsonPropertyName("relocation_kick_start_tile_x")]
    public int? RelocationKickStartTileX { get; set; }

    [JsonPropertyName("relocation_kick_start_tile_y")]
    public int? RelocationKickStartTileY { get; set; }

    [JsonPropertyName("relocation_kick_progress")]
    public float RelocationKickProgress { get; set; } = -1f;

    [JsonPropertyName("relocation_status")]
    public string RelocationStatus { get; set; } = "not_applicable";

    [JsonPropertyName("relocation_blocking_reasons")]
    public string[] RelocationBlockingReasons { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("inventory_node_reference")]
    public string InventoryNodeReference { get; set; } =
        "farm.material_inventory_graph.inventory_nodes[node_id]";
}

public sealed class StorageInfrastructureProjection
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "storage_infrastructure.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "available";

    [JsonPropertyName("scope_location_id")]
    public string ScopeLocationId { get; set; } = string.Empty;

    [JsonPropertyName("source_graph_schema_version")]
    public string SourceGraphSchemaVersion { get; set; } =
        "material_inventory_graph.v1";

    [JsonPropertyName("source_graph_player_id")]
    public long SourceGraphPlayerId { get; set; }

    [JsonPropertyName("inventory_node_reference")]
    public string InventoryNodeReference { get; set; } =
        "farm.material_inventory_graph.inventory_nodes[node_id]";

    [JsonPropertyName("access_points")]
    public MaterialInventoryAccessPoint[] AccessPoints { get; set; } =
        Array.Empty<MaterialInventoryAccessPoint>();

    [JsonPropertyName("access_point_count")]
    public int AccessPointCount { get; set; }

    [JsonPropertyName("distinct_inventory_node_count")]
    public int DistinctInventoryNodeCount { get; set; }

    [JsonPropertyName("actor_authorized_access_point_count")]
    public int ActorAuthorizedAccessPointCount { get; set; }

    [JsonPropertyName("locked_access_point_count")]
    public int LockedAccessPointCount { get; set; }

    [JsonPropertyName("removable_empty_access_point_count")]
    public int RemovableEmptyAccessPointCount { get; set; }

    [JsonPropertyName("nonempty_shove_access_point_count")]
    public int NonemptyShoveAccessPointCount { get; set; }

    [JsonPropertyName("content_duplication_policy")]
    public string ContentDuplicationPolicy { get; set; } =
        "reference_canonical_material_graph_nodes";
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

    [JsonPropertyName("restricted_quantity")]
    public int RestrictedQuantity { get; set; }

    [JsonPropertyName("source_slot_count")]
    public int SourceSlotCount { get; set; }
}
