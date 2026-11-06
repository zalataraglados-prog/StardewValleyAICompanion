using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MuseumTerminalTeacherComparisonRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "museum_terminal_teacher_comparison_request.v1";
    [JsonPropertyName("intake_path")] public string IntakePath { get; init; } = string.Empty;
    [JsonPropertyName("strategy_ledger_path")] public string StrategyLedgerPath { get; init; } = string.Empty;
    [JsonPropertyName("goal_id")] public string GoalId { get; init; } = GoalMethodTeacherCoverageGoalIds.Authoritative;
}

public sealed record MuseumTerminalTeacherAlternative(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("admitted")] bool Admitted,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons,
    [property: JsonPropertyName("projected_new_achievement_credit")] int ProjectedNewAchievementCredit,
    [property: JsonPropertyName("estimated_ticks")] int? EstimatedTicks,
    [property: JsonPropertyName("compiled_plan")] SmallModelPlanEnvelope? CompiledPlan,
    [property: JsonPropertyName("compiled_queue")] ActionQueueEnvelope? CompiledQueue);

public sealed record MuseumTerminalTeacherComparison(
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
    [property: JsonPropertyName("candidate_evaluations")] MuseumTerminalTeacherAlternative[] CandidateEvaluations,
    [property: JsonPropertyName("pairwise_preferences")] CurrentCollectionTeacherPairwisePreference[] PairwisePreferences)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "museum_terminal_teacher_comparison.v1";
    [JsonPropertyName("scope")] public string Scope => "reserve_safe_held_final_donation_vs_bounded_no_donation_only";
    [JsonPropertyName("selection_policy_id")] public string SelectionPolicyId => MuseumTerminalTeacherComparisonBuilder.PolicyId;
    [JsonPropertyName("comparison_horizon_ticks")] public int ComparisonHorizonTicks => MuseumTerminalTeacherComparisonBuilder.HorizonTicks;
    [JsonPropertyName("original_donation_candidate_count")] public int OriginalDonationCandidateCount => CandidateEvaluations.Length - 1;
    [JsonPropertyName("independent_pairwise_label_eligible")] public bool IndependentPairwiseLabelEligible => PairwisePreferences.Length > 0;
    [JsonPropertyName("complete_preference_label_eligible")] public bool CompletePreferenceLabelEligible => false;
    [JsonPropertyName("native_terminal_outcome_verified")] public bool NativeTerminalOutcomeVerified => false;
    [JsonPropertyName("prior_acquisition_verified")] public bool PriorAcquisitionVerified => false;
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_training_authorized")] public bool FormalTrainingAuthorized => false;
    [JsonPropertyName("uses_learner_rank_or_native_outcome")] public bool UsesLearnerRankOrNativeOutcome => false;
}
