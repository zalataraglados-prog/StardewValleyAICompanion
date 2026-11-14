using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class ActionOriginObservationReceipt
{
    [JsonPropertyName("tilling_tool_metadata_before")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTillingToolMetadataContext? TillingToolMetadataBefore { get; set; }

    [JsonPropertyName("tilling_tool_metadata_after")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTillingToolMetadataContext? TillingToolMetadataAfter { get; set; }
}

// Purpose-scoped raw native inputs; not an expected outcome or a resource certificate.
public sealed class NativeTillingToolMetadataContext
{
    [JsonPropertyName("schema_version")] public string? SchemaVersion { get; set; }
    [JsonPropertyName("game_tick")] public long? GameTick { get; set; }
    [JsonPropertyName("actor_reference_id")] public string? ActorReferenceId { get; set; }
    [JsonPropertyName("hoe_reference_id")] public string? HoeReferenceId { get; set; }
    [JsonPropertyName("hoe_slot_index")] public int? HoeSlotIndex { get; set; }
    [JsonPropertyName("last_equipped_tool_is_null")] public bool? LastEquippedToolIsNull { get; set; }
    [JsonPropertyName("last_equipped_tool_reference_id")] public string? LastEquippedToolReferenceId { get; set; }
    [JsonPropertyName("last_equipped_tool_runtime_type")] public string? LastEquippedToolRuntimeType { get; set; }
    [JsonPropertyName("facing_direction")] public int? FacingDirection { get; set; }
    [JsonPropertyName("current_tool_slot_index")] public int? CurrentToolSlotIndex { get; set; }
    [JsonPropertyName("tool_power")] public int? ToolPower { get; set; }
    [JsonPropertyName("using_tool")] public bool? UsingTool { get; set; }
    [JsonPropertyName("pause_for_single_animation")] public bool? PauseForSingleAnimation { get; set; }
    [JsonPropertyName("can_release_tool")] public bool? CanReleaseTool { get; set; }
    [JsonPropertyName("initial_parent_tile_index")] public int? InitialParentTileIndex { get; set; }
    [JsonPropertyName("current_parent_tile_index")] public int? CurrentParentTileIndex { get; set; }
    [JsonPropertyName("swing_ticker")] public int? SwingTicker { get; set; }
    [JsonPropertyName("has_been_in_inventory")] public bool? HasBeenInInventory { get; set; }
}
