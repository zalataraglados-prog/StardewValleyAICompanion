using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropDailyCapacityProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_daily_capacity_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("planting_layout_proof_paths")]
    public string[] PlantingLayoutProofPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("daily_snapshot_paths")]
    public string[] DailySnapshotPaths { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("watering_can_refill_receipts")]
    public MoneyRevenueCropDailyCapacityRefillReceiptRequest[]
        WateringCanRefillReceipts { get; set; } =
            Array.Empty<MoneyRevenueCropDailyCapacityRefillReceiptRequest>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropDailyCapacityProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_daily_capacity_proof.v1";

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

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("covered_commitment_ids")]
    public string[] CoveredCommitmentIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("planting_layout_proof_sha256_by_commitment")]
    public MoneyRevenueCropDailyCapacityLayoutBinding[]
        PlantingLayoutProofSha256ByCommitment { get; set; } =
            Array.Empty<MoneyRevenueCropDailyCapacityLayoutBinding>();

    [JsonPropertyName("required_day_count")]
    public int RequiredDayCount { get; set; }

    [JsonPropertyName("days")]
    public MoneyRevenueCropDailyCapacityDay[] Days { get; set; } =
        Array.Empty<MoneyRevenueCropDailyCapacityDay>();

    [JsonPropertyName("verified_watering_can_refill_receipts")]
    public MoneyRevenueCropDailyCapacityRefillReceiptProof[]
        VerifiedWateringCanRefillReceipts { get; set; } =
            Array.Empty<MoneyRevenueCropDailyCapacityRefillReceiptProof>();

    [JsonPropertyName("daily_capacity_verified")]
    public bool DailyCapacityVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "Every active crop commitment in the exact production plan must have one " +
        "independently rebuilt planting-layout proof. Their tiles may not overlap. " +
        "For every planting, conservative watering, or harvest day across that full " +
        "plan, one fresh same-save snapshot supplies current time, energy, exact " +
        "vanilla power-zero WateringCan state and the current Farm collision grid. " +
        "The shared BFS route and compiler-owned plant, water, harvest, movement and " +
        "game-clock policies rebuild one aggregate day budget. Rain-satisfied crop " +
        "tiles require no native watering action. A depleted watering can is admitted " +
        "only when an exact same-state native queue receipt proves a successful refill " +
        "and its conservative actual ticks fit the day budget; otherwise version 1 " +
        "fails closed. This proves aggregate crop-maintenance capacity only. It does not " +
        "prove native lifecycle transitions, retained output, settlement, a fresh-save " +
        "revenue schedule or formal training authorization.";
}

public sealed record MoneyRevenueCropDailyCapacityLayoutBinding(
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("planting_layout_proof_path")]
    string PlantingLayoutProofPath,
    [property: JsonPropertyName("planting_layout_proof_sha256")]
    string PlantingLayoutProofSha256,
    [property: JsonPropertyName("raised_crop")] bool RaisedCrop);

public sealed record MoneyRevenueCropDailyCapacityRefillReceiptRequest(
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("execution_receipt_path")]
    string ExecutionReceiptPath);

public sealed record MoneyRevenueCropDailyCapacityRefillReceiptProof(
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("execution_receipt_path")]
    string ExecutionReceiptPath,
    [property: JsonPropertyName("execution_receipt_sha256")]
    string ExecutionReceiptSha256,
    [property: JsonPropertyName("refill_actual_ticks")]
    int RefillActualTicks);

public sealed record MoneyRevenueCropDailyCapacityDay(
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("snapshot_path")] string SnapshotPath,
    [property: JsonPropertyName("snapshot_sha256")] string SnapshotSha256,
    [property: JsonPropertyName("snapshot_state_hash")]
    string SnapshotStateHash,
    [property: JsonPropertyName("snapshot_start_time")]
    int SnapshotStartTime,
    [property: JsonPropertyName("available_game_minutes")]
    int AvailableGameMinutes,
    [property: JsonPropertyName("plant_action_count")]
    int PlantActionCount,
    [property: JsonPropertyName("water_action_count")]
    int WaterActionCount,
    [property: JsonPropertyName("harvest_action_count")]
    int HarvestActionCount,
    [property: JsonPropertyName("movement_tile_count")]
    int MovementTileCount,
    [property: JsonPropertyName("movement_ticks")] int MovementTicks,
    [property: JsonPropertyName("action_ticks")] int ActionTicks,
    [property: JsonPropertyName("total_ticks")] int TotalTicks,
    [property: JsonPropertyName("required_game_minutes")]
    int RequiredGameMinutes,
    [property: JsonPropertyName("available_energy")]
    double AvailableEnergy,
    [property: JsonPropertyName("required_energy")]
    double RequiredEnergy,
    [property: JsonPropertyName("minimum_energy_reserve")]
    double MinimumEnergyReserve,
    [property: JsonPropertyName("watering_can_slot_index")]
    int? WateringCanSlotIndex,
    [property: JsonPropertyName("watering_can_water_left")]
    int? WateringCanWaterLeft,
    [property: JsonPropertyName("watering_can_capacity")]
    int? WateringCanCapacity,
    [property: JsonPropertyName("watering_can_bottomless")]
    bool? WateringCanBottomless,
    [property: JsonPropertyName("watering_can_refill_route_verified")]
    bool WateringCanRefillRouteVerified,
    [property: JsonPropertyName("watering_can_refill_actual_ticks")]
    int WateringCanRefillActualTicks,
    [property: JsonPropertyName("time_budget_fits")]
    bool TimeBudgetFits,
    [property: JsonPropertyName("energy_budget_fits")]
    bool EnergyBudgetFits,
    [property: JsonPropertyName("water_budget_fits")]
    bool WaterBudgetFits,
    [property: JsonPropertyName("all_targets_reachable")]
    bool AllTargetsReachable);
