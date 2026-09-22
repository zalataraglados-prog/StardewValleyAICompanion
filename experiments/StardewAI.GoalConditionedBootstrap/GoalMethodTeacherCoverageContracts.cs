using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class GoalMethodTeacherCoverageSourceKinds
{
    public const string AcquisitionRoutePortfolioCorpus =
        "acquisition_route_portfolio_supervision_corpus";
}

public static class GoalMethodTeacherCoverageGoalIds
{
    public const string Authoritative = "grandpa.maximum_21";
    public const string AcquisitionCorpus = "goal.grandpa_21";
}

public sealed class GoalMethodFrontierBuildInputs
{
    public string ExpansionPath { get; set; } = string.Empty;
    public string DependencyExpansionPath { get; set; } = string.Empty;
    public string IsolatedTrainingAuthorizationPath { get; set; } =
        string.Empty;
    public string RequirementInventoryPath { get; set; } = string.Empty;
    public string AcquisitionLoweringPath { get; set; } = string.Empty;
    public string AcquisitionLoweringCatalogPath { get; set; } = string.Empty;
    public string KnowledgePath { get; set; } = string.Empty;
    public string OptionMatrixPath { get; set; } = string.Empty;
    public string ClaimLedgerPath { get; set; } = string.Empty;
    public string DirectionCatalogSourcePath { get; set; } = string.Empty;
}

public sealed class GoalMethodTeacherCoverageRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "goal_method_teacher_coverage_request.v1";

    [JsonPropertyName("coverage_id")]
    public string CoverageId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public GoalMethodTeacherCoverageSource[] Sources { get; set; } =
        Array.Empty<GoalMethodTeacherCoverageSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record GoalMethodTeacherCoverageSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("artifact_path")] string ArtifactPath);

public sealed class GoalMethodTeacherCoverageReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "goal_method_teacher_coverage_gate.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("coverage_id")]
    public string CoverageId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("target_score")]
    public int TargetScore { get; set; }

    [JsonPropertyName("criterion_point_sum")]
    public int CriterionPointSum { get; set; }

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("frontier_status")]
    public string FrontierStatus { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_sha256")]
    public string KnowledgeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("direction_catalog_sha256")]
    public string DirectionCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("expansion_overlay_sha256")]
    public string ExpansionOverlaySha256 { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_sha256")]
    public string DependencyExpansionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("isolated_training_authorization_sha256")]
    public string IsolatedTrainingAuthorizationSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_route_lowering_sha256")]
    public string AcquisitionRouteLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_route_lowering_catalog_sha256")]
    public string AcquisitionRouteLoweringCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("option_matrix_sha256")]
    public string OptionMatrixSha256 { get; set; } = string.Empty;

    [JsonPropertyName("claim_ledger_sha256")]
    public string ClaimLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public GoalMethodTeacherCoverageSourceDigest[] Sources { get; set; } =
        Array.Empty<GoalMethodTeacherCoverageSourceDigest>();

    [JsonPropertyName("criterion_denominator_count")]
    public int CriterionDenominatorCount { get; set; }

    [JsonPropertyName("catalog_mapped_criterion_count")]
    public int CatalogMappedCriterionCount { get; set; }

    [JsonPropertyName("executable_criterion_count")]
    public int ExecutableCriterionCount { get; set; }

    [JsonPropertyName("teacher_comparison_covered_criterion_count")]
    public int TeacherComparisonCoveredCriterionCount { get; set; }

    [JsonPropertyName("native_outcome_covered_criterion_count")]
    public int NativeOutcomeCoveredCriterionCount { get; set; }

    [JsonPropertyName("split_complete_teacher_criterion_count")]
    public int SplitCompleteTeacherCriterionCount { get; set; }

    [JsonPropertyName("coverage_gate_ready_criterion_count")]
    public int CoverageGateReadyCriterionCount { get; set; }

    [JsonPropertyName("criteria")]
    public GoalMethodTeacherCriterionCoverage[] Criteria { get; set; } =
        Array.Empty<GoalMethodTeacherCriterionCoverage>();

    [JsonPropertyName("coverage_gate_satisfied")]
    public bool CoverageGateSatisfied { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("coverage_policy")]
    public string CoveragePolicy { get; set; } =
        "Every native Grandpa criterion is derived from the rebuilt authoritative goal-method frontier. A criterion is coverage-ready only when its sole method is executable, an admitted explicit Teacher comparison varies that method in train, validation and test, and verified native outcomes for that method exist in all three partitions. Source declarations cannot name criteria. This gate reports readiness only and never authorizes formal product training.";
}

public sealed record GoalMethodTeacherCoverageSourceDigest(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_kind")] string SourceKind,
    [property: JsonPropertyName("artifact_path")] string ArtifactPath,
    [property: JsonPropertyName("artifact_sha256")] string ArtifactSha256,
    [property: JsonPropertyName("verified_row_count")] int VerifiedRowCount,
    [property: JsonPropertyName("verified_pair_count")] int VerifiedPairCount);

public sealed record GoalMethodTeacherCriterionCoverage(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("direction_id")] string DirectionId,
    [property: JsonPropertyName("method_id")] string MethodId,
    [property: JsonPropertyName("method_status")] string MethodStatus,
    [property: JsonPropertyName("requirement_set_ids")] string[] RequirementSetIds,
    [property: JsonPropertyName("teacher_comparison_partitions")] string[] TeacherComparisonPartitions,
    [property: JsonPropertyName("native_outcome_partitions")] string[] NativeOutcomePartitions,
    [property: JsonPropertyName("teacher_comparison_covered")] bool TeacherComparisonCovered,
    [property: JsonPropertyName("native_outcome_covered")] bool NativeOutcomeCovered,
    [property: JsonPropertyName("split_coverage_complete")] bool SplitCoverageComplete,
    [property: JsonPropertyName("coverage_gate_ready")] bool CoverageGateReady,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);
