using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueComparisonAdmissionVersions
{
    public const string Manifest =
        "money_revenue_comparison_admission_manifest.v1";
    public const string Receipt =
        "money_revenue_comparison_admission_receipt.v2";
}

public sealed class MoneyRevenueComparisonAdmissionManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyRevenueComparisonAdmissionVersions.Manifest;

    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_execution_proof_manifest_path")]
    public string TerminalExecutionProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("terminal_execution_proof_receipt_path")]
    public string TerminalExecutionProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueComparisonAdmissionAlternative(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("concrete_alternative_id")]
        string ConcreteAlternativeId,
    [property: JsonPropertyName("terminal_execution_evidence_sha256")]
        string TerminalExecutionEvidenceSha256,
    [property: JsonPropertyName("native_terminal_artifacts")]
        MoneyRevenueArtifactBinding[] NativeTerminalArtifacts,
    [property: JsonPropertyName("acquisition_rollout_required")]
        bool AcquisitionRolloutRequired,
    [property: JsonPropertyName("acquisition_evidence")]
        MoneyRevenueAlternativeArtifactEvidence? AcquisitionEvidence,
    [property: JsonPropertyName("revenue_prefix")]
        MoneyRevenueRecurrencePrefixEvidence RevenuePrefix,
    [property: JsonPropertyName("acquisition_opportunity_cost")]
        AcquisitionOpportunityCostVector? AcquisitionOpportunityCost,
    [property: JsonPropertyName("terminal_opportunity_cost")]
        AcquisitionOpportunityCostVector TerminalOpportunityCost,
    [property: JsonPropertyName("aggregate_opportunity_cost")]
        AcquisitionOpportunityCostVector AggregateOpportunityCost,
    [property: JsonPropertyName("reservation_not_required")]
        bool ReservationNotRequired,
    [property: JsonPropertyName("reservation_ids")]
        string[] ReservationIds);

public sealed class MoneyRevenueComparisonAdmissionReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyRevenueComparisonAdmissionVersions.Receipt;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("request_catalog_sha256")]
    public string RequestCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_execution_proof_receipt_sha256")]
    public string TerminalExecutionProofReceiptSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_sha256")]
    public string SourceStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_ledger_revision")]
    public int SourceLedgerRevision { get; set; }

    [JsonPropertyName("request_denominator_count")]
    public int RequestDenominatorCount { get; set; }

    [JsonPropertyName("request_ids")]
    public string[] RequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("concrete_alternative_ids")]
    public string[] ConcreteAlternativeIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("candidate_denominator_authoritative")]
    public bool CandidateDenominatorAuthoritative { get; set; }

    [JsonPropertyName("terminal_execution_proofs_verified")]
    public bool TerminalExecutionProofsVerified { get; set; }

    [JsonPropertyName("comparison")]
    public MoneyRevenueAlternativeComparison Comparison { get; set; } =
        new(
            string.Empty,
            0,
            string.Empty,
            0,
            0,
            false,
            MoneyRevenueAlternativeSelectionDisposition.InvalidDenominator,
            string.Empty,
            false,
            Array.Empty<MoneyRevenueAlternativeEvaluation>(),
            Array.Empty<MoneyRevenueAlternativePairwisePreference>());

    [JsonPropertyName("alternatives")]
    public MoneyRevenueComparisonAdmissionAlternative[] Alternatives
    { get; set; } =
        Array.Empty<MoneyRevenueComparisonAdmissionAlternative>();

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The authoritative counterfactual request IDs and terminal execution evidence IDs must be exactly equal. " +
        "Direct alternatives use terminal execution cost; rebound alternatives add their independently rebuilt " +
        "acquisition cost and reservation identity. Only a unique strict-Pareto result is eligible to become a " +
        "production Teacher preference. Criterion partition admission and formal product training remain separate.";
}
