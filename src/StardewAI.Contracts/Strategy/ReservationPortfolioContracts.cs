using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class ReservationPortfolioCommitRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("release_reservation_ids")]
    public string[] ReleaseReservationIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("material_claims")]
    public MaterialReservationUpsertRequest[] MaterialClaims { get; set; } =
        Array.Empty<MaterialReservationUpsertRequest>();

    [JsonPropertyName("currency_claims")]
    public CurrencyReservationUpsertRequest[] CurrencyClaims { get; set; } =
        Array.Empty<CurrencyReservationUpsertRequest>();

    [JsonPropertyName("machine_support_intent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MachineSupportIntentUpsertRequest? MachineSupportIntent
    { get; set; }
}

public sealed class ReservationPortfolioCommitResult
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("committed_ledger_revision")]
    public int? CommittedLedgerRevision { get; set; }

    [JsonPropertyName("material_claim_count")]
    public int MaterialClaimCount { get; set; }

    [JsonPropertyName("currency_claim_count")]
    public int CurrencyClaimCount { get; set; }

    [JsonPropertyName("machine_support_intent_id")]
    public string MachineSupportIntentId { get; set; } = string.Empty;

    [JsonPropertyName("released_reservation_ids")]
    public string[] ReleasedReservationIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("errors")]
    public string[] Errors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ledger")]
    public StrategyCommitmentLedger? Ledger { get; set; }
}

public sealed class ReservationPortfolioRouteSettlementRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("reservation_ids")]
    public string[] ReservationIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReservationPortfolioRouteSettlementResult
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("fresh_terminal_receipt_sha256")]
    public string FreshTerminalReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("committed_ledger_revision")]
    public int? CommittedLedgerRevision { get; set; }

    [JsonPropertyName("completed_reservation_ids")]
    public string[] CompletedReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("errors")]
    public string[] Errors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ledger")]
    public StrategyCommitmentLedger? Ledger { get; set; }
}

public sealed class ReservationPortfolioSupportingTransitionSettlementRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("supporting_transition_receipt_sha256")]
    public string SupportingTransitionReceiptSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("material_consumptions")]
    public ReservationPortfolioMaterialConsumption[] MaterialConsumptions
    { get; set; } = Array.Empty<ReservationPortfolioMaterialConsumption>();

    [JsonPropertyName("material_relocations")]
    public ReservationPortfolioMaterialRelocation[] MaterialRelocations
    { get; set; } = Array.Empty<ReservationPortfolioMaterialRelocation>();

    [JsonPropertyName("currency_consumptions")]
    public ReservationPortfolioCurrencyConsumption[] CurrencyConsumptions
    { get; set; } = Array.Empty<ReservationPortfolioCurrencyConsumption>();

    [JsonPropertyName("rebind_active_reservation_ids")]
    public string[] RebindActiveReservationIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("machine_support_intent_id")]
    public string MachineSupportIntentId { get; set; } = string.Empty;

    [JsonPropertyName("machine_support_intent_stage")]
    public string MachineSupportIntentStage { get; set; } = string.Empty;

    [JsonPropertyName("machine_support_sources_json")]
    public string MachineSupportSourcesJson { get; set; } = "[]";

    // Legacy single-consumption representation. New producers use
    // material_consumptions and must not mix the two forms.
    [JsonPropertyName("material_reservation_id")]
    public string MaterialReservationId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReservationPortfolioMaterialConsumption
{
    [JsonPropertyName("material_reservation_id")]
    public string MaterialReservationId { get; set; } = string.Empty;

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }
}

public sealed class ReservationPortfolioMaterialRelocation
{
    [JsonPropertyName("material_reservation_id")]
    public string MaterialReservationId { get; set; } = string.Empty;

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_slot_index")]
    public int SourceSlotIndex { get; set; }

    [JsonPropertyName("destination_node_id")]
    public string DestinationNodeId { get; set; } = string.Empty;

    [JsonPropertyName("destination_slot_index")]
    public int DestinationSlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}

public sealed class ReservationPortfolioCurrencyConsumption
{
    [JsonPropertyName("currency_reservation_id")]
    public string CurrencyReservationId { get; set; } = string.Empty;

    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; set; }

    [JsonPropertyName("consumed_amount")]
    public int ConsumedAmount { get; set; }
}

public sealed class ReservationPortfolioSupportingTransitionSettlementResult
{
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    [JsonPropertyName("portfolio_id")]
    public string PortfolioId { get; set; } = string.Empty;

    [JsonPropertyName("route_source_decision_id")]
    public string RouteSourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("supporting_transition_receipt_sha256")]
    public string SupportingTransitionReceiptSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("committed_ledger_revision")]
    public int? CommittedLedgerRevision { get; set; }

    [JsonPropertyName("completed_material_reservation_id")]
    public string CompletedMaterialReservationId { get; set; } = string.Empty;

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

    [JsonPropertyName("rebound_machine_support_intent_id")]
    public string ReboundMachineSupportIntentId { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public string[] Errors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ledger")]
    public StrategyCommitmentLedger? Ledger { get; set; }
}

public sealed class ReservationPortfolioMaterialSettlement
{
    [JsonPropertyName("material_reservation_id")]
    public string MaterialReservationId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }

    [JsonPropertyName("remaining_quantity")]
    public int RemainingQuantity { get; set; }

    [JsonPropertyName("reservation_status")]
    public string ReservationStatus { get; set; } = string.Empty;
}

public sealed class ReservationPortfolioCurrencySettlement
{
    [JsonPropertyName("currency_reservation_id")]
    public string CurrencyReservationId { get; set; } = string.Empty;

    [JsonPropertyName("consumed_amount")]
    public int ConsumedAmount { get; set; }

    [JsonPropertyName("remaining_amount")]
    public int RemainingAmount { get; set; }

    [JsonPropertyName("reservation_status")]
    public string ReservationStatus { get; set; } = string.Empty;
}
