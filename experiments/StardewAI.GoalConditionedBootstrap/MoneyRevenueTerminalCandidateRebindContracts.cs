using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueTerminalCandidateRebindManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_candidate_rebind_manifest.v1";

    [JsonPropertyName("alternative_denominator_catalog_path")]
    public string AlternativeDenominatorCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("rebinds")]
    public MoneyRevenueTerminalCandidateRebindRequest[] Rebinds { get; set; } =
        Array.Empty<MoneyRevenueTerminalCandidateRebindRequest>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueTerminalCandidateRebindRequest
{
    [JsonPropertyName("alternative_id")]
    public string AlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_manifest_path")]
    public string AcquisitionRolloutProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("acquisition_rollout_proof_receipt_path")]
    public string AcquisitionRolloutProofReceiptPath { get; set; } =
        string.Empty;
}

public sealed record MoneyRevenueIncreasedInventorySlot(
    [property: JsonPropertyName("slot_index")] int SlotIndex,
    [property: JsonPropertyName("quality")] int Quality,
    [property: JsonPropertyName("before_quantity")] int BeforeQuantity,
    [property: JsonPropertyName("after_quantity")] int AfterQuantity,
    [property: JsonPropertyName("quantity_increase")] int QuantityIncrease);

public sealed class MoneyRevenueReboundTerminalCandidate
{
    [JsonPropertyName("concrete_alternative_id")]
    public string ConcreteAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_node_id")]
    public string TerminalNodeId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_candidate")]
    public MoneyRevenueCandidateCatalogEntry TerminalCandidate { get; set; } =
        new();

    [JsonPropertyName("concrete_alternative_sha256")]
    public string ConcreteAlternativeSha256 { get; set; } = string.Empty;
}

public sealed class MoneyRevenueTerminalCandidateRebindEvidence
{
    [JsonPropertyName("alternative_id")]
    public string AlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("skeleton_path_sha256")]
    public string SkeletonPathSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_catalog_candidate_id")]
    public string SourceCatalogCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("source_candidate_id")]
    public string SourceCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_option_id")]
    public string TerminalOptionId { get; set; } = string.Empty;

    [JsonPropertyName("proof_shape")]
    public string ProofShape { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("rollout_manifest_sha256")]
    public string RolloutManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_receipt_sha256")]
    public string RolloutReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("matched_transition_index")]
    public int MatchedTransitionIndex { get; set; }

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_snapshot_path")]
    public string TerminalSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_snapshot_sha256")]
    public string TerminalSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_state_hash")]
    public string TerminalStateHash { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("required_amount")]
    public int RequiredAmount { get; set; }

    [JsonPropertyName("minimum_quality")]
    public int MinimumQuality { get; set; }

    [JsonPropertyName("quantity_increase")]
    public int QuantityIncrease { get; set; }

    [JsonPropertyName("increased_inventory_slots")]
    public MoneyRevenueIncreasedInventorySlot[] IncreasedInventorySlots
    { get; set; } = Array.Empty<MoneyRevenueIncreasedInventorySlot>();

    [JsonPropertyName("fresh_candidate_catalog_sha256")]
    public string FreshCandidateCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_candidates")]
    public MoneyRevenueReboundTerminalCandidate[] TerminalCandidates
    { get; set; } = Array.Empty<MoneyRevenueReboundTerminalCandidate>();

    [JsonPropertyName("evidence_sha256")]
    public string EvidenceSha256 { get; set; } = string.Empty;
}

public sealed class MoneyRevenueTerminalCandidateRebindReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_candidate_rebind_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

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

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("required_rebind_count")]
    public int RequiredRebindCount { get; set; }

    [JsonPropertyName("verified_rebind_count")]
    public int VerifiedRebindCount { get; set; }

    [JsonPropertyName("concrete_terminal_candidate_count")]
    public int ConcreteTerminalCandidateCount { get; set; }

    [JsonPropertyName("terminal_rebind_denominator_complete")]
    public bool TerminalRebindDenominatorComplete { get; set; }

    [JsonPropertyName("current_alternative_denominator_authoritative")]
    public bool CurrentAlternativeDenominatorAuthoritative { get; set; }

    [JsonPropertyName("production_teacher_preference_label_eligible")]
    public bool ProductionTeacherPreferenceLabelEligible { get; set; }

    [JsonPropertyName("rebinds")]
    public MoneyRevenueTerminalCandidateRebindEvidence[] Rebinds { get; set; } =
        Array.Empty<MoneyRevenueTerminalCandidateRebindEvidence>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every skeleton that requires a fresh terminal rebind is represented exactly once. " +
        "Each rollout and receipt is rebuilt, the exact execution transition is selected by " +
        "the skeleton source candidate identity, and newly produced inventory slots are derived " +
        "from the verified before/after snapshots. The existing Revenue candidate evaluator is " +
        "then rerun on that terminal snapshot and every eligible sale or shipping candidate bound " +
        "to those new slots is retained. Callers cannot declare output items, slots, prices or " +
        "terminal candidates. This receipt does not yet union direct and rebound alternatives or " +
        "authorize a production Teacher label or formal product training.";
}
