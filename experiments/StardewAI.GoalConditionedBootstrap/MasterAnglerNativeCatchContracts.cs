using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerNativeCatchRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "master_angler_native_catch_request.v1";
    [JsonPropertyName("run_id")] public string RunId { get; init; } = string.Empty;
    [JsonPropertyName("requirement_inventory_path")] public string RequirementInventoryPath { get; init; } = string.Empty;
    [JsonPropertyName("acquisition_lowering_path")] public string AcquisitionLoweringPath { get; init; } = string.Empty;
    [JsonPropertyName("action_queue_path")] public string ActionQueuePath { get; init; } = string.Empty;
    [JsonPropertyName("execution_receipt_path")] public string ExecutionReceiptPath { get; init; } = string.Empty;
    [JsonPropertyName("after_snapshot_path")] public string AfterSnapshotPath { get; init; } = string.Empty;
}

public sealed record MasterAnglerNativeCatchReceipt(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("bridge_version")] string BridgeVersion,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("request_nonce")] string RequestNonce,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "master_angler_native_catch_receipt.v1";
    [JsonPropertyName("native_collection_transition_verified")] public bool NativeCollectionTransitionVerified => true;
    [JsonPropertyName("teacher_comparison_verified")] public bool TeacherComparisonVerified => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
}
