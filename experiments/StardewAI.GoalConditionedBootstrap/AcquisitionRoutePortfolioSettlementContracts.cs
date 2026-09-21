using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioSettlementReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_settlement_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("execution_binding_sha256")]
    public string ExecutionBindingSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_sha256")]
    public string BaseLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settlement_request_sha256")]
    public string SettlementRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settlement_result_sha256")]
    public string SettlementResultSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settled_ledger_sha256")]
    public string SettledLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("base_ledger_revision")]
    public int BaseLedgerRevision { get; set; }

    [JsonPropertyName("settled_ledger_revision")]
    public int SettledLedgerRevision { get; set; }

    [JsonPropertyName("completed_reservation_ids")]
    public string[] CompletedReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("fresh_terminal_receipt_verified")]
    public bool FreshTerminalReceiptVerified { get; set; }

    [JsonPropertyName("exact_settlement_replay_verified")]
    public bool ExactSettlementReplayVerified { get; set; }

    [JsonPropertyName("reservation_lifecycle_verified")]
    public bool ReservationLifecycleVerified { get; set; }

    [JsonPropertyName("fresh_replan_required")]
    public bool FreshReplanRequired { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A completed route settlement is admitted only after rebuilding the exact execution binding and fresh terminal receipt, deriving the complete active reservation set owned by that selected route, and exactly replaying the one-revision ledger mutation. The route's claims must all become completed with the terminal receipt hash while unrelated rows remain byte-for-byte deterministic under replay. A verified settlement requires a fresh portfolio replan and is not itself portfolio completion or formal training authorization.";
}
