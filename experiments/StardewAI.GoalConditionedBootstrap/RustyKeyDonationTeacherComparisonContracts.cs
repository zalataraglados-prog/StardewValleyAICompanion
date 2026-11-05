using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record RustyKeyDonationTeacherComparisonRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "rusty_key_donation_teacher_comparison_request.v1";
    [JsonPropertyName("snapshot_path")] public string SnapshotPath { get; init; } = string.Empty;
    [JsonPropertyName("strategy_ledger_path")] public string StrategyLedgerPath { get; init; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; init; } = GoalMethodTeacherCoverageGoalIds.Authoritative;
}

public sealed record RustyKeyDonationTeacherComparison(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("source_sha256")] IReadOnlyDictionary<string, string> SourceSha256,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("goal_id")] string GoalId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("game_tick")] long GameTick,
    [property: JsonPropertyName("ledger_revision")] int LedgerRevision,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("candidate_evaluations")] RustyKeyDonationTeacherAlternative[] CandidateEvaluations,
    [property: JsonPropertyName("pairwise_preferences")] CurrentCollectionTeacherPairwisePreference[] PairwisePreferences)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "rusty_key_donation_teacher_comparison.v1";
    [JsonPropertyName("scope")] public string Scope => "held_threshold_donation_vs_bounded_no_donation_only";
    [JsonPropertyName("selection_policy_id")] public string SelectionPolicyId => "independent_teacher.rusty_key_threshold_progress_within_600_ticks.v1";
    [JsonPropertyName("comparison_horizon_ticks")] public int ComparisonHorizonTicks => 600;
    [JsonPropertyName("teacher_comparison_verified")] public bool TeacherComparisonVerified => PairwisePreferences.Length > 0;
    [JsonPropertyName("native_terminal_outcome_verified")] public bool NativeTerminalOutcomeVerified => false;
    [JsonPropertyName("full_criterion_verified")] public bool FullCriterionVerified => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
    [JsonPropertyName("uses_control_selection_or_native_outcome")] public bool UsesControlSelectionOrNativeOutcome => false;
}

public sealed record RustyKeyDonationTeacherAlternative(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("admitted")] bool Admitted,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons,
    [property: JsonPropertyName("projected_threshold_credit")] int ProjectedThresholdCredit,
    [property: JsonPropertyName("estimated_ticks")] int? EstimatedTicks,
    [property: JsonPropertyName("compiled_queue")] ActionQueueEnvelope? CompiledQueue);
