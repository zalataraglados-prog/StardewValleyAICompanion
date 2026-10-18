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

public sealed class MoneyRevenueCropNativeLifecycleProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_native_lifecycle_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("planting_layout_proof_path")]
    public string PlantingLayoutProofPath { get; set; } = string.Empty;

    [JsonPropertyName("daily_capacity_proof_path")]
    public string DailyCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("transitions")]
    public MoneyRevenueCropNativeLifecycleTransitionRequest[] Transitions
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeLifecycleTransitionRequest>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueCropNativeLifecycleTransitionRequest(
    [property: JsonPropertyName("transition_kind")] string TransitionKind,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("queue_path")] string QueuePath,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("executor_version")] string ExecutorVersion,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId);

public sealed record MoneyRevenueCropNativeLifecycleTransitionProof(
    [property: JsonPropertyName("transition_kind")] string TransitionKind,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("queue_path")] string QueuePath,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")] string BeforeSnapshotSha256,
    [property: JsonPropertyName("execution_receipt_path")] string ExecutionReceiptPath,
    [property: JsonPropertyName("execution_receipt_sha256")] string ExecutionReceiptSha256,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")] string AfterSnapshotSha256,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("item_count")] int ItemCount,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("verified")] bool Verified);

public sealed class MoneyRevenueCropNativeLifecycleProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_native_lifecycle_proof.v1";

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

    [JsonPropertyName("planting_layout_proof_path")]
    public string PlantingLayoutProofPath { get; set; } = string.Empty;

    [JsonPropertyName("planting_layout_proof_sha256")]
    public string PlantingLayoutProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("daily_capacity_proof_path")]
    public string DailyCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("daily_capacity_proof_sha256")]
    public string DailyCapacityProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("seed_qualified_item_id")]
    public string SeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("crop_action_queue_count")]
    public int CropActionQueueCount { get; set; }

    [JsonPropertyName("native_day_rollover_count")]
    public int NativeDayRolloverCount { get; set; }

    [JsonPropertyName("verified_plant_action_count")]
    public int VerifiedPlantActionCount { get; set; }

    [JsonPropertyName("verified_water_action_count")]
    public int VerifiedWaterActionCount { get; set; }

    [JsonPropertyName("verified_harvest_action_count")]
    public int VerifiedHarvestActionCount { get; set; }

    [JsonPropertyName("transitions")]
    public MoneyRevenueCropNativeLifecycleTransitionProof[] Transitions
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeLifecycleTransitionProof>();

    [JsonPropertyName("native_crop_lifecycle_verified")]
    public bool NativeCropLifecycleVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every crop action is admitted only through an existing action queue " +
        "and verified execution receipt. Homogeneous queues of at most the " +
        "training queue limit must cover each committed tile exactly once for " +
        "every scheduled plant and harvest operation. Scheduled watering must " +
        "leave every tile watered; already-watered rain tiles need no redundant " +
        "water action, while explicit water actions may target each tile at most " +
        "once. Consecutive fresh " +
        "snapshots must form one state-hash chain, while each overnight boundary " +
        "must be a single native sleep queue whose crop phase transition exactly " +
        "matches the decompiled Crop.newDay state machine. This proves lifecycle " +
        "execution only; harvested quantity, retention and terminal revenue remain " +
        "separate evidence obligations.";
}

public sealed class MoneyRevenueCropProductionProofFragmentIndex
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_fragment_index.v6";

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

    [JsonPropertyName("daily_capacity_proof_paths")]
    public string[] DailyCapacityProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("native_lifecycle_proof_paths")]
    public string[] NativeLifecycleProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionProofCheckpointRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_checkpoint_request.v5";

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
        "money_revenue_crop_production_proof_checkpoint.v5";

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

    [JsonPropertyName("verified_daily_capacities")]
    public MoneyRevenueCropDailyCapacityProof[] VerifiedDailyCapacities
        { get; set; } = Array.Empty<MoneyRevenueCropDailyCapacityProof>();

    [JsonPropertyName("verified_native_lifecycles")]
    public MoneyRevenueCropNativeLifecycleProof[] VerifiedNativeLifecycles
        { get; set; } = Array.Empty<MoneyRevenueCropNativeLifecycleProof>();

    [JsonPropertyName("future_crop_production_proof_ready")]
    public bool FutureCropProductionProofReady { get; set; }

    [JsonPropertyName("fresh_save_revenue_schedule_ready")]
    public bool FreshSaveRevenueScheduleReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("checkpoint_policy")]
    public string CheckpointPolicy { get; set; } =
        "Only independently rebuilt proof fragments inside the exact plan " +
        "denominator are verified. Version 5 admits planting-layout, exact " +
        "committed seed-reservation, exact shared-rollout seed-acquisition and " +
        "aggregate daily crop-capacity fragments plus receipt-bound native crop " +
        "lifecycles; every unsupported retention and terminal " +
        "request remains pending, so this checkpoint cannot authorize future-crop " +
        "proof, a fresh-save revenue schedule or formal product training.";
}
