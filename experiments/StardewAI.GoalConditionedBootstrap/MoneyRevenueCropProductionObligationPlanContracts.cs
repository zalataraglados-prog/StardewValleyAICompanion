using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropProductionObligationPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_obligation_plan_request.v1";

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; } = 224;

    [JsonPropertyName("target_total_money_earned")]
    public long TargetTotalMoneyEarned { get; set; } = 1_000_000;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionObligationPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_obligation_plan.v2";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; }

    [JsonPropertyName("target_total_money_earned")]
    public long TargetTotalMoneyEarned { get; set; }

    [JsonPropertyName("active_commitment_count")]
    public int ActiveCommitmentCount { get; set; }

    [JsonPropertyName("minimum_gross_revenue")]
    public long MinimumGrossRevenue { get; set; }

    [JsonPropertyName("minimum_gross_revenue_reaches_target")]
    public bool MinimumGrossRevenueReachesTarget { get; set; }

    [JsonPropertyName("commitments")]
    public MoneyRevenueCropProductionCommitmentPlan[] Commitments { get; set; } =
        Array.Empty<MoneyRevenueCropProductionCommitmentPlan>();

    [JsonPropertyName("pending_evidence_request_ids")]
    public string[] PendingEvidenceRequestIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("future_crop_production_proof_ready")]
    public bool FutureCropProductionProofReady { get; set; }

    [JsonPropertyName("fresh_save_revenue_schedule_ready")]
    public bool FreshSaveRevenueScheduleReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("planning_policy")]
    public string PlanningPolicy { get; set; } =
        "This artifact independently replays every active crop commitment against the exact transparent crop catalog and the shared crop-production obligation projector. It enumerates planting-layout binding, seed reservation/acquisition, planting, every conservative watering day, every minimum harvest wave, output retention and terminal settlement evidence without creating a second crop executor. It is a collection plan, not proof: pending layout, native lifecycle, daily capacity, reservation and terminal evidence cannot authorize Money labels or formal training.";
}

public sealed record MoneyRevenueCropProductionCommitmentPlan(
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("seed_qualified_item_id")]
    string SeedQualifiedItemId,
    [property: JsonPropertyName("harvest_item_qualified_id")]
    string HarvestItemQualifiedId,
    [property: JsonPropertyName("tile_count")] int TileCount,
    [property: JsonPropertyName("planting_total_day")] int PlantingTotalDay,
    [property: JsonPropertyName("planting_season")] string PlantingSeason,
    [property: JsonPropertyName("base_grow_days")] int BaseGrowDays,
    [property: JsonPropertyName("seed_supply_deadline_total_day")]
    int SeedSupplyDeadlineTotalDay,
    [property: JsonPropertyName("current_unreserved_seed_quantity")]
    int CurrentUnreservedSeedQuantity,
    [property: JsonPropertyName("current_reserved_seed_quantity")]
    int CurrentReservedSeedQuantity,
    [property: JsonPropertyName("seed_quantity_shortfall")]
    int SeedQuantityShortfall,
    [property: JsonPropertyName("seed_supply_status")] string SeedSupplyStatus,
    [property: JsonPropertyName("required_watering_total_days")]
    int[] RequiredWateringTotalDays,
    [property: JsonPropertyName("harvest_waves")]
    MoneyRevenueCropHarvestWaveObligation[] HarvestWaves,
    [property: JsonPropertyName("minimum_total_units")]
    int MinimumTotalUnits,
    [property: JsonPropertyName("minimum_gross_revenue")]
    long MinimumGrossRevenue,
    [property: JsonPropertyName("pending_evidence_request_ids")]
    string[] PendingEvidenceRequestIds);

public sealed record MoneyRevenueCropHarvestWaveObligation(
    [property: JsonPropertyName("wave_index")] int WaveIndex,
    [property: JsonPropertyName("harvest_total_day")] int HarvestTotalDay,
    [property: JsonPropertyName("minimum_units")] int MinimumUnits,
    [property: JsonPropertyName("minimum_gross_revenue")]
    long MinimumGrossRevenue,
    [property: JsonPropertyName("output_retention_key")]
    string OutputRetentionKey);
