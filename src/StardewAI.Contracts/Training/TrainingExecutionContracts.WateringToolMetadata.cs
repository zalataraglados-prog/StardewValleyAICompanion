using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class ActionOriginObservationReceipt
{
    [JsonPropertyName("watering_tool_metadata_before")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWateringToolMetadataFrame? WateringToolMetadataBefore { get; set; }

    [JsonPropertyName("watering_tool_metadata_after")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWateringToolMetadataFrame? WateringToolMetadataAfter { get; set; }

    [JsonPropertyName("watering_tool_metadata_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWateringToolMetadataCall[]? WateringToolMetadataCalls { get; set; }
}

// Raw inputs/endpoints only; none of these fields certifies a predicted mutation.
public sealed class NativeWateringToolMetadataFrame
{
    [JsonPropertyName("schema_version")] public string? SchemaVersion { get; set; }
    [JsonPropertyName("game_tick")] public long? GameTick { get; set; }
    [JsonPropertyName("actor_reference_id")] public string? ActorReferenceId { get; set; }
    [JsonPropertyName("tools")] public NativeWateringToolMetadataSlot[]? Tools { get; set; }
}

public sealed class NativeWateringToolMetadataSlot
{
    [JsonPropertyName("slot_index")] public int? SlotIndex { get; set; }
    [JsonPropertyName("item_reference_id")] public string? ItemReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
    [JsonPropertyName("qualified_item_id")] public string? QualifiedItemId { get; set; }
    [JsonPropertyName("initial_parent_tile_index")] public int? InitialParentTileIndex { get; set; }
    [JsonPropertyName("current_parent_tile_index")] public int? CurrentParentTileIndex { get; set; }
    [JsonPropertyName("swing_ticker")] public int? SwingTicker { get; set; }
    [JsonPropertyName("has_been_in_inventory")] public bool? HasBeenInInventory { get; set; }
    [JsonPropertyName("water_left")] public int? WaterLeft { get; set; }
}

public sealed class NativeWateringToolMetadataCall
{
    [JsonPropertyName("ordinal")] public int? Ordinal { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("completed")] public bool? Completed { get; set; }
    [JsonPropertyName("game_tick")] public long? GameTick { get; set; }
    [JsonPropertyName("actor_reference_id")] public string? ActorReferenceId { get; set; }
    [JsonPropertyName("location_id")] public string? LocationId { get; set; }
    [JsonPropertyName("item_reference_id")] public string? ItemReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
    [JsonPropertyName("qualified_item_id")] public string? QualifiedItemId { get; set; }
    [JsonPropertyName("direction")] public int? Direction { get; set; }
    [JsonPropertyName("farmer_motion_frame")] public int? FarmerMotionFrame { get; set; }
    [JsonPropertyName("facing_direction")] public int? FacingDirection { get; set; }
    [JsonPropertyName("tool_power")] public int? ToolPower { get; set; }
    [JsonPropertyName("inventory_history_value")] public bool? InventoryHistoryValue { get; set; }
}
