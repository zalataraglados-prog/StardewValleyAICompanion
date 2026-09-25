using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class GoalMethodCoverageDispositions
{
    public const string CoverageReady = "coverage_ready";
    public const string DependencyGraphIncomplete =
        "dependency_graph_incomplete";
    public const string OptionGovernanceGap = "option_governance_gap";
    public const string TeacherSourceAdapterMissing =
        "teacher_source_adapter_missing";
    public const string EvidenceNotConnected = "evidence_not_connected";
    public const string TeacherComparisonMissing =
        "teacher_comparison_missing";
    public const string NativeSplitEvidenceMissing =
        "native_split_evidence_missing";
    public const string SplitEvidenceIncomplete =
        "split_evidence_incomplete";
}

public sealed class GoalMethodCoverageReconciliationReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "goal_method_coverage_reconciliation.v2";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("target_score")]
    public int TargetScore { get; set; }

    [JsonPropertyName("criterion_denominator_count")]
    public int CriterionDenominatorCount { get; set; }

    [JsonPropertyName("root_method_count")]
    public int RootMethodCount { get; set; }

    [JsonPropertyName("referenced_option_count")]
    public int ReferencedOptionCount { get; set; }

    [JsonPropertyName("executable_criterion_count")]
    public int ExecutableCriterionCount { get; set; }

    [JsonPropertyName("coverage_gate_ready_criterion_count")]
    public int CoverageGateReadyCriterionCount { get; set; }

    [JsonPropertyName("frontier_status")]
    public string FrontierStatus { get; set; } = string.Empty;

    [JsonPropertyName("teacher_coverage_status")]
    public string TeacherCoverageStatus { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("option_matrix_sha256")]
    public string OptionMatrixSha256 { get; set; } = string.Empty;

    [JsonPropertyName("criterion_disposition_counts")]
    public GoalMethodCoverageDispositionCount[] CriterionDispositionCounts
        { get; set; } = Array.Empty<GoalMethodCoverageDispositionCount>();

    [JsonPropertyName("method_disposition_counts")]
    public GoalMethodCoverageDispositionCount[] MethodDispositionCounts
        { get; set; } = Array.Empty<GoalMethodCoverageDispositionCount>();

    [JsonPropertyName("methods")]
    public GoalMethodCoverageMethodReconciliation[] Methods { get; set; } =
        Array.Empty<GoalMethodCoverageMethodReconciliation>();

    [JsonPropertyName("criteria")]
    public GoalMethodCoverageCriterionReconciliation[] Criteria { get; set; } =
        Array.Empty<GoalMethodCoverageCriterionReconciliation>();

    [JsonPropertyName("denominator_policy")]
    public string DenominatorPolicy { get; set; } =
        "The 19 rows are Grandpa score criteria, the root methods are strategic routes, and referenced options are an inventory-only action audit. Current method work is derived only from the authoritative frontier and verified Teacher/native coverage. Referenced-option diagnostics, including later Product Executor promotion, never create a current method blocker.";
}

public sealed record GoalMethodCoverageDispositionCount(
    [property: JsonPropertyName("disposition")] string Disposition,
    [property: JsonPropertyName("count")] int Count);

public sealed record GoalMethodCoverageCriterionReconciliation(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("direction_id")] string DirectionId,
    [property: JsonPropertyName("method_id")] string MethodId,
    [property: JsonPropertyName("method_status")] string MethodStatus,
    [property: JsonPropertyName("primary_disposition")] string PrimaryDisposition,
    [property: JsonPropertyName("coverage_gate_ready")] bool CoverageGateReady,
    [property: JsonPropertyName("open_work_kinds")] string[] OpenWorkKinds);

public sealed record GoalMethodCoverageMethodReconciliation(
    [property: JsonPropertyName("method_id")] string MethodId,
    [property: JsonPropertyName("direction_id")] string DirectionId,
    [property: JsonPropertyName("criterion_ids")] string[] CriterionIds,
    [property: JsonPropertyName("criterion_point_sum")] int CriterionPointSum,
    [property: JsonPropertyName("method_status")] string MethodStatus,
    [property: JsonPropertyName("primary_disposition")] string PrimaryDisposition,
    [property: JsonPropertyName("dependency_blockers")] string[] DependencyBlockers,
    [property: JsonPropertyName("requirement_set_ids")] string[] RequirementSetIds,
    [property: JsonPropertyName("implemented_teacher_source_kinds")] string[] ImplementedTeacherSourceKinds,
    [property: JsonPropertyName("active_teacher_source_kinds")] string[] ActiveTeacherSourceKinds,
    [property: JsonPropertyName("teacher_comparison_partitions")] string[] TeacherComparisonPartitions,
    [property: JsonPropertyName("native_outcome_partitions")] string[] NativeOutcomePartitions,
    [property: JsonPropertyName("referenced_options")] GoalMethodCoverageOptionReconciliation[] ReferencedOptions,
    [property: JsonPropertyName("referenced_option_inventory_transparent_read_complete")] bool ReferencedOptionInventoryTransparentReadComplete,
    [property: JsonPropertyName("referenced_option_inventory_native_runtime_complete")] bool ReferencedOptionInventoryNativeRuntimeComplete,
    [property: JsonPropertyName("referenced_option_inventory_five_gate_complete")] bool ReferencedOptionInventoryFiveGateComplete,
    [property: JsonPropertyName("referenced_option_inventory_internal_pipeline_complete")] bool ReferencedOptionInventoryInternalPipelineComplete,
    [property: JsonPropertyName("referenced_option_inventory_product_executor_complete")] bool ReferencedOptionInventoryProductExecutorComplete,
    [property: JsonPropertyName("option_inventory_transparent_read_diagnostics")] string[] OptionInventoryTransparentReadDiagnostics,
    [property: JsonPropertyName("option_inventory_runtime_evidence_diagnostics")] string[] OptionInventoryRuntimeEvidenceDiagnostics,
    [property: JsonPropertyName("option_inventory_product_executor_diagnostics")] string[] OptionInventoryProductExecutorDiagnostics,
    [property: JsonPropertyName("option_inventory_diagnostics_affect_current_readiness")] bool OptionInventoryDiagnosticsAffectCurrentReadiness,
    [property: JsonPropertyName("evidence_ids")] string[] EvidenceIds,
    [property: JsonPropertyName("open_work_kinds")] string[] OpenWorkKinds,
    [property: JsonPropertyName("next_actions")] string[] NextActions);

public sealed record GoalMethodCoverageOptionReconciliation(
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("roles")] string[] Roles,
    [property: JsonPropertyName("training_eligibility")] string TrainingEligibility,
    [property: JsonPropertyName("runtime_status")] string RuntimeStatus,
    [property: JsonPropertyName("product_status")] string ProductStatus,
    [property: JsonPropertyName("transparent_read_gate_ready")] bool TransparentReadGateReady,
    [property: JsonPropertyName("five_gate_training_ready")] bool FiveGateTrainingReady,
    [property: JsonPropertyName("internal_execution_pipeline_supported")] bool InternalExecutionPipelineSupported,
    [property: JsonPropertyName("product_executor_supported")] bool ProductExecutorSupported,
    [property: JsonPropertyName("evidence_ids")] string[] EvidenceIds,
    [property: JsonPropertyName("training_exclusion_reasons")] string[] TrainingExclusionReasons);
