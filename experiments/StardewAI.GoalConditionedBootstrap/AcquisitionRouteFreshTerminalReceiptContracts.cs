using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteFreshTerminalReceiptAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_fresh_terminal_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("execution_binding_sha256")]
    public string ExecutionBindingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_receipt_kind")]
    public string TerminalReceiptKind { get; set; } = string.Empty;

    [JsonPropertyName("terminal_transition")]
    public AcquisitionTerminalTransitionEvidence? TerminalTransition { get; set; }

    [JsonPropertyName("queue_execution_verified")]
    public bool QueueExecutionVerified { get; set; }

    [JsonPropertyName("fresh_terminal_receipt_verified")]
    public bool FreshTerminalReceiptVerified { get; set; }

    [JsonPropertyName("route_training_evidence_eligible")]
    public bool RouteTrainingEvidenceEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "Fresh terminal evidence exists only after the exact hash-bound route queue has executed. The queue receipt must prove every ordered primitive against fresh same-save snapshots, and the terminal transition must independently prove the complete required quantity/quality increase or the exact native community-center payment transition. Predicted receipts, partial quantity gains, stale snapshots and identity-only route guesses are rejected. A verified route receipt is eligible as route-level evidence only; route-set composition and the formal training controller remain separate authorization boundaries.";
}

public sealed class AcquisitionTerminalTransitionEvidence
{
    [JsonPropertyName("transition_kind")]
    public string TransitionKind { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("required_amount")]
    public int RequiredAmount { get; set; }

    [JsonPropertyName("minimum_quality")]
    public int MinimumQuality { get; set; }

    [JsonPropertyName("before_quantity")]
    public int? BeforeQuantity { get; set; }

    [JsonPropertyName("after_quantity")]
    public int? AfterQuantity { get; set; }

    [JsonPropertyName("quantity_increase")]
    public int? QuantityIncrease { get; set; }

    [JsonPropertyName("before_money")]
    public int? BeforeMoney { get; set; }

    [JsonPropertyName("after_money")]
    public int? AfterMoney { get; set; }

    [JsonPropertyName("money_decrease")]
    public int? MoneyDecrease { get; set; }

    [JsonPropertyName("before_native_completion")]
    public bool? BeforeNativeCompletion { get; set; }

    [JsonPropertyName("after_native_completion")]
    public bool? AfterNativeCompletion { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}
