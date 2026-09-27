using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteSupportingTransitionSettlementReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_supporting_transition_settlement_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("support_request_id")]
    public string SupportRequestId { get; set; } = string.Empty;

    [JsonPropertyName("route_occurrence_id")]
    public string RouteOccurrenceId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("support_request_sha256")]
    public string SupportRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("support_commit_receipt_sha256")]
    public string SupportCommitReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("supporting_transition_receipt_sha256")]
    public string SupportingTransitionReceiptSha256 { get; set; } =
        string.Empty;

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

    [JsonPropertyName("consumed_material_reservation_id")]
    public string ConsumedMaterialReservationId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_material_reservation_ids")]
    public string[] ConsumedMaterialReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("completed_material_reservation_ids")]
    public string[] CompletedMaterialReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("active_material_reservation_ids")]
    public string[] ActiveMaterialReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("relocated_material_reservation_ids")]
    public string[] RelocatedMaterialReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }

    [JsonPropertyName("material_settlements")]
    public ReservationPortfolioMaterialSettlement[] MaterialSettlements
    { get; set; } = Array.Empty<ReservationPortfolioMaterialSettlement>();

    [JsonPropertyName("material_relocations")]
    public ReservationPortfolioMaterialRelocation[] MaterialRelocations
    { get; set; } = Array.Empty<ReservationPortfolioMaterialRelocation>();

    [JsonPropertyName("completed_currency_reservation_ids")]
    public string[] CompletedCurrencyReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("active_currency_reservation_ids")]
    public string[] ActiveCurrencyReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("currency_settlements")]
    public ReservationPortfolioCurrencySettlement[] CurrencySettlements
    { get; set; } = Array.Empty<ReservationPortfolioCurrencySettlement>();

    [JsonPropertyName("rebound_active_reservation_ids")]
    public string[] ReboundActiveReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("supporting_transition_verified")]
    public bool SupportingTransitionVerified { get; set; }

    [JsonPropertyName("exact_settlement_replay_verified")]
    public bool ExactSettlementReplayVerified { get; set; }

    [JsonPropertyName("reservation_lifecycle_verified")]
    public bool ReservationLifecycleVerified { get; set; }

    [JsonPropertyName("route_terminal_completion_recorded")]
    public bool RouteTerminalCompletionRecorded { get; set; }

    [JsonPropertyName("fresh_replan_required")]
    public bool FreshReplanRequired { get; set; }

    [JsonPropertyName("terminal_receipt_eligible")]
    public bool TerminalReceiptEligible { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A supporting-transition settlement is admitted only after rebuilding the exact support request, atomic reservation commit, commit-gated compilation and fresh after-state receipt. Every material or currency consumption must match one verified before/after delta and one active route claim. Exact full consumption completes that claim; partial consumption decrements its quantity or amount and preserves it as active. A material-staging transition instead relocates one full active claim from the exact chest slot to the one observed player-inventory slot; partial, mixed, missing-destination or multi-slot relocation fails closed. Machine purchase route and interaction progress only rebinds active claims to the fresh state. A purchase consumes one exact unit price, rebinds every still-active material or currency claim, and never rebinds a fully consumed currency claim. The shared ledger mutation is exactly replayed and records one supporting-transition marker, never a terminal route-completion marker. Success requires a full fresh-snapshot replan and never authorizes formal training.";
}
