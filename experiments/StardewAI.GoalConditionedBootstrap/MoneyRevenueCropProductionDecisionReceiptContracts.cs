using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropProductionDecisionRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_decision_request.v1";

    [JsonPropertyName("snapshot_path")]
    public string SnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("base_strategy_ledger_path")]
    public string BaseStrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("decision_recorded_at")]
    public string DecisionRecordedAt { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = "earn_money";

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; } = 224;

    [JsonPropertyName("target_total_money_earned")]
    public long TargetTotalMoneyEarned { get; set; } = 1_000_000;

    [JsonPropertyName("commitments")]
    public MoneyRevenueCropProductionDecisionCommitment[] Commitments
        { get; set; } =
            Array.Empty<MoneyRevenueCropProductionDecisionCommitment>();

    [JsonPropertyName("max_queue_item_count")]
    public int MaxQueueItemCount { get; set; } = 8;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropProductionDecisionCommitment
{
    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("seed_id")]
    public string SeedId { get; set; } = string.Empty;

    [JsonPropertyName("tile_count")]
    public int TileCount { get; set; }

    [JsonPropertyName("planting_year")]
    public int PlantingYear { get; set; }

    [JsonPropertyName("planting_season")]
    public string PlantingSeason { get; set; } = string.Empty;

    [JsonPropertyName("planting_day_of_month")]
    public int PlantingDayOfMonth { get; set; }

    [JsonPropertyName("location_context")]
    public string LocationContext { get; set; } = "outdoor_seasonal";

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = "Farm";

    [JsonPropertyName("tiles")]
    public MoneyRevenueCropPlantingTile[] Tiles { get; set; } =
        Array.Empty<MoneyRevenueCropPlantingTile>();
}

public sealed record MoneyRevenueCropProductionDecisionMutationReceipt(
    [property: JsonPropertyName("sequence_index")] int SequenceIndex,
    [property: JsonPropertyName("request")]
    CropPlantingCommitmentUpsertRequest Request,
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("errors")] string[] Errors,
    [property: JsonPropertyName("resulting_ledger_revision")]
    int? ResultingLedgerRevision,
    [property: JsonPropertyName("resulting_commitment_revision")]
    int? ResultingCommitmentRevision);

public sealed class MoneyRevenueCropProductionDecisionReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_production_decision_receipt.v1";

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

    [JsonPropertyName("base_strategy_ledger_path")]
    public string BaseStrategyLedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("base_strategy_ledger_sha256")]
    public string BaseStrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("decision_recorded_at")]
    public string DecisionRecordedAt { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("deadline_total_day_exclusive")]
    public int DeadlineTotalDayExclusive { get; set; }

    [JsonPropertyName("target_total_money_earned")]
    public long TargetTotalMoneyEarned { get; set; }

    [JsonPropertyName("mutations")]
    public MoneyRevenueCropProductionDecisionMutationReceipt[] Mutations
        { get; set; } =
            Array.Empty<MoneyRevenueCropProductionDecisionMutationReceipt>();

    [JsonPropertyName("final_ledger")]
    public StrategyCommitmentLedger? FinalLedger { get; set; }

    [JsonPropertyName("layout_intents")]
    public MoneyRevenueCropNativeProductionLayoutIntent[] LayoutIntents
        { get; set; } =
            Array.Empty<MoneyRevenueCropNativeProductionLayoutIntent>();

    [JsonPropertyName("max_queue_item_count")]
    public int MaxQueueItemCount { get; set; }

    [JsonPropertyName("all_commitments_accepted")]
    public bool AllCommitmentsAccepted { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("decision_policy")]
    public string DecisionPolicy { get; set; } =
        "The authoritative Teacher supplies explicit crop commitments and " +
        "non-overlapping Farm layout intents. This receipt deterministically " +
        "replays those commitments through the one existing crop commitment " +
        "ledger service and preserves each typed mutation outcome. It does not " +
        "invent crops or layouts, execute mechanics, prove native outcomes or " +
        "authorize formal product training.";
}
