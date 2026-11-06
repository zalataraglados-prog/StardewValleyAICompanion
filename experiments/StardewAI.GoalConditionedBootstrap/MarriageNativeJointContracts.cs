using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MarriageNativeJointRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("steps")] MarriageNativeJointStep[] Steps,
    [property: JsonPropertyName("final_snapshot_path")] string FinalSnapshotPath);

public sealed record MarriageNativeJointStep(
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("action_queue_path")] string ActionQueuePath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("preference_path")] string? PreferencePath = null);
