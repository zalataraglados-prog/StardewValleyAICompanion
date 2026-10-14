using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueConcreteAlternativeDenominatorManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_concrete_alternative_denominator_manifest.v1";

    [JsonPropertyName("alternative_denominator_catalog_path")]
    public string AlternativeDenominatorCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("terminal_rebind_manifest_path")]
    public string TerminalRebindManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_rebind_receipt_path")]
    public string TerminalRebindReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueConcreteAlternative
{
    [JsonPropertyName("concrete_alternative_id")]
    public string ConcreteAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("skeleton_alternative_id")]
    public string SkeletonAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("skeleton_path_sha256")]
    public string SkeletonPathSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_catalog_candidate_id")]
    public string SourceCatalogCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_option_id")]
    public string SourceOptionId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate")]
    public MoneyRevenueCandidateCatalogEntry SourceCandidate { get; set; } =
        new();

    [JsonPropertyName("terminal_option_id")]
    public string TerminalOptionId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_candidate")]
    public MoneyRevenueCandidateCatalogEntry TerminalCandidate { get; set; } =
        new();

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

    [JsonPropertyName("uses_fresh_terminal_rebind")]
    public bool UsesFreshTerminalRebind { get; set; }

    [JsonPropertyName("terminal_state_hash")]
    public string TerminalStateHash { get; set; } = string.Empty;

    [JsonPropertyName("rebind_evidence_sha256")]
    public string RebindEvidenceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_id")]
    public string AcquisitionRolloutId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_manifest_path")]
    public string AcquisitionRolloutManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_manifest_sha256")]
    public string AcquisitionRolloutManifestSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_receipt_path")]
    public string AcquisitionRolloutReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_receipt_sha256")]
    public string AcquisitionRolloutReceiptSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("path_node_ids")]
    public string[] PathNodeIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("path_edges")]
    public MoneyRevenueAlternativePathEdge[] PathEdges { get; set; } =
        Array.Empty<MoneyRevenueAlternativePathEdge>();

    [JsonPropertyName("concrete_alternative_sha256")]
    public string ConcreteAlternativeSha256 { get; set; } = string.Empty;
}

public sealed class MoneyRevenueConcreteAlternativeDenominatorCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_concrete_alternative_denominator_catalog.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = "goal.economy.earn_money";

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("alternative_denominator_catalog_path")]
    public string AlternativeDenominatorCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("alternative_denominator_file_sha256")]
    public string AlternativeDenominatorFileSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("alternative_denominator_catalog_sha256")]
    public string AlternativeDenominatorCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("terminal_rebind_manifest_sha256")]
    public string TerminalRebindManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_rebind_manifest_path")]
    public string TerminalRebindManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_rebind_receipt_sha256")]
    public string TerminalRebindReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_rebind_receipt_path")]
    public string TerminalRebindReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("skeleton_denominator_complete")]
    public bool SkeletonDenominatorComplete { get; set; }

    [JsonPropertyName("terminal_rebind_denominator_complete")]
    public bool TerminalRebindDenominatorComplete { get; set; }

    [JsonPropertyName("current_concrete_denominator_authoritative")]
    public bool CurrentConcreteDenominatorAuthoritative { get; set; }

    [JsonPropertyName("long_horizon_denominator_authoritative")]
    public bool LongHorizonDenominatorAuthoritative { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("skeleton_count")]
    public int SkeletonCount { get; set; }

    [JsonPropertyName("direct_alternative_count")]
    public int DirectAlternativeCount { get; set; }

    [JsonPropertyName("rebound_alternative_count")]
    public int ReboundAlternativeCount { get; set; }

    [JsonPropertyName("concrete_alternative_count")]
    public int ConcreteAlternativeCount { get; set; }

    [JsonPropertyName("concrete_alternative_ids")]
    public string[] ConcreteAlternativeIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("alternatives")]
    public MoneyRevenueConcreteAlternative[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueConcreteAlternative>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("catalog_sha256")]
    public string CatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "The catalog is the deterministic union of every direct terminal skeleton and every " +
        "verified fresh-terminal rebound candidate. Direct and acquisition-dependent alternatives " +
        "retain distinct proof requirements. No caller-supplied alternative identity is admitted. " +
        "Current concrete denominator authority does not imply verified recurrence outcomes, a " +
        "production Teacher preference label, long-horizon authority or formal product training.";
}
