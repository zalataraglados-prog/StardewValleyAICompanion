using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_observation_scope_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActionOriginObservationScopeId { get; set; }
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("action_origin_observation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginObservationReceipt? ActionOriginObservation { get; set; }
}

public sealed partial class ActionOriginObservationReceipt
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = "action_origin_observation.v1";
    [JsonPropertyName("scope_id")] public string ScopeId { get; set; } = string.Empty;
    [JsonPropertyName("capture_session_id")] public string CaptureSessionId { get; set; } = string.Empty;
    [JsonPropertyName("run_id")] public string RunId { get; set; } = string.Empty;
    [JsonPropertyName("queue_id")] public string QueueId { get; set; } = string.Empty;
    [JsonPropertyName("queue_item_id")] public string QueueItemId { get; set; } = string.Empty;
    [JsonPropertyName("request_nonce")] public string RequestNonce { get; set; } = string.Empty;
    [JsonPropertyName("before_state_hash")] public string BeforeStateHash { get; set; } = string.Empty;
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("before_game_tick")] public long BeforeGameTick { get; set; }
    [JsonPropertyName("after_game_tick")] public long AfterGameTick { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "unavailable";
    [JsonPropertyName("diagnostics")] public string[] Diagnostics { get; set; } = Array.Empty<string>();
    [JsonPropertyName("before_debris")] public ActionOriginObservedDebris[] BeforeDebris { get; set; } = Array.Empty<ActionOriginObservedDebris>();
    [JsonPropertyName("after_debris")] public ActionOriginObservedDebris[] AfterDebris { get; set; } = Array.Empty<ActionOriginObservedDebris>();
    [JsonPropertyName("inventory_before")] public ActionOriginInventoryCount[] InventoryBefore { get; set; } = Array.Empty<ActionOriginInventoryCount>();
    [JsonPropertyName("inventory_after")] public ActionOriginInventoryCount[] InventoryAfter { get; set; } = Array.Empty<ActionOriginInventoryCount>();
    [JsonPropertyName("collections")] public ActionOriginCollectionObservation[] Collections { get; set; } = Array.Empty<ActionOriginCollectionObservation>();
    [JsonPropertyName("native_outcome_verified")] public bool NativeOutcomeVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;
}

public sealed class ActionOriginObservedDebris
{
    [JsonPropertyName("debris_id")] public string DebrisId { get; set; } = string.Empty;
    [JsonPropertyName("runtime_type")] public string RuntimeType { get; set; } = string.Empty;
    [JsonPropertyName("debris_type")] public string DebrisType { get; set; } = string.Empty;
    [JsonPropertyName("chunk_type")] public int ChunkType { get; set; }
    [JsonPropertyName("raw_item_id")] public string RawItemId { get; set; } = string.Empty;
    [JsonPropertyName("item_quality")] public int ItemQuality { get; set; }
    [JsonPropertyName("held_item_stack")] public int? HeldItemStack { get; set; }
    [JsonPropertyName("held_item")] public ActionOriginObservedItem? HeldItem { get; set; }
    [JsonPropertyName("chunk_ids")] public string[] ChunkIds { get; set; } = Array.Empty<string>();
}

public sealed class ActionOriginObservedItem
{
    [JsonPropertyName("runtime_type")] public string RuntimeType { get; set; } = string.Empty;
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("quality")] public int Quality { get; set; }
    [JsonPropertyName("serialized_state_xml")] public string SerializedStateXml { get; set; } = string.Empty;
    [JsonPropertyName("serialized_state_sha256")] public string SerializedStateSha256 { get; set; } = string.Empty;
}

public sealed class ActionOriginInventoryCount
{
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("quality")] public int Quality { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
}

public sealed class ActionOriginCollectionObservation
{
    [JsonPropertyName("ordinal")] public int Ordinal { get; set; }
    [JsonPropertyName("debris_id")] public string DebrisId { get; set; } = string.Empty;
    [JsonPropertyName("chunk_id")] public string? ChunkId { get; set; }
    [JsonPropertyName("before_game_tick")] public long BeforeGameTick { get; set; }
    [JsonPropertyName("after_game_tick")] public long AfterGameTick { get; set; }
    [JsonPropertyName("debris_before")] public ActionOriginObservedDebris? DebrisBefore { get; set; }
    [JsonPropertyName("native_return")] public bool? NativeReturn { get; set; }
    [JsonPropertyName("inventory_admissions")] public ActionOriginInventoryAdmission[] InventoryAdmissions { get; set; } = Array.Empty<ActionOriginInventoryAdmission>();
}

public sealed class ActionOriginInventoryAdmission
{
    [JsonPropertyName("item")] public ActionOriginObservedItem? Item { get; set; }
    [JsonPropertyName("offered_quantity")] public int OfferedQuantity { get; set; }
    [JsonPropertyName("item_stack_after_call")] public int ItemStackAfterCall { get; set; }
    [JsonPropertyName("inventory_before")] public int InventoryBefore { get; set; }
    [JsonPropertyName("inventory_after")] public int InventoryAfter { get; set; }
    [JsonPropertyName("native_return")] public bool? NativeReturn { get; set; }
}
