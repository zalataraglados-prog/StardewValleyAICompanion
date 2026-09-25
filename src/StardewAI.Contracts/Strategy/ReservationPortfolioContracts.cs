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

    [JsonPropertyName("consumed_quantity")]
    public int ConsumedQuantity { get; set; }

    [JsonPropertyName("errors")]
    public string[] Errors { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ledger")]
    public StrategyCommitmentLedger? Ledger { get; set; }
}
