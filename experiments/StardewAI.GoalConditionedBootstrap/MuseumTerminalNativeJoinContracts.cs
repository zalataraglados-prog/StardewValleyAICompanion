using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MuseumTerminalNativeJoinRequest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("comparison_path")] string ComparisonPath,
    [property: JsonPropertyName("action_queue_path")] string ActionQueuePath,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("preferred_candidate_id")] string PreferredCandidateId);

public sealed record MuseumTerminalJoinedItem(
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("donated_before")] bool DonatedBefore,
    [property: JsonPropertyName("donated_after")] bool DonatedAfter);

public sealed record MuseumTerminalJoinedAction(
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("request_nonce")] string RequestNonce,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("before_game_tick")] long BeforeGameTick,
    [property: JsonPropertyName("after_game_tick")] long AfterGameTick,
    [property: JsonPropertyName("actual_ticks")] int? ActualTicks);

public sealed record MuseumTerminalNativeJoin(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("decision_total_day")] int DecisionTotalDay,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("preferred_candidate_id")] string PreferredCandidateId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("inventory_slot_index")] int InventorySlotIndex,
    [property: JsonPropertyName("stack_before")] int StackBefore,
    [property: JsonPropertyName("stack_after")] int StackAfter,
    [property: JsonPropertyName("original_candidate_count")] int OriginalCandidateCount,
    [property: JsonPropertyName("pairwise_preferences")] CurrentCollectionTeacherPairwisePreference[] PairwisePreferences,
    [property: JsonPropertyName("items")] MuseumTerminalJoinedItem[] Items,
    [property: JsonPropertyName("action")] MuseumTerminalJoinedAction Action)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "museum_terminal_native_join.v1";
    [JsonPropertyName("direction_id")] public string DirectionId => "complete_museum_collection";
    [JsonPropertyName("criterion_id")] public string CriterionId => "achievement_complete_collection";
    [JsonPropertyName("scope")] public string Scope => "conditional_held_final_donation_to_same_frame_native_full95_and_achievement5";
    [JsonPropertyName("native_donation_elapsed_ticks")] public long NativeDonationElapsedTicks => Action.AfterGameTick - Action.BeforeGameTick;
    [JsonPropertyName("teacher_comparison_verified")] public bool TeacherComparisonVerified => true;
    [JsonPropertyName("native_terminal_outcome_verified")] public bool NativeTerminalOutcomeVerified => true;
    [JsonPropertyName("achievement_5_false_to_true")] public bool Achievement5FalseToTrue => true;
    [JsonPropertyName("prior_acquisition_verified")] public bool PriorAcquisitionVerified => false;
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
}
