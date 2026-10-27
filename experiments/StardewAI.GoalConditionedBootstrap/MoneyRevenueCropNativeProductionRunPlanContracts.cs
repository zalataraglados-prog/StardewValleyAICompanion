using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropNativeProductionRunPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_native_production_run_plan_request.v2";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("layout_intents")]
    public MoneyRevenueCropNativeProductionLayoutIntent[] LayoutIntents
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeProductionLayoutIntent>();

    [JsonPropertyName("seed_acquisition_intents")]
    public MoneyRevenueCropNativeSeedAcquisitionIntent[] SeedAcquisitionIntents
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeSeedAcquisitionIntent>();

    [JsonPropertyName("max_queue_item_count")]
    public int MaxQueueItemCount { get; set; } = 8;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueCropNativeProductionLayoutIntent(
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("location_id")] string LocationId,
    [property: JsonPropertyName("tiles")]
    MoneyRevenueCropPlantingTile[] Tiles,
    [property: JsonPropertyName("watering_refill_preparation_tiles")]
    MoneyRevenueCropPlantingTile[]? WateringRefillPreparationTiles = null,
    [property: JsonPropertyName("watering_refill_approach_tile")]
    MoneyRevenueCropPlantingTile? WateringRefillApproachTile = null);

public sealed record MoneyRevenueCropNativeSeedAcquisitionIntent(
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("seed_qualified_item_id")]
    string SeedQualifiedItemId,
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("max_unit_price")] int MaxUnitPrice);

public sealed record MoneyRevenueCropNativeProductionActionBatch(
    [property: JsonPropertyName("batch_index")] int BatchIndex,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("execution_condition")]
    string ExecutionCondition,
    [property: JsonPropertyName("location_id")] string LocationId,
    [property: JsonPropertyName("seed_qualified_item_id")]
    string SeedQualifiedItemId,
    [property: JsonPropertyName("tiles")]
    MoneyRevenueCropPlantingTile[] Tiles);

public sealed record MoneyRevenueCropNativeSeedAcquisition(
    [property: JsonPropertyName("acquisition_index")] int AcquisitionIndex,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("seed_qualified_item_id")]
    string SeedQualifiedItemId,
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("max_unit_price")] int MaxUnitPrice,
    [property: JsonPropertyName("required_quantity")] int RequiredQuantity,
    [property: JsonPropertyName("deadline_total_day")] int DeadlineTotalDay,
    [property: JsonPropertyName("execution_policy")] string ExecutionPolicy);

public sealed record MoneyRevenueCropNativeProductionRunDay(
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("capture_layout_commitment_ids")]
    string[] CaptureLayoutCommitmentIds,
    [property: JsonPropertyName("capture_daily_capacity_snapshot")]
    bool CaptureDailyCapacitySnapshot,
    [property: JsonPropertyName("action_batches")]
    MoneyRevenueCropNativeProductionActionBatch[] ActionBatches,
    [property: JsonPropertyName("native_sleep_required")]
    bool NativeSleepRequired);

public sealed class MoneyRevenueCropNativeProductionRunPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_native_production_run_plan.v3";

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

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_total_day")]
    public int SourceTotalDay { get; set; }

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("covered_commitment_ids")]
    public string[] CoveredCommitmentIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("first_total_day")]
    public int FirstTotalDay { get; set; }

    [JsonPropertyName("last_total_day")]
    public int LastTotalDay { get; set; }

    [JsonPropertyName("day_count")]
    public int DayCount { get; set; }

    [JsonPropertyName("action_batch_count")]
    public int ActionBatchCount { get; set; }

    [JsonPropertyName("action_count")]
    public int ActionCount { get; set; }

    [JsonPropertyName("max_queue_item_count")]
    public int MaxQueueItemCount { get; set; }

    [JsonPropertyName("seed_acquisition_count")]
    public int SeedAcquisitionCount { get; set; }

    [JsonPropertyName("seed_acquisitions")]
    public MoneyRevenueCropNativeSeedAcquisition[] SeedAcquisitions
        { get; set; } = Array.Empty<MoneyRevenueCropNativeSeedAcquisition>();

    [JsonPropertyName("days")]
    public MoneyRevenueCropNativeProductionRunDay[] Days { get; set; } =
        Array.Empty<MoneyRevenueCropNativeProductionRunDay>();

    [JsonPropertyName("native_production_run_ready")]
    public bool NativeProductionRunReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("planning_policy")]
    public string PlanningPolicy { get; set; } =
        "The exact crop commitments and non-overlapping layout intents are " +
        "expanded once from the exact source snapshot into a continuous native-day " +
        "schedule. Exact seed shortfalls are explicit pre-production obligations, " +
        "executed as bounded one-unit native purchase slices through the existing " +
        "economy.buy_supplies candidate, planner and compiler chain. Pre-plant days " +
        "otherwise contain only one native sleep boundary. Plant, conditional " +
        "water and harvest batches preserve compiler order and the configured " +
        "queue limit; one native sleep closes every non-terminal day. Runtime " +
        "must keep every commitment inside one bounded farm-layout projection, " +
        "rebuild the existing site-preparation plan after every preparation " +
        "action, retain the Teacher-verified watering-refill approach so later " +
        "route repair can emit explicit move and clearance actions, and only " +
        "then capture planting-layout and daily-capacity " +
        "snapshots at the marked boundaries. It compiles each batch through " +
        "the existing action compiler and retains every queue receipt. This is " +
        "orchestration, not evidence, a " +
        "second crop planner, a model label or formal training authorization.";
}
