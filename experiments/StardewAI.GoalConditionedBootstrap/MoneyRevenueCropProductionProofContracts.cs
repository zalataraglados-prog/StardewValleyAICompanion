using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropPlantingLayoutProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_planting_layout_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("planting_snapshot_path")]
    public string PlantingSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tiles")]
    public MoneyRevenueCropPlantingTile[] Tiles { get; set; } =
        Array.Empty<MoneyRevenueCropPlantingTile>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueCropPlantingTile(
    [property: JsonPropertyName("tile_x")] int TileX,
    [property: JsonPropertyName("tile_y")] int TileY);

public sealed class MoneyRevenueCropPlantingLayoutProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_planting_layout_proof.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

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

    [JsonPropertyName("planting_snapshot_path")]
    public string PlantingSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("planting_snapshot_sha256")]
    public string PlantingSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("planting_state_hash")]
    public string PlantingStateHash { get; set; } = string.Empty;

    [JsonPropertyName("planting_total_day")]
    public int PlantingTotalDay { get; set; }

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("tile_count")]
    public int TileCount { get; set; }

    [JsonPropertyName("tiles")]
    public MoneyRevenueCropPlantingTile[] Tiles { get; set; } =
        Array.Empty<MoneyRevenueCropPlantingTile>();

    [JsonPropertyName("planting_layout_verified")]
    public bool PlantingLayoutVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every selected tile is replayed against one fresh planting-day " +
        "transparent snapshot. The proof requires the exact save/player, Farm " +
        "outdoor seasonal context, an empty non-pot HoeDirt tile, the requested " +
        "seed's native planting permission and a growth duration no later than the " +
        "commitment's conservative deadline. It proves layout readiness only; it does not prove seed quantity, " +
        "daily capacity, lifecycle execution, retained output or terminal settlement.";
}

public sealed class MoneyRevenueCropProductionProofFragmentIndex
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_fragment_index.v4";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("planting_layout_proof_paths")]
    public string[] PlantingLayoutProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("seed_reservation_proof_paths")]
    public string[] SeedReservationProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("seed_acquisition_proof_paths")]
    public string[] SeedAcquisitionProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionProofCheckpointRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_checkpoint_request.v3";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_path")]
    public string FragmentIndexPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionProofCheckpoint
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_checkpoint.v3";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_path")]
    public string FragmentIndexPath { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_sha256")]
    public string FragmentIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }

    [JsonPropertyName("verified_fragment_count")]
    public int VerifiedFragmentCount { get; set; }

    [JsonPropertyName("pending_request_count")]
    public int PendingRequestCount { get; set; }

    [JsonPropertyName("verified_request_ids")]
    public string[] VerifiedRequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("pending_request_ids")]
    public string[] PendingRequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("verified_planting_layouts")]
    public MoneyRevenueCropPlantingLayoutProof[] VerifiedPlantingLayouts
        { get; set; } = Array.Empty<MoneyRevenueCropPlantingLayoutProof>();

    [JsonPropertyName("verified_seed_reservations")]
    public MoneyRevenueCropSeedReservationProof[] VerifiedSeedReservations
        { get; set; } = Array.Empty<MoneyRevenueCropSeedReservationProof>();

    [JsonPropertyName("verified_seed_acquisitions")]
    public MoneyRevenueCropSeedAcquisitionProof[] VerifiedSeedAcquisitions
        { get; set; } = Array.Empty<MoneyRevenueCropSeedAcquisitionProof>();

    [JsonPropertyName("future_crop_production_proof_ready")]
    public bool FutureCropProductionProofReady { get; set; }

    [JsonPropertyName("fresh_save_revenue_schedule_ready")]
    public bool FreshSaveRevenueScheduleReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("checkpoint_policy")]
    public string CheckpointPolicy { get; set; } =
        "Only independently rebuilt proof fragments inside the exact plan " +
        "denominator are verified. Version 3 admits planting-layout, exact " +
        "committed seed-reservation and exact shared-rollout seed-acquisition " +
        "fragments; every unsupported capacity, lifecycle, retention and terminal " +
        "request remains pending, so this checkpoint cannot authorize future-crop " +
        "proof, a fresh-save revenue schedule or formal product training.";
}
