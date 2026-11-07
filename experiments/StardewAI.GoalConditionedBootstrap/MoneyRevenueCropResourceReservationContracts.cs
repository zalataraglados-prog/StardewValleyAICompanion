using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropResourceReservationReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("decision_request_path")]
    public string DecisionRequestPath { get; set; } = string.Empty;

    [JsonPropertyName("decision_request_sha256")]
    public string DecisionRequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("seed_reserved_quantity")]
    public int SeedReservedQuantity { get; set; }

    [JsonPropertyName("seed_acquisition_quantity")]
    public int SeedAcquisitionQuantity { get; set; }

    [JsonPropertyName("seed_acquisition_cost")]
    public int SeedAcquisitionCost { get; set; }

    [JsonPropertyName("reservation_request")]
    public ReservationPortfolioCommitRequest ReservationRequest { get; set; } = new();

    [JsonPropertyName("reservation_result")]
    public ReservationPortfolioCommitResult ReservationResult { get; set; } = new();

    [JsonPropertyName("resource_reservation_replay_verified")]
    public bool ResourceReservationReplayVerified { get; set; }

    [JsonPropertyName("runtime_ledger_installed")]
    public bool RuntimeLedgerInstalled { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("reservation_policy")]
    public string ReservationPolicy { get; set; } =
        "The original Teacher decision and crop commitment receipt are rebuilt, then current seed slots and the exact seed-purchase money shortfall are atomically reserved through the existing portfolio ledger service. The resulting planning ledger preserves other roots and can feed the existing production obligation builder. This is not proof of runtime ledger installation, seed acquisition, native outcomes, a fresh-save schedule or formal training. Purchase settlement and acquired-seed custody must be bound before managed execution.";
}
