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
        "money_revenue_crop_native_lifecycle_proof_request.v4";

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

    [JsonPropertyName("checkpoint_resumes")]
    public MoneyRevenueCropNativeLifecycleCheckpointResumeRequest[]
        CheckpointResumes { get; set; } =
            Array.Empty<MoneyRevenueCropNativeLifecycleCheckpointResumeRequest>();

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

public sealed record MoneyRevenueCropNativeLifecycleCheckpointResumeRequest(
    [property: JsonPropertyName("transition_index")] int TransitionIndex);

public sealed record MoneyRevenueCropNativeLifecycleCheckpointResumeProof(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("prior_after_state_hash")] string PriorAfterStateHash,
    [property: JsonPropertyName("resumed_before_state_hash")] string ResumedBeforeStateHash,
    [property: JsonPropertyName("normalized_state_sha256")] string NormalizedStateSha256,
    [property: JsonPropertyName("prior_run_id")] string PriorRunId,
    [property: JsonPropertyName("resumed_run_id")] string ResumedRunId,
    [property: JsonPropertyName("verified")] bool Verified);

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
    [property: JsonPropertyName("action_commitment_id")]
    string ActionCommitmentId,
    [property: JsonPropertyName("verified")] bool Verified);

public sealed class MoneyRevenueCropNativeLifecycleProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_native_lifecycle_proof.v4";

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

    [JsonPropertyName("shared_transition_denominator_commitment_ids")]
    public string[] SharedTransitionDenominatorCommitmentIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("shared_native_trajectory_verified")]
    public bool SharedNativeTrajectoryVerified { get; set; }

    [JsonPropertyName("transition_count")]
    public int TransitionCount { get; set; }

    [JsonPropertyName("crop_action_queue_count")]
    public int CropActionQueueCount { get; set; }

    [JsonPropertyName("supporting_route_queue_count")]
    public int SupportingRouteQueueCount { get; set; }

    [JsonPropertyName("crop_ingress_route_queue_count")]
    public int CropIngressRouteQueueCount { get; set; }

    [JsonPropertyName("sleep_ingress_route_queue_count")]
    public int SleepIngressRouteQueueCount { get; set; }

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

    [JsonPropertyName("checkpoint_resume_count")]
    public int CheckpointResumeCount { get; set; }

    [JsonPropertyName("checkpoint_resumes")]
    public MoneyRevenueCropNativeLifecycleCheckpointResumeProof[]
        CheckpointResumes { get; set; } =
            Array.Empty<MoneyRevenueCropNativeLifecycleCheckpointResumeProof>();

    [JsonPropertyName("native_crop_lifecycle_verified")]
    public bool NativeCropLifecycleVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "One continuous native world-state trajectory is shared by every crop " +
        "commitment in the exact production plan. Every crop action is admitted " +
        "only through an existing action queue and verified execution receipt. " +
        "Each homogeneous queue belongs to exactly one non-overlapping commitment " +
        "layout, while the complete queue set must cover every scheduled plant " +
        "and harvest operation. Scheduled watering must " +
        "leave every tile watered; already-watered rain tiles need no redundant " +
        "water action, while explicit water actions may target each tile at most " +
        "once. Explicit crop-ingress and sleep-ingress route queues may surround " +
        "the day's crop actions, but must use resource-neutral movement and " +
        "connector primitives, retain the same calendar day, preserve inventory, " +
        "currency and persistent world state, and end at the declared crop or " +
        "home location. Consecutive fresh snapshots must form one state-hash " +
        "chain. A declared checkpoint resume is allowed only after a verified " +
        "native rollover and before the next crop-ingress route, where complete " +
        "normalized save state is equal after excluding runtime identity and " +
        "transparent-read metadata. Inventory, money, crops, calendar and other " +
        "game state remain exact. Each overnight boundary " +
        "must be a single native sleep queue whose crop phase transition exactly " +
        "matches the decompiled Crop.newDay state machine. This proves lifecycle " +
        "execution only; harvested quantity, retention and terminal revenue remain " +
        "separate evidence obligations.";
}

public sealed class MoneyRevenueCropOutputRetentionProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_output_retention_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("native_lifecycle_proof_path")]
    public string NativeLifecycleProofPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("wave_index")]
    public int WaveIndex { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueCropOutputRetentionHarvestQueueEvidence(
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_path")] string QueuePath,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("execution_receipt_path")]
    string ExecutionReceiptPath,
    [property: JsonPropertyName("execution_receipt_sha256")]
    string ExecutionReceiptSha256,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("harvest_action_count")] int HarvestActionCount);

