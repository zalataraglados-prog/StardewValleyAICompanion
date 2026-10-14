using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCandidateDenominatorCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_candidate_denominator_catalog.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = "goal.economy.earn_money";

    [JsonPropertyName("direction_id")]
    public string DirectionId { get; set; } = "earn_money";

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_path")]
    public string DependencyExpansionPath { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_sha256")]
    public string DependencyExpansionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_status")]
    public string DependencyExpansionStatus { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_blockers")]
    public string[] DependencyExpansionBlockers { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("node_coverage_complete")]
    public bool NodeCoverageComplete { get; set; }

    [JsonPropertyName("current_snapshot_denominator_authoritative")]
    public bool CurrentSnapshotDenominatorAuthoritative { get; set; }

    [JsonPropertyName("current_comparison_denominator_ready")]
    public bool CurrentComparisonDenominatorReady { get; set; }

    [JsonPropertyName("long_horizon_denominator_authoritative")]
    public bool LongHorizonDenominatorAuthoritative { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("policy_node_count")]
    public int PolicyNodeCount { get; set; }

    [JsonPropertyName("deterministic_support_node_count")]
    public int DeterministicSupportNodeCount { get; set; }

    [JsonPropertyName("enumerated_candidate_count")]
    public int EnumeratedCandidateCount { get; set; }

    [JsonPropertyName("comparison_candidate_count")]
    public int ComparisonCandidateCount { get; set; }

    [JsonPropertyName("comparison_candidate_ids")]
    public string[] ComparisonCandidateIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("nodes")]
    public MoneyRevenueCandidateNodeCoverage[] Nodes { get; set; } =
        Array.Empty<MoneyRevenueCandidateNodeCoverage>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("catalog_sha256")]
    public string CatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Current candidates are enumerated once through CandidateOptionAvailabilityEvaluator. " +
        "The catalog does not reproduce price, protection, route, compiler, or executor rules. " +
        "Current enumeration authority does not imply long-horizon coverage, rollout proof, " +
        "teacher-label eligibility, or formal product-training authorization.";
}

public sealed class MoneyRevenueCandidateNodeCoverage
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("node_kind")]
    public string NodeKind { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("policy_choice")]
    public bool PolicyChoice { get; set; }

    [JsonPropertyName("deterministic_support")]
    public bool DeterministicSupport { get; set; }

    [JsonPropertyName("declared_candidate_kinds")]
    public string[] DeclaredCandidateKinds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("required_field_paths")]
    public string[] RequiredFieldPaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("option_evaluation_count")]
    public int OptionEvaluationCount { get; set; }

    [JsonPropertyName("option_status")]
    public string OptionStatus { get; set; } = string.Empty;

    [JsonPropertyName("option_available")]
    public bool OptionAvailable { get; set; }

    [JsonPropertyName("read_eligible")]
    public bool ReadEligible { get; set; }

    [JsonPropertyName("missing_state_factors")]
    public string[] MissingStateFactors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("option_blocking_reasons")]
    public string[] OptionBlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("enumeration_complete")]
    public bool EnumerationComplete { get; set; }

    [JsonPropertyName("enumerated_candidate_count")]
    public int EnumeratedCandidateCount { get; set; }

    [JsonPropertyName("comparison_candidate_count")]
    public int ComparisonCandidateCount { get; set; }

    [JsonPropertyName("candidates")]
    public MoneyRevenueCandidateCatalogEntry[] Candidates { get; set; } =
        Array.Empty<MoneyRevenueCandidateCatalogEntry>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class MoneyRevenueCandidateCatalogEntry
{
    [JsonPropertyName("catalog_candidate_id")]
    public string CatalogCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("candidate_channel")]
    public string CandidateChannel { get; set; } = string.Empty;

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("candidate_kind")]
    public string CandidateKind { get; set; } = string.Empty;

    [JsonPropertyName("semantic_kind_binding")]
    public string SemanticKindBinding { get; set; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("comparison_eligible")]
    public bool ComparisonEligible { get; set; }

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int? TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int? TileY { get; set; }

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("shop_id")]
    public string ShopId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int? SlotIndex { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    public int UnitPrice { get; set; }

    [JsonPropertyName("total_value")]
    public int TotalValue { get; set; }

    [JsonPropertyName("estimated_ticks")]
    public int EstimatedTicks { get; set; }

    [JsonPropertyName("energy_cost")]
    public double EnergyCost { get; set; }

    [JsonPropertyName("parameters")]
    public SmallModelActionParameter[] Parameters { get; set; } =
        Array.Empty<SmallModelActionParameter>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("candidate_sha256")]
    public string CandidateSha256 { get; set; } = string.Empty;
}
