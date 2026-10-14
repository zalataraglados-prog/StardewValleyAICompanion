using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCounterfactualProofRequestManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_counterfactual_proof_request_manifest.v1";

    [JsonPropertyName("concrete_alternative_catalog_path")]
    public string ConcreteAlternativeCatalogPath { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_path")]
    public string SourceStrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCounterfactualProofRequest
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_id")]
    public string ConcreteAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_sha256")]
    public string ConcreteAlternativeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_sha256")]
    public string SourceStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_revision")]
    public int SourceStrategyLedgerRevision { get; set; }

    [JsonPropertyName("terminal_state_hash")]
    public string TerminalStateHash { get; set; } = string.Empty;

    [JsonPropertyName("terminal_snapshot_path")]
    public string TerminalSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_snapshot_sha256")]
    public string TerminalSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_option_id")]
    public string TerminalOptionId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_candidate")]
    public MoneyRevenueCandidateCatalogEntry TerminalCandidate { get; set; } =
        new();

    [JsonPropertyName("proof_shape")]
    public string ProofShape { get; set; } = string.Empty;

    [JsonPropertyName("required_revenue_transition_proof_kind")]
    public string RequiredRevenueTransitionProofKind { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_required")]
    public bool AcquisitionRolloutProofRequired { get; set; }

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

    [JsonPropertyName("terminal_opportunity_cost_proof_required")]
    public bool TerminalOpportunityCostProofRequired { get; set; } = true;

    [JsonPropertyName("terminal_reservation_claim_proof_required")]
    public bool TerminalReservationClaimProofRequired { get; set; } = true;

    [JsonPropertyName("native_shop_sale_proof_required")]
    public bool NativeShopSaleProofRequired { get; set; }

    [JsonPropertyName("native_shipping_deposit_proof_required")]
    public bool NativeShippingDepositProofRequired { get; set; }

    [JsonPropertyName("native_shipping_settlement_proof_required")]
    public bool NativeShippingSettlementProofRequired { get; set; }

    [JsonPropertyName("revenue_recurrence_proof_required")]
    public bool RevenueRecurrenceProofRequired { get; set; } = true;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;
}

public sealed class MoneyRevenueCounterfactualProofRequestCatalog
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_counterfactual_proof_request_catalog.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_catalog_path")]
    public string ConcreteAlternativeCatalogPath { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_catalog_file_sha256")]
    public string ConcreteAlternativeCatalogFileSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("concrete_alternative_catalog_sha256")]
    public string ConcreteAlternativeCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_path")]
    public string SourceStrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_sha256")]
    public string SourceStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_id")]
    public string SourceStrategyLedgerId { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_revision")]
    public int SourceStrategyLedgerRevision { get; set; }

    [JsonPropertyName("request_denominator_authoritative")]
    public bool RequestDenominatorAuthoritative { get; set; }

    [JsonPropertyName("counterfactual_proof_collection_ready")]
    public bool CounterfactualProofCollectionReady { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }

    [JsonPropertyName("request_ids")]
    public string[] RequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("requests")]
    public MoneyRevenueCounterfactualProofRequest[] Requests { get; set; } =
        Array.Empty<MoneyRevenueCounterfactualProofRequest>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("catalog_sha256")]
    public string CatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("request_policy")]
    public string RequestPolicy { get; set; } =
        "Every authoritative concrete alternative emits exactly one proof request. Direct " +
        "alternatives require no fabricated acquisition rollout; rebound alternatives retain " +
        "their verified rollout identity. Every request still requires independently verified " +
        "terminal opportunity cost, reservation safety and native Revenue recurrence evidence. " +
        "This catalog schedules " +
        "proof collection but cannot claim outcomes, emit a production label or authorize training.";
}
