using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class CurrencyReservation
{
    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = StrategyCommitmentStatuses.Active;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("owner_player_id")]
    public long OwnerPlayerId { get; set; }

    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; set; }

    [JsonPropertyName("currency_key")]
    public string CurrencyKey { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("cancel_reason")]
    public string CancelReason { get; set; } = string.Empty;
}

public sealed class CurrencyReservationUpsertRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("reservation_id")]
    public string ReservationId { get; set; } = string.Empty;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("currency_id")]
    public int CurrencyId { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;
}