public sealed class MoneyRevenueCropOutputRetentionProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_output_retention_proof.v1";

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

    [JsonPropertyName("native_lifecycle_proof_path")]
    public string NativeLifecycleProofPath { get; set; } = string.Empty;

    [JsonPropertyName("native_lifecycle_proof_sha256")]
    public string NativeLifecycleProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("wave_index")]
    public int WaveIndex { get; set; }

    [JsonPropertyName("harvest_total_day")]
    public int HarvestTotalDay { get; set; }

    [JsonPropertyName("output_retention_key")]
    public string OutputRetentionKey { get; set; } = string.Empty;

    [JsonPropertyName("harvest_item_qualified_id")]
    public string HarvestItemQualifiedId { get; set; } = string.Empty;

    [JsonPropertyName("minimum_units")]
    public int MinimumUnits { get; set; }

    [JsonPropertyName("minimum_gross_revenue")]
    public long MinimumGrossRevenue { get; set; }

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_state_hash")]
    public string BeforeStateHash { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("inventory_quantity_before")]
    public int InventoryQuantityBefore { get; set; }

    [JsonPropertyName("inventory_quantity_after")]
    public int InventoryQuantityAfter { get; set; }

    [JsonPropertyName("inventory_quantity_increase")]
    public int InventoryQuantityIncrease { get; set; }

    [JsonPropertyName("harvest_queue_count")]
    public int HarvestQueueCount { get; set; }

    [JsonPropertyName("harvest_action_count")]
    public int HarvestActionCount { get; set; }

    [JsonPropertyName("harvest_queues")]
    public MoneyRevenueCropOutputRetentionHarvestQueueEvidence[] HarvestQueues
        { get; set; } =
            Array.Empty<MoneyRevenueCropOutputRetentionHarvestQueueEvidence>();

    [JsonPropertyName("native_output_retention_verified")]
    public bool NativeOutputRetentionVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "One proof covers one exact planned harvest wave. It independently " +
        "rebuilds the crop-production plan and native lifecycle proof, selects " +
        "only the contiguous verified harvest queues for that wave, requires " +
        "their action count to equal the complete committed tile denominator, " +
        "and verifies that the exact harvest item increases in player inventory " +
        "by at least the conservative minimum units. World debris is not retained " +
        "inventory and cannot satisfy this proof. Extra native yield is observed " +
        "but does not raise the planned minimum revenue bound. Settlement and " +
        "formal product training remain separate gates.";
}

public sealed class MoneyRevenueCropProductionProofFragmentIndex
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_fragment_index.v13";

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

    [JsonPropertyName("output_retention_proof_paths")]
    public string[] OutputRetentionProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("terminal_settlement_proof_paths")]
    public string[] TerminalSettlementProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("settlement_capacity_proof_paths")]
    public string[] SettlementCapacityProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("shared_shipping_settlement_proof_paths")]
    public string[] SharedShippingSettlementProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionProofCheckpointRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_proof_checkpoint_request.v12";

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
        "money_revenue_crop_production_proof_checkpoint.v12";

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

    [JsonPropertyName("verified_output_retentions")]
    public MoneyRevenueCropOutputRetentionProof[] VerifiedOutputRetentions
        { get; set; } = Array.Empty<MoneyRevenueCropOutputRetentionProof>();

    [JsonPropertyName("verified_terminal_settlements")]
    public MoneyRevenueCropTerminalSettlementProof[] VerifiedTerminalSettlements
        { get; set; } =
            Array.Empty<MoneyRevenueCropTerminalSettlementProof>();

    [JsonPropertyName("verified_settlement_capacities")]
    public MoneyRevenueCropSettlementCapacityProof[]
        VerifiedSettlementCapacities { get; set; } =
            Array.Empty<MoneyRevenueCropSettlementCapacityProof>();

    [JsonPropertyName("verified_shared_shipping_settlements")]
    public MoneyRevenueCropSharedShippingSettlementProof[]
        VerifiedSharedShippingSettlements { get; set; } =
            Array.Empty<MoneyRevenueCropSharedShippingSettlementProof>();

    [JsonPropertyName("future_crop_production_proof_ready")]
    public bool FutureCropProductionProofReady { get; set; }

    [JsonPropertyName("fresh_save_revenue_schedule_ready")]
    public bool FreshSaveRevenueScheduleReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("checkpoint_policy")]
    public string CheckpointPolicy { get; set; } =
        "Only independently rebuilt proof fragments inside the exact plan " +
        "denominator are verified. Version 12 admits planting-layout, exact " +
        "committed seed-reservation, exact shared-rollout seed-acquisition and " +
        "aggregate daily crop-capacity fragments plus receipt-bound native crop " +
        "lifecycles plus per-wave retained inventory support. Only a combined " +
        "native terminal-settlement proof can close an output-retention-terminal " +
        "request. Shipping terminal proofs additionally require one exact plan-" +
        "wide aggregate settlement-capacity support proof, which never increments " +
        "the request numerator. Multi-wave same-day shipping may instead close " +
        "its exact request set through one independently rebuilt shared deposit " +
        "chain and overnight recurrence. Every unsupported terminal " +
        "request remains pending, so this checkpoint cannot authorize future-crop " +
        "proof, a fresh-save revenue schedule or formal product training.";
}
