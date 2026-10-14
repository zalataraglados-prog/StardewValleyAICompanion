using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueAlternativeDenominatorCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_alternative_denominator_catalog.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = "goal.economy.earn_money";

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_catalog_path")]
    public string SourceCandidateCatalogPath { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_catalog_file_sha256")]
    public string SourceCandidateCatalogFileSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("source_candidate_catalog_sha256")]
    public string SourceCandidateCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("dependency_expansion_sha256")]
    public string DependencyExpansionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settled_milestone_node_id")]
    public string SettledMilestoneNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_denominator_authoritative")]
    public bool SourceCandidateDenominatorAuthoritative { get; set; }

    [JsonPropertyName("alternative_skeleton_denominator_complete")]
    public bool AlternativeSkeletonDenominatorComplete { get; set; }

    [JsonPropertyName("current_alternative_denominator_authoritative")]
    public bool CurrentAlternativeDenominatorAuthoritative { get; set; }

    [JsonPropertyName("long_horizon_denominator_authoritative")]
    public bool LongHorizonDenominatorAuthoritative { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("source_candidate_count")]
    public int SourceCandidateCount { get; set; }

    [JsonPropertyName("alternative_count")]
    public int AlternativeCount { get; set; }

    [JsonPropertyName("alternative_ids")]
    public string[] AlternativeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("alternatives")]
    public MoneyRevenueAlternativeSkeleton[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueAlternativeSkeleton>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("catalog_sha256")]
    public string CatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every currently comparison-eligible policy candidate is expanded through every simple authoritative dependency path to the unique settled-money milestone. This artifact is a complete high-level alternative skeleton denominator, not execution or outcome proof. Direct sale, shipping deposit plus day settlement, and acquisition/processing rollouts retain distinct proof shapes.";
}

public sealed class MoneyRevenueAlternativeSkeleton
{
    [JsonPropertyName("alternative_id")]
    public string AlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("source_catalog_candidate_id")]
    public string SourceCatalogCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_sha256")]
    public string SourceCandidateSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_id")]
    public string SourceCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_kind")]
    public string SourceCandidateKind { get; set; } = string.Empty;

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_option_id")]
    public string SourceOptionId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_option_id")]
    public string TerminalOptionId { get; set; } = string.Empty;

    [JsonPropertyName("proof_shape")]
    public string ProofShape { get; set; } = string.Empty;

    [JsonPropertyName("requires_acquisition_or_processing_rollout")]
    public bool RequiresAcquisitionOrProcessingRollout { get; set; }

    [JsonPropertyName("requires_native_shop_sale_proof")]
    public bool RequiresNativeShopSaleProof { get; set; }

    [JsonPropertyName("requires_native_shipping_deposit_proof")]
    public bool RequiresNativeShippingDepositProof { get; set; }

    [JsonPropertyName("requires_native_shipping_settlement_proof")]
    public bool RequiresNativeShippingSettlementProof { get; set; }

    [JsonPropertyName("requires_fresh_terminal_candidate_rebind")]
    public bool RequiresFreshTerminalCandidateRebind { get; set; }

    [JsonPropertyName("concrete_alternative_identity_complete")]
    public bool ConcreteAlternativeIdentityComplete { get; set; }

    [JsonPropertyName("path_node_ids")]
    public string[] PathNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("path_edges")]
    public MoneyRevenueAlternativePathEdge[] PathEdges { get; set; } =
        Array.Empty<MoneyRevenueAlternativePathEdge>();

    [JsonPropertyName("path_sha256")]
    public string PathSha256 { get; set; } = string.Empty;
}

public sealed record MoneyRevenueAlternativePathEdge(
    [property: JsonPropertyName("from_node_id")] string FromNodeId,
    [property: JsonPropertyName("to_node_id")] string ToNodeId,
    [property: JsonPropertyName("edge_kind")] string EdgeKind);
