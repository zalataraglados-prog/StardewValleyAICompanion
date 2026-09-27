using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class GoalMethodFormalTrainingGateCategories
{
    public const string EvidenceIntegrity = "evidence_integrity";
    public const string TeacherCoverage = "teacher_coverage";
    public const string RuntimePromotion = "runtime_promotion";
    public const string SafetyIsolation = "safety_isolation";
}

public static class GoalMethodFormalTrainingGateIds
{
    public const string AuthoritativeGoalDenominator =
        "authoritative_goal_denominator";
    public const string AcquisitionCorpusReplay =
        "acquisition_corpus_replay";
    public const string DatasetSplitIntegrity = "dataset_split_integrity";
    public const string CheckpointCorpusBinding =
        "checkpoint_corpus_binding";
    public const string HoldoutEvaluationRecomputed =
        "holdout_evaluation_recomputed";
    public const string SupportTerminalLineage =
        "support_terminal_lineage";
    public const string CompleteTeacherCoverage =
        "complete_teacher_coverage";
    public const string ReferencedOptionExecutionInventory =
        "referenced_option_execution_inventory";
    public const string LeafAuthorityIsolation =
        "leaf_authority_isolation";
}

public sealed class GoalMethodFormalTrainingAdmissionReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "goal_method_formal_training_admission_reconciliation.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("target_score")]
    public int TargetScore { get; set; }

    [JsonPropertyName("criterion_denominator_count")]
    public int CriterionDenominatorCount { get; set; }

    [JsonPropertyName("coverage_ready_criterion_count")]
    public int CoverageReadyCriterionCount { get; set; }

    [JsonPropertyName("root_method_count")]
    public int RootMethodCount { get; set; }

    [JsonPropertyName("coverage_ready_method_count")]
    public int CoverageReadyMethodCount { get; set; }

    [JsonPropertyName("corpus_source_count")]
    public int CorpusSourceCount { get; set; }

    [JsonPropertyName("corpus_row_count")]
    public int CorpusRowCount { get; set; }

    [JsonPropertyName("train_rows")]
    public int TrainRows { get; set; }

    [JsonPropertyName("train_pairs")]
    public int TrainPairs { get; set; }

    [JsonPropertyName("validation_rows")]
    public int ValidationRows { get; set; }

    [JsonPropertyName("validation_pairs")]
    public int ValidationPairs { get; set; }

    [JsonPropertyName("validation_pair_accuracy")]
    public double ValidationPairAccuracy { get; set; }

    [JsonPropertyName("test_rows")]
    public int TestRows { get; set; }

    [JsonPropertyName("test_pairs")]
    public int TestPairs { get; set; }

    [JsonPropertyName("test_pair_accuracy")]
    public double TestPairAccuracy { get; set; }

    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("required_support_transition_count")]
    public int RequiredSupportTransitionCount { get; set; }

    [JsonPropertyName("covered_support_transition_count")]
    public int CoveredSupportTransitionCount { get; set; }

    [JsonPropertyName("teacher_training_evidence_ready")]
    public bool TeacherTrainingEvidenceReady { get; set; }

    [JsonPropertyName("runtime_product_promotion_ready")]
    public bool RuntimeProductPromotionReady { get; set; }

    [JsonPropertyName("ready_for_separate_promotion_review")]
    public bool ReadyForSeparatePromotionReview { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("artifact_bindings")]
    public GoalMethodFormalTrainingArtifactBinding[] ArtifactBindings
    { get; set; } = Array.Empty<
        GoalMethodFormalTrainingArtifactBinding>();

    [JsonPropertyName("gates")]
    public GoalMethodFormalTrainingGateResult[] Gates { get; set; } =
        Array.Empty<GoalMethodFormalTrainingGateResult>();

    [JsonPropertyName("intentional_safety_locks")]
    public string[] IntentionalSafetyLocks { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("current_teacher_blocking_reasons")]
    public string[] CurrentTeacherBlockingReasons { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("downstream_promotion_blocking_reasons")]
    public string[] DownstreamPromotionBlockingReasons { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("next_actions")]
    public string[] NextActions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("reconciliation_policy")]
    public string ReconciliationPolicy { get; set; } =
        "This read-only reconciliation rebuilds the authoritative 19-criterion Teacher gate, replays the acquisition corpus and its rollout/admission sources, verifies train/validation/test partitions, rebinds and reevaluates the checkpoint, and recomputes support-transition terminal lineage. Leaf formal-training flags must remain false. A complete report permits only a separate promotion review; it never self-authorizes formal product training or learned runtime authority.";
}

public sealed record GoalMethodFormalTrainingArtifactBinding(
    [property: JsonPropertyName("artifact_kind")] string ArtifactKind,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record GoalMethodFormalTrainingGateResult(
    [property: JsonPropertyName("gate_id")] string GateId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("satisfied")] bool Satisfied,
    [property: JsonPropertyName("required_for_promotion_review")] bool RequiredForPromotionReview,
    [property: JsonPropertyName("details")] string[] Details);
