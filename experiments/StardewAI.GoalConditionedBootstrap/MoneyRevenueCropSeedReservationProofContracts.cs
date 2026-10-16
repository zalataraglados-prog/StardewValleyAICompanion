using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSeedReservationProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_reservation_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("reservation_snapshot_path")]
    public string ReservationSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("committed_strategy_ledger_path")]
    public string CommittedStrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropSeedReservationProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_seed_reservation_proof.v1";
    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;
    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;
    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;
    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;
    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;
    [JsonPropertyName("reservation_snapshot_path")]
    public string ReservationSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("reservation_snapshot_sha256")]
    public string ReservationSnapshotSha256 { get; set; } = string.Empty;
    [JsonPropertyName("committed_strategy_ledger_path")]
    public string CommittedStrategyLedgerPath { get; set; } = string.Empty;
    [JsonPropertyName("committed_strategy_ledger_sha256")]
    public string CommittedStrategyLedgerSha256 { get; set; } = string.Empty;
    [JsonPropertyName("reservation_state_hash")]
    public string ReservationStateHash { get; set; } = string.Empty;
    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("required_quantity")]
    public int RequiredQuantity { get; set; }
    [JsonPropertyName("reserved_quantity")]
    public int ReservedQuantity { get; set; }
    [JsonPropertyName("reservation_ids")]
    public string[] ReservationIds { get; set; } = Array.Empty<string>();
    [JsonPropertyName("seed_reservation_verified")]
    public bool SeedReservationVerified { get; set; }
    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
