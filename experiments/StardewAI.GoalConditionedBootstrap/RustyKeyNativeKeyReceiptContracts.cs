using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class RustyKeyNativeKeyReceiptRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "rusty_key_native_key_receipt_request.v1";
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;
    [JsonPropertyName("action_queue_path")]
    public string ActionQueuePath { get; set; } = string.Empty;
    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;
    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;
}

public sealed record RustyKeyNativeKeyReceipt(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("bound_candidate_id")] string BoundCandidateId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("bridge_version")] string BridgeVersion,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick)
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "rusty_key_native_key_receipt.v1";
    [JsonPropertyName("direction_id")]
    public string DirectionId => "obtain_rusty_key";
    [JsonPropertyName("native_terminal_outcome_verified")]
    public bool NativeTerminalOutcomeVerified => true;
    [JsonPropertyName("teacher_comparison_verified")]
    public bool TeacherComparisonVerified => false;
    [JsonPropertyName("full_criterion_verified")]
    public bool FullCriterionVerified => false;
    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized => false;
    [JsonPropertyName("scope")]
    public string Scope => "criterion_local_native_key_terminal_only_not_threshold_supply_or_teacher_comparison";
}
