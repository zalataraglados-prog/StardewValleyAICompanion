using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_inventory_transients")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginInventoryTransients { get; set; }
}

public sealed partial class ActionOriginInventoryPayload
{
    [JsonPropertyName("selection_transient_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginSelectionTransientState? SelectionTransientState { get; set; }
}

public sealed partial class ActionOriginInventoryPayloadSlot
{
    [JsonPropertyName("transient_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginItemTransientState? TransientState { get; set; }
}

// A missing wrapper is unavailable; an observed null and an observed empty string differ.
public sealed class ActionOriginNullableText
{
    [JsonPropertyName("is_null")] public bool? IsNull { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}

public sealed class ActionOriginItemTransientState
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("set_flag_on_pickup")] public ActionOriginNullableText? SetFlagOnPickup { get; set; }
    [JsonPropertyName("temp_data_present")] public bool? TempDataPresent { get; set; }
    [JsonPropertyName("temp_data_entries")] public ActionOriginTempDataEntry[]? TempDataEntries { get; set; }
    [JsonPropertyName("cache")] public ActionOriginItemCacheState? Cache { get; set; }
    [JsonPropertyName("tool")] public ActionOriginToolTransientState? Tool { get; set; }
    [JsonPropertyName("pickaxe")] public ActionOriginPickaxeTransientState? Pickaxe { get; set; }
    [JsonPropertyName("object")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginObjectTransientState? Object { get; set; }
}

public sealed class ActionOriginTempDataEntry
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("boolean_value")] public bool? BooleanValue { get; set; }
    [JsonPropertyName("string_value")] public string? StringValue { get; set; }
}

public sealed class ActionOriginItemCacheState
{
    [JsonPropertyName("qualified_item_id")] public ActionOriginNullableText? QualifiedItemId { get; set; }
    [JsonPropertyName("context_tags_dirty")] public bool? ContextTagsDirty { get; set; }
    [JsonPropertyName("context_tags_present")] public bool? ContextTagsPresent { get; set; }
    [JsonPropertyName("context_tags")] public string?[]? ContextTags { get; set; }
}

public sealed class ActionOriginNativeActorReference
{
    [JsonPropertyName("is_null")] public bool? IsNull { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("actor_id")] public string? ActorId { get; set; }
    [JsonPropertyName("is_actor")] public bool? IsActor { get; set; }
}

public sealed class ActionOriginToolTransientState
{
    [JsonPropertyName("last_user")] public ActionOriginNativeActorReference? LastUser { get; set; }
    [JsonPropertyName("play_use_sounds")] public bool? PlayUseSounds { get; set; }
    [JsonPropertyName("description")] public ActionOriginNullableText? Description { get; set; }
    [JsonPropertyName("display_name")] public ActionOriginNullableText? DisplayName { get; set; }
}

public sealed class ActionOriginPickaxeTransientState
{
    [JsonPropertyName("boulder_tile_x")] public int? BoulderTileX { get; set; }
    [JsonPropertyName("boulder_tile_y")] public int? BoulderTileY { get; set; }
    [JsonPropertyName("hits_to_boulder")] public int? HitsToBoulder { get; set; }
}

public sealed class ActionOriginNativeItemReference
{
    [JsonPropertyName("is_null")] public bool? IsNull { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
    [JsonPropertyName("is_tool")] public bool? IsTool { get; set; }
}

public sealed class ActionOriginSelectionTransientState
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("actor_reference_id")] public string? ActorReferenceId { get; set; }
    [JsonPropertyName("item_stowed")] public bool? ItemStowed { get; set; }
    [JsonPropertyName("temporary_item")] public ActionOriginNativeItemReference? TemporaryItem { get; set; }
    [JsonPropertyName("current_item")] public ActionOriginNativeItemReference? CurrentItem { get; set; }
}
