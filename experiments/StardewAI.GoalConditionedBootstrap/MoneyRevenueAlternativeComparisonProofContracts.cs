using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueAlternativeComparisonProofManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_alternative_comparison_proof_manifest.v1";

    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public MoneyRevenueAlternativeArtifactProof[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueAlternativeArtifactProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueAlternativeArtifactProof
{
    [JsonPropertyName("alternative_id")]
    public string AlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("revenue_recurrence_proof_manifest_path")]
    public string RevenueRecurrenceProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("revenue_recurrence_proof_receipt_path")]
    public string RevenueRecurrenceProofReceiptPath { get; set; } =
        string.Empty;
}

public sealed record MoneyRevenueAlternativeRouteArtifactEvidence(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("route_occurrence_id")]
        string RouteOccurrenceId,
    [property: JsonPropertyName("source_state_hash")]
        string SourceStateHash,
    [property: JsonPropertyName("base_ledger_revision")]
        int BaseLedgerRevision,
    [property: JsonPropertyName("committed_ledger_revision")]
        int CommittedLedgerRevision,
    [property: JsonPropertyName("opportunity_cost_report_path")]
        string OpportunityCostReportPath,
    [property: JsonPropertyName("opportunity_cost_report_sha256")]
        string OpportunityCostReportSha256,
    [property: JsonPropertyName("portfolio_commit_receipt_path")]
        string PortfolioCommitReceiptPath,
    [property: JsonPropertyName("portfolio_commit_receipt_sha256")]
        string PortfolioCommitReceiptSha256,
    [property: JsonPropertyName("reservation_not_required")]
        bool ReservationNotRequired,
    [property: JsonPropertyName("reservation_ids")]
        string[] ReservationIds,
    [property: JsonPropertyName("opportunity_cost")]
        AcquisitionOpportunityCostVector OpportunityCost);

public sealed record MoneyRevenueAlternativeArtifactEvidence(
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("acquisition_goal_id")]
        string AcquisitionGoalId,
    [property: JsonPropertyName("acquisition_rollout_id")]
        string AcquisitionRolloutId,
    [property: JsonPropertyName("source_state_hash")]
        string SourceStateHash,
    [property: JsonPropertyName("source_snapshot_sha256")]
        string SourceSnapshotSha256,
    [property: JsonPropertyName("source_strategy_ledger_sha256")]
        string SourceStrategyLedgerSha256,
    [property: JsonPropertyName("source_ledger_revision")]
        int SourceLedgerRevision,
    [property: JsonPropertyName("revenue_start_state_hash")]
        string RevenueStartStateHash,
    [property: JsonPropertyName("acquisition_rollout_manifest_sha256")]
        string AcquisitionRolloutManifestSha256,
    [property: JsonPropertyName("acquisition_rollout_receipt_sha256")]
        string AcquisitionRolloutReceiptSha256,
    [property: JsonPropertyName("revenue_recurrence_manifest_sha256")]
        string RevenueRecurrenceManifestSha256,
    [property: JsonPropertyName("revenue_recurrence_receipt_sha256")]
        string RevenueRecurrenceReceiptSha256,
    [property: JsonPropertyName("route_transitions")]
        MoneyRevenueAlternativeRouteArtifactEvidence[] RouteTransitions,
    [property: JsonPropertyName("aggregate_opportunity_cost")]
        AcquisitionOpportunityCostVector AggregateOpportunityCost,
    [property: JsonPropertyName("revenue_prefix")]
        MoneyRevenueRecurrencePrefixEvidence RevenuePrefix);

public sealed class MoneyRevenueAlternativeComparisonProofReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_alternative_comparison_proof_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_strategy_ledger_sha256")]
    public string SourceStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_ledger_revision")]
    public int SourceLedgerRevision { get; set; }

    [JsonPropertyName("candidate_denominator_authoritative")]
    public bool CandidateDenominatorAuthoritative { get; set; }

    [JsonPropertyName("artifact_proof_verified")]
    public bool ArtifactProofVerified { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

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
    public MoneyRevenueAlternativeArtifactEvidence[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueAlternativeArtifactEvidence>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every alternative exactly rebuilds one complete acquisition rollout and one contiguous native Revenue recurrence. Each executed acquisition transition is rebound to its deterministic opportunity-cost report, reservation claims and atomic portfolio commit. All alternatives must share one exact source snapshot and strategy ledger, while the verified rollout terminal state must equal the Revenue prefix start. The manifest is not yet an authoritative enumeration of every feasible Revenue alternative, so this receipt never authorizes a production Teacher label or formal product training.";
}
