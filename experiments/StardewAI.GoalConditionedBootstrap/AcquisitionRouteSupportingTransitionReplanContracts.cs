using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionSettlementProof
{
    public string SupportRequestPath { get; init; } = string.Empty;
    public string SupportCommitReceiptPath { get; init; } = string.Empty;
    public string CommittedLedgerPath { get; init; } = string.Empty;
    public string CommitResultPath { get; init; } = string.Empty;
    public string CompilationPath { get; init; } = string.Empty;
    public string ExecutionReceiptPath { get; init; } = string.Empty;
    public string AfterSnapshotPath { get; init; } = string.Empty;
    public string SupportingTransitionReceiptPath { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string ExecutorVersion { get; init; } = string.Empty;
    public string SettlementRequestPath { get; init; } = string.Empty;
    public string SettlementResultPath { get; init; } = string.Empty;
    public string SettledLedgerPath { get; init; } = string.Empty;
    public string SettlementReceiptPath { get; init; } = string.Empty;
}

public sealed class AcquisitionRouteSupportingTransitionReplanAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_supporting_transition_replan_admission.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("support_request_id")]
    public string SupportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("prior_queue_id")]
    public string PriorQueueId { get; set; } = string.Empty;

    [JsonPropertyName("prior_state_hash")]
    public string PriorStateHash { get; set; } = string.Empty;

    [JsonPropertyName("prior_ledger_revision")]
    public int PriorLedgerRevision { get; set; }

    [JsonPropertyName("fresh_state_hash")]
    public string FreshStateHash { get; set; } = string.Empty;

    [JsonPropertyName("fresh_ledger_revision")]
    public int FreshLedgerRevision { get; set; }

    [JsonPropertyName("support_settlement_receipt_sha256")]
    public string SupportSettlementReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_snapshot_sha256")]
    public string FreshSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_strategy_ledger_sha256")]
    public string FreshStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_requirement_inventory_sha256")]
    public string FreshRequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_opportunity_cost_sha256")]
    public string FreshOpportunityCostSha256 { get; set; } = string.Empty;

    [JsonPropertyName("requirement_set_id")]
    public string RequirementSetId { get; set; } = string.Empty;

    [JsonPropertyName("requirement_id")]
    public string RequirementId { get; set; } = string.Empty;

    [JsonPropertyName("all_target_date_axes_rebuilt")]
    public bool AllTargetDateAxesRebuilt { get; set; }

    [JsonPropertyName("prior_queue_invalidated")]
    public bool PriorQueueInvalidated { get; set; }

    [JsonPropertyName("fresh_teacher_request_ready")]
    public bool FreshTeacherRequestReady { get; set; }

    [JsonPropertyName("next_teacher_preference_request")]
    public AcquisitionRoutePortfolioTeacherPreferenceRequest?
        NextTeacherPreferenceRequest { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A support recurrence admission rebuilds the verified nonterminal settlement, then recomputes the complete target-date opportunity chain from the post-transition snapshot and settled ledger. The prior queue must remain bound to both the old state hash and old ledger revision, so no old queue item can survive. Only a fresh Teacher preference request scoped to the affected authoritative requirement is emitted; execution and formal training remain blocked until downstream selection and reservation commit are independently verified.";
}

internal sealed record AcquisitionRouteSupportingTransitionFreshContext(
    string GoalId,
    string StateHash,
    string SnapshotSha256,
    int LedgerRevision,
    string LedgerSha256,
    string RequirementInventorySha256,
    string OpportunityCostSha256,
    string RouteOccurrenceId,
    string RequirementSetId,
    string RequirementId,
    bool AllTargetDateAxesRebuilt);
