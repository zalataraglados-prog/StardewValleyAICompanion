using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSitePreparationPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_site_preparation_plan_request.v2";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("preparation_snapshot_path")]
    public string PreparationSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tiles")]
    public MoneyRevenueCropPlantingTile[] Tiles { get; set; } =
        Array.Empty<MoneyRevenueCropPlantingTile>();

    [JsonPropertyName("watering_refill_preparation_tiles")]
    public MoneyRevenueCropPlantingTile[] WateringRefillPreparationTiles
        { get; set; } = Array.Empty<MoneyRevenueCropPlantingTile>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropSitePreparationPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_site_preparation_plan.v1";

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

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("preparation_snapshot_path")]
    public string PreparationSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("preparation_snapshot_sha256")]
    public string PreparationSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("preparation_state_hash")]
    public string PreparationStateHash { get; set; } = string.Empty;

    [JsonPropertyName("preparation_total_day")]
    public int PreparationTotalDay { get; set; }

    [JsonPropertyName("planting_total_day")]
    public int PlantingTotalDay { get; set; }

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_count")]
    public int TileCount { get; set; }

    [JsonPropertyName("planting_tile_count")]
    public int PlantingTileCount { get; set; }

    [JsonPropertyName("watering_refill_preparation_tile_count")]
    public int WateringRefillPreparationTileCount { get; set; }

    [JsonPropertyName("already_prepared_count")]
    public int AlreadyPreparedCount { get; set; }

    [JsonPropertyName("direct_till_count")]
    public int DirectTillCount { get; set; }

    [JsonPropertyName("clear_then_rebind_count")]
    public int ClearThenRebindCount { get; set; }

    [JsonPropertyName("access_clear_then_rebind_count")]
    public int AccessClearThenRebindCount { get; set; }

    [JsonPropertyName("blocked_count")]
    public int BlockedCount { get; set; }

    [JsonPropertyName("steps")]
    public MoneyRevenueCropSitePreparationStep[] Steps { get; set; } =
        Array.Empty<MoneyRevenueCropSitePreparationStep>();

    [JsonPropertyName("site_preparation_plan_ready")]
    public bool SitePreparationPlanReady { get; set; }

    [JsonPropertyName("planting_layout_proof_still_required")]
    public bool PlantingLayoutProofStillRequired { get; set; } = true;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("planning_policy")]
    public string PlanningPolicy { get; set; } =
        "The plan reads only the nominated bounded Farm region. Already prepared " +
        "tiles are no-ops; directly tillable tiles bind executor.till_soil; one " +
        "supported blocker binds the existing clear executor and requires a fresh " +
        "snapshot before tilling. A route-isolated tillable tile may bind one " +
        "currently reachable adjacent clearance before the same fresh rebind. " +
        "A Teacher-bound watering-refill corridor is " +
        "prepared by the same clear/till chain but never becomes a crop action. " +
        "Unsupported or multiply occupied tiles remain " +
        "blocked. This is a plan, not production proof, and never authorizes training.";
}

public sealed record MoneyRevenueCropSitePreparationStep(
    [property: JsonPropertyName("tile_x")] int TileX,
    [property: JsonPropertyName("tile_y")] int TileY,
    [property: JsonPropertyName("observed_status")] string ObservedStatus,
    [property: JsonPropertyName("plan_status")] string PlanStatus,
    [property: JsonPropertyName("action_option_id")] string ActionOptionId,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("parameters")]
    SmallModelActionParameter[] Parameters,
    [property: JsonPropertyName("continuation")]
    string Continuation,
    [property: JsonPropertyName("block_reasons")]
    string[] BlockReasons);
