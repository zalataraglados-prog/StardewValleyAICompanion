using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_inventory_payload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginInventoryPayload { get; set; }
}

public sealed partial class ActionOriginObservationReceipt
{
    [JsonPropertyName("inventory_payload_before")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginInventoryPayload? InventoryPayloadBefore { get; set; }

    [JsonPropertyName("inventory_payload_after")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginInventoryPayload? InventoryPayloadAfter { get; set; }
}

public sealed partial class ActionOriginInventoryPayload
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("payload_kind")] public string PayloadKind { get; set; } = string.Empty;
    [JsonPropertyName("actor_id")] public string ActorId { get; set; } = string.Empty;
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("game_tick")] public long? GameTick { get; set; }
    [JsonPropertyName("selection_observed")] public bool? SelectionObserved { get; set; }
    [JsonPropertyName("selected_slot_index")] public int? SelectedSlotIndex { get; set; }
    [JsonPropertyName("selected_item_reference_id")] public string? SelectedItemReferenceId { get; set; }
    [JsonPropertyName("current_tool_reference_id")] public string? CurrentToolReferenceId { get; set; }
    [JsonPropertyName("slots")] public ActionOriginInventoryPayloadSlot[]? Slots { get; set; }
    [JsonPropertyName("full_transient_state_verified")] public bool FullTransientStateVerified => false;
}

public sealed partial class ActionOriginInventoryPayloadSlot
{
    [JsonPropertyName("slot_index")] public int? SlotIndex { get; set; }
    [JsonPropertyName("is_empty")] public bool? IsEmpty { get; set; }
    [JsonPropertyName("item_reference_id")] public string? ItemReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
    [JsonPropertyName("qualified_item_id")] public string? QualifiedItemId { get; set; }
    [JsonPropertyName("is_tool")] public bool? IsTool { get; set; }
    [JsonPropertyName("quality")] public int? Quality { get; set; }
    [JsonPropertyName("stack")] public int? Stack { get; set; }
    [JsonPropertyName("serialized_state_xml")] public string? SerializedStateXml { get; set; }
    [JsonPropertyName("serialized_state_sha256")] public string? SerializedStateSha256 { get; set; }
}
