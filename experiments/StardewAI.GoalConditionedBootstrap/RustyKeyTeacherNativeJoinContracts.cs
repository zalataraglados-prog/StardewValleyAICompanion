using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record RustyKeyTeacherNativeJoinRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "rusty_key_teacher_native_join_request.v1";
    [JsonPropertyName("comparison_path")] public string ComparisonPath { get; init; } = string.Empty;
    [JsonPropertyName("native_key_receipt_path")] public string NativeKeyReceiptPath { get; init; } = string.Empty;
    [JsonPropertyName("actions")] public RustyKeyNativeActionSource[] Actions { get; init; } = Array.Empty<RustyKeyNativeActionSource>();
}

public sealed record RustyKeyNativeActionSource(
    [property: JsonPropertyName("action_queue_path")] string ActionQueuePath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath);

public sealed record RustyKeyTeacherNativeJoin(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("decision_total_day")] int DecisionTotalDay,
    [property: JsonPropertyName("terminal_total_day")] int TerminalTotalDay,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("preferred_candidate_id")] string PreferredCandidateId,
    [property: JsonPropertyName("native_donation_elapsed_ticks")] long NativeDonationElapsedTicks,
    [property: JsonPropertyName("actions")] RustyKeyJoinedNativeAction[] Actions)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "rusty_key_teacher_native_join.v1";
    [JsonPropertyName("direction_id")] public string DirectionId => "obtain_rusty_key";
    [JsonPropertyName("scope")] public string Scope => "conditional_threshold_decision_to_same_run_native_key_not_full_supply_or_global_night_handoff";
    [JsonPropertyName("teacher_comparison_verified")] public bool TeacherComparisonVerified => true;
    [JsonPropertyName("native_terminal_outcome_verified")] public bool NativeTerminalOutcomeVerified => true;
    [JsonPropertyName("causal_continuation_verified")] public bool CausalContinuationVerified => true;
    [JsonPropertyName("full_criterion_verified")] public bool FullCriterionVerified => false;
    [JsonPropertyName("threshold_supply_verified")] public bool ThresholdSupplyVerified => false;
    [JsonPropertyName("global_night_handoff_verified")] public bool GlobalNightHandoffVerified => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
}

public sealed record RustyKeyJoinedNativeAction(
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("request_nonce")] string RequestNonce,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick);
