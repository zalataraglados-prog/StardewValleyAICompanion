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
