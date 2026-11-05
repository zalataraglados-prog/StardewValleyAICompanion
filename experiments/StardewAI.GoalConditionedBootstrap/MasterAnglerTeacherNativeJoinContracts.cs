using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerTeacherNativeJoinRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "master_angler_teacher_native_join_request.v1";
    [JsonPropertyName("teacher_row_path")] public string TeacherRowPath { get; init; } = string.Empty;
    [JsonPropertyName("native_catch_receipt_path")] public string NativeCatchReceiptPath { get; init; } = string.Empty;
}

public sealed record MasterAnglerTeacherNativeJoin(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("decision_total_day")] int DecisionTotalDay,
    [property: JsonPropertyName("trajectory_id")] string TrajectoryId,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("candidate_denominator_sha256")] string CandidateDenominatorSha256,
    [property: JsonPropertyName("strict_pair_count")] int StrictPairCount,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("request_nonce")] string RequestNonce,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "master_angler_teacher_native_join.v1";
    [JsonPropertyName("teacher_comparison_verified")] public bool TeacherComparisonVerified => true;
    [JsonPropertyName("native_collection_transition_verified")] public bool NativeCollectionTransitionVerified => true;
    [JsonPropertyName("causal_source_binding_verified")] public bool CausalSourceBindingVerified => true;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
}
