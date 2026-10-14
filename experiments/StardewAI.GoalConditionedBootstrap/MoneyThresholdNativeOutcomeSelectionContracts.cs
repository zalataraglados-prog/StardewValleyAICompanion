using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyThresholdNativeOutcomeSelection
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_threshold_native_outcome_selection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("target_total_money_earned")]
    public uint TargetTotalMoneyEarned { get; set; }

    [JsonPropertyName("expected_crossed_criterion_ids")]
    public string[] ExpectedCrossedCriterionIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("split_key")]
    public string SplitKey { get; set; } = string.Empty;

    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("total_day")]
    public int TotalDay { get; set; }

    [JsonPropertyName("total_money_earned_before")]
    public uint TotalMoneyEarnedBefore { get; set; }

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("expected_total_money_earned_after")]
    public uint ExpectedTotalMoneyEarnedAfter { get; set; }

    [JsonPropertyName("candidate_denominator_path")]
    public string CandidateDenominatorPath { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_sha256")]
    public string CandidateDenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_catalog_sha256")]
    public string CandidateDenominatorCatalogSha256 { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_count")]
    public int CandidateDenominatorCount { get; set; }

    [JsonPropertyName("eligible_sale_candidate_count")]
    public int EligibleSaleCandidateCount { get; set; }

    [JsonPropertyName("crossing_sale_candidate_count")]
    public int CrossingSaleCandidateCount { get; set; }

    [JsonPropertyName("selected_candidate")]
    public MoneyRevenueCandidateCatalogEntry SelectedCandidate { get; set; } =
        new();

    [JsonPropertyName("objective_parameters")]
    public StardewAI.Contracts.Execution.SmallModelActionParameter[]
        ObjectiveParameters { get; set; } =
            Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>();

    [JsonPropertyName("selection_sha256")]
    public string SelectionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("selection_policy")]
    public string SelectionPolicy { get; set; } =
        "This artifact schedules native evidence collection only. It selects the smallest current, available, exact shop-sale candidate that crosses the requested authoritative threshold, then breaks ties by runtime cost and stable candidate identity. The requested criterion and partition are assertions, not labels. The complete current candidate denominator remains authoritative for later counterfactual comparison, and the resulting native episode must independently derive its crossed criteria from the evaluator authority.";
}
