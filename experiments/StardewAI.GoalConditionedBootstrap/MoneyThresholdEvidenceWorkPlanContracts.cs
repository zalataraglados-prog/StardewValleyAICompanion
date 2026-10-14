using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyThresholdEvidenceWorkPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_threshold_evidence_work_plan_request.v1";

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("money_threshold_corpus_path")]
    public string MoneyThresholdCorpusPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyThresholdEvidenceWorkPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_threshold_evidence_work_plan.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("money_threshold_corpus_path")]
    public string MoneyThresholdCorpusPath { get; set; } = string.Empty;

    [JsonPropertyName("money_threshold_corpus_sha256")]
    public string MoneyThresholdCorpusSha256 { get; set; } = string.Empty;

    [JsonPropertyName("criterion_count")]
    public int CriterionCount { get; set; }

    [JsonPropertyName("required_partition_count")]
    public int RequiredPartitionCount { get; set; }

    [JsonPropertyName("required_cell_count")]
    public int RequiredCellCount { get; set; }

    [JsonPropertyName("native_outcome_complete_cell_count")]
    public int NativeOutcomeCompleteCellCount { get; set; }

    [JsonPropertyName("teacher_comparison_complete_cell_count")]
    public int TeacherComparisonCompleteCellCount { get; set; }

    [JsonPropertyName("fully_complete_cell_count")]
    public int FullyCompleteCellCount { get; set; }

    [JsonPropertyName("coverage_complete")]
    public bool CoverageComplete { get; set; }

    [JsonPropertyName("cells")]
    public MoneyThresholdEvidenceWorkCell[] Cells { get; set; } =
        Array.Empty<MoneyThresholdEvidenceWorkCell>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("plan_policy")]
    public string PlanPolicy { get; set; } =
        "The work plan is the Cartesian product of the authoritative Grandpa Money thresholds and train/validation/test. Native outcome and exact-denominator Teacher comparison channels are counted independently from a replay-verified Money corpus. This plan schedules missing evidence only; it cannot create evidence, promote fixtures or authorize formal training.";
}

public sealed record MoneyThresholdEvidenceWorkCell(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("threshold_order")] int ThresholdOrder,
    [property: JsonPropertyName("target_total_money_earned")]
        uint TargetTotalMoneyEarned,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("dataset_partition")]
        string DatasetPartition,
    [property: JsonPropertyName("native_outcome_verified")]
        bool NativeOutcomeVerified,
    [property: JsonPropertyName("teacher_comparison_verified")]
        bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_outcome_row_ids")]
        string[] NativeOutcomeRowIds,
    [property: JsonPropertyName("teacher_comparison_row_ids")]
        string[] TeacherComparisonRowIds,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("next_action")] string NextAction);
