using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdTeacherCorpusVersions
{
    public const string Request =
        "money_threshold_teacher_corpus_request.v4";
    public const string Corpus =
        "money_threshold_teacher_corpus.v4";
}

public static class MoneyThresholdNativeOutcomeKinds
{
    public const string ShopSaleEpisode = "shop_sale_episode";
    public const string RevenueTrajectory = "revenue_trajectory";
}

public sealed class MoneyThresholdTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdTeacherCorpusVersions.Request;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public MoneyThresholdTeacherCorpusSource[] Sources { get; set; } =
        Array.Empty<MoneyThresholdTeacherCorpusSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyThresholdTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath,
    [property: JsonPropertyName("comparison_admissions")]
        MoneyThresholdTeacherComparisonSource[]? ComparisonAdmissions = null,
    [property: JsonPropertyName("native_trajectory_receipt_path")]
        string NativeTrajectoryReceiptPath = "");

public sealed record MoneyThresholdTeacherComparisonSource(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("admission_manifest_path")]
        string AdmissionManifestPath,
    [property: JsonPropertyName("admission_receipt_path")]
        string AdmissionReceiptPath);

public sealed class MoneyThresholdTeacherCorpus
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdTeacherCorpusVersions.Corpus;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } =
        GoalMethodTeacherCoverageGoalIds.Authoritative;

    [JsonPropertyName("direction_id")]
    public string DirectionId { get; set; } = "earn_money";

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } = "grandpa.direct.earn_money";

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("covered_criterion_count")]
    public int CoveredCriterionCount { get; set; }

    [JsonPropertyName("teacher_compared_criterion_count")]
    public int TeacherComparedCriterionCount { get; set; }

    [JsonPropertyName("teacher_comparison_pair_count")]
    public int TeacherComparisonPairCount { get; set; }

    [JsonPropertyName("native_split_complete")]
    public bool NativeSplitComplete { get; set; }

    [JsonPropertyName("teacher_comparison_split_complete")]
    public bool TeacherComparisonSplitComplete { get; set; }

    [JsonPropertyName("rows")]
    public MoneyThresholdTeacherEvidenceRow[] Rows { get; set; } =
        Array.Empty<MoneyThresholdTeacherEvidenceRow>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("corpus_policy")]
    public string CorpusPolicy { get; set; } =
        "Each row is rebuilt from exactly one native outcome source: either an isolated ShopMenu sale episode or a complete verified Revenue trajectory. A trajectory preserves the authoritative request, optional acquisition rollout, terminal execution, sale or shipping settlement recurrence, source/final checkpoints and transitive artifact hashes. Crossed Grandpa thresholds and their criterion IDs are derived only from evaluator authority. Native outcomes never self-label. A criterion-local Teacher comparison is admitted only when its exact selected alternative, Revenue prefix, terminal artifacts, source identity and authoritative criterion match the rebuilt native outcome.";
}

public sealed record MoneyThresholdTeacherEvidenceRow(
    [property: JsonPropertyName("row_id")] string RowId,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath,
    [property: JsonPropertyName("execution_episode_sha256")]
        string ExecutionEpisodeSha256,
    [property: JsonPropertyName("before_snapshot_path")]
        string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")]
        string BeforeSnapshotSha256,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_snapshot_path")]
        string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")]
        string AfterSnapshotSha256,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("execution_result_path")]
        string ExecutionResultPath,
    [property: JsonPropertyName("execution_result_sha256")]
        string ExecutionResultSha256,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("money_before")] int MoneyBefore,
    [property: JsonPropertyName("money_after")] int MoneyAfter,
    [property: JsonPropertyName("total_money_earned_before")]
        uint TotalMoneyEarnedBefore,
    [property: JsonPropertyName("total_money_earned_after")]
        uint TotalMoneyEarnedAfter,
    [property: JsonPropertyName("earned_delta")] uint EarnedDelta,
    [property: JsonPropertyName("crossed_thresholds")]
        MoneyThresholdTeacherCriterion[] CrossedThresholds,
    [property: JsonPropertyName("teacher_comparison_count")]
        int TeacherComparisonCount,
    [property: JsonPropertyName("native_terminal_outcome_verified")]
        bool NativeTerminalOutcomeVerified,
    [property: JsonPropertyName("native_outcome_kind")]
        string NativeOutcomeKind,
    [property: JsonPropertyName("native_outcome_primary_path")]
        string NativeOutcomePrimaryPath,
    [property: JsonPropertyName("native_outcome_primary_sha256")]
        string NativeOutcomePrimarySha256,
    [property: JsonPropertyName("native_trajectory_receipt_path")]
        string NativeTrajectoryReceiptPath,
    [property: JsonPropertyName("native_trajectory_receipt_sha256")]
        string NativeTrajectoryReceiptSha256,
    [property: JsonPropertyName("concrete_alternative_id")]
        string ConcreteAlternativeId,
    [property: JsonPropertyName("acquisition_rollout_required")]
        bool AcquisitionRolloutRequired,
    [property: JsonPropertyName("terminal_execution_evidence_sha256")]
        string TerminalExecutionEvidenceSha256,
    [property: JsonPropertyName("revenue_prefix")]
        MoneyRevenueRecurrencePrefixEvidence RevenuePrefix,
    [property: JsonPropertyName("native_outcome_artifacts")]
        MoneyRevenueArtifactBinding[] NativeOutcomeArtifacts);

public sealed record MoneyThresholdTeacherCriterion(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("target")] uint Target,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("teacher_goal_context_id")]
        string TeacherGoalContextId,
    [property: JsonPropertyName("terminal_assertion")]
        string TerminalAssertion,
    [property: JsonPropertyName("teacher_comparison")]
        MoneyThresholdTeacherComparisonEvidence? TeacherComparison,
    [property: JsonPropertyName("native_terminal_outcome_verified")]
        bool NativeTerminalOutcomeVerified);

public sealed record MoneyThresholdTeacherComparisonEvidence(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("admission_manifest_path")]
        string AdmissionManifestPath,
    [property: JsonPropertyName("admission_manifest_sha256")]
        string AdmissionManifestSha256,
    [property: JsonPropertyName("admission_receipt_path")]
        string AdmissionReceiptPath,
    [property: JsonPropertyName("admission_receipt_sha256")]
        string AdmissionReceiptSha256,
    [property: JsonPropertyName("comparison_source_state_hash")]
        string ComparisonSourceStateHash,
    [property: JsonPropertyName("candidate_denominator_count")]
        int CandidateDenominatorCount,
    [property: JsonPropertyName("preferred_alternative")]
        string PreferredAlternative,
    [property: JsonPropertyName("rejected_alternatives")]
        string[] RejectedAlternatives,
    [property: JsonPropertyName("selected_terminal_episode_sha256")]
        string SelectedTerminalEpisodeSha256,
    [property: JsonPropertyName("selection_policy")]
        string SelectionPolicy,
    [property: JsonPropertyName("selected_native_outcome_sha256")]
        string SelectedNativeOutcomeSha256);
