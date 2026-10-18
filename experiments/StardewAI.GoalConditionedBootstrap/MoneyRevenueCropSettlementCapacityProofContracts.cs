using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSettlementCapacityProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_settlement_capacity_proof_request.v1";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("daily_capacity_proof_path")]
    public string DailyCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropSettlementCapacityProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_settlement_capacity_proof.v1";

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

    [JsonPropertyName("daily_capacity_proof_path")]
    public string DailyCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("daily_capacity_proof_sha256")]
    public string DailyCapacityProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("settlement_capacity_id")]
    public string SettlementCapacityId { get; set; } = string.Empty;

    [JsonPropertyName("covered_terminal_request_ids")]
    public string[] CoveredTerminalRequestIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("settlement_day_count")]
    public int SettlementDayCount { get; set; }

    [JsonPropertyName("days")]
    public MoneyRevenueCropSettlementCapacityDay[] Days { get; set; } =
        Array.Empty<MoneyRevenueCropSettlementCapacityDay>();

    [JsonPropertyName("aggregate_settlement_capacity_verified")]
    public bool AggregateSettlementCapacityVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "This support proof rebuilds the exact crop-production plan and its " +
        "aggregate maintenance capacity proof, groups every conservative harvest " +
        "wave by native settlement day, and reuses the shared crop BFS endpoint, " +
        "shipping-bin route projection, compiler shipping tick constant and " +
        "recovery cutoff. Each conservative unit requires one single-item native " +
        "deposit transition and one fresh replan queue. This artifact proves " +
        "aggregate same-day shipping capacity only; it closes no denominator " +
        "request and cannot replace native retention, deposit, overnight " +
        "settlement, production collection or formal training evidence.";
}

public sealed record MoneyRevenueCropSettlementCapacityWave(
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("wave_index")] int WaveIndex,
    [property: JsonPropertyName("evidence_request_id")]
        string EvidenceRequestId,
    [property: JsonPropertyName("minimum_units")] int MinimumUnits,
    [property: JsonPropertyName("minimum_gross_revenue")]
        long MinimumGrossRevenue);

public sealed record MoneyRevenueCropSettlementCapacityDay(
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("snapshot_path")] string SnapshotPath,
    [property: JsonPropertyName("snapshot_sha256")] string SnapshotSha256,
    [property: JsonPropertyName("snapshot_state_hash")]
        string SnapshotStateHash,
    [property: JsonPropertyName("waves")]
        MoneyRevenueCropSettlementCapacityWave[] Waves,
    [property: JsonPropertyName("minimum_shipping_units")]
        int MinimumShippingUnits,
    [property: JsonPropertyName("minimum_gross_revenue")]
        long MinimumGrossRevenue,
    [property: JsonPropertyName("existing_maintenance_ticks")]
        int ExistingMaintenanceTicks,
    [property: JsonPropertyName("tail_movement_tile_count")]
        int TailMovementTileCount,
    [property: JsonPropertyName("tail_movement_ticks")]
        int TailMovementTicks,
    [property: JsonPropertyName("shipping_action_ticks")]
        int ShippingActionTicks,
    [property: JsonPropertyName("required_queue_count")]
        int RequiredQueueCount,
    [property: JsonPropertyName("queue_item_count_per_transition")]
        int QueueItemCountPerTransition,
    [property: JsonPropertyName("total_ticks")] int TotalTicks,
    [property: JsonPropertyName("required_game_minutes")]
        int RequiredGameMinutes,
    [property: JsonPropertyName("available_game_minutes")]
        int AvailableGameMinutes,
    [property: JsonPropertyName("shipping_bin_tile_x")]
        int ShippingBinTileX,
    [property: JsonPropertyName("shipping_bin_tile_y")]
        int ShippingBinTileY,
    [property: JsonPropertyName("shipping_stand_tile_x")]
        int ShippingStandTileX,
    [property: JsonPropertyName("shipping_stand_tile_y")]
        int ShippingStandTileY,
    [property: JsonPropertyName("time_budget_fits")] bool TimeBudgetFits,
    [property: JsonPropertyName("all_targets_reachable")]
        bool AllTargetsReachable,
    [property: JsonPropertyName("shipping_bin_reachable")]
        bool ShippingBinReachable);
