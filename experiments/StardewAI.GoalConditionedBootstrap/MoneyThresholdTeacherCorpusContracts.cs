using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdTeacherCorpusVersions
{
    public const string Request =
        "money_threshold_teacher_corpus_request.v2";
    public const string Corpus =
        "money_threshold_teacher_corpus.v2";
}

public sealed class MoneyThresholdTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdTeacherCorpusVersions.Request;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public MoneyThresholdTeacherCorpusSource[] Sources { get; set; } =
        Array.Empty<MoneyThresholdTeacherCorpusSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyThresholdTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath);

public sealed class MoneyThresholdTeacherCorpus
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdTeacherCorpusVersions.Corpus;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } =
        GoalMethodTeacherCoverageGoalIds.Authoritative;

    [JsonPropertyName("direction_id")]
    public string DirectionId { get; set; } = "earn_money";

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } = "grandpa.direct.earn_money";

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("covered_criterion_count")]
    public int CoveredCriterionCount { get; set; }

    [JsonPropertyName("rows")]
    public MoneyThresholdTeacherEvidenceRow[] Rows { get; set; } =
        Array.Empty<MoneyThresholdTeacherEvidenceRow>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("corpus_policy")]
    public string CorpusPolicy { get; set; } =
        "Each row is rebuilt from one successful native ShopMenu sale episode bound to fresh before/after snapshots. The shared Money progression verifier requires the sale to increase current money and totalMoneyEarned by the same exact positive amount while preserving the de-duplicated pending shipping scope. Crossed Grandpa thresholds and their criterion IDs are derived only from the evaluator authority, never supplied by the request. This raw corpus proves native terminal outcomes only; it cannot infer a Teacher preference from the action that happened. A separate exact-denominator comparison admission must supply criterion-local Teacher comparisons before coverage can become ready.";
}

public sealed record MoneyThresholdTeacherEvidenceRow(
    [property: JsonPropertyName("row_id")] string RowId,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath,
    [property: JsonPropertyName("execution_episode_sha256")]
        string ExecutionEpisodeSha256,
    [property: JsonPropertyName("before_snapshot_path")]
        string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")]
        string BeforeSnapshotSha256,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_snapshot_path")]
        string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")]
        string AfterSnapshotSha256,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("execution_result_path")]
        string ExecutionResultPath,
    [property: JsonPropertyName("execution_result_sha256")]
        string ExecutionResultSha256,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("money_before")] int MoneyBefore,
    [property: JsonPropertyName("money_after")] int MoneyAfter,
    [property: JsonPropertyName("total_money_earned_before")]
        uint TotalMoneyEarnedBefore,
    [property: JsonPropertyName("total_money_earned_after")]
        uint TotalMoneyEarnedAfter,
    [property: JsonPropertyName("earned_delta")] uint EarnedDelta,
    [property: JsonPropertyName("crossed_thresholds")]
        MoneyThresholdTeacherCriterion[] CrossedThresholds,
    [property: JsonPropertyName("preferred_alternative")]
        string PreferredAlternative,
    [property: JsonPropertyName("rejected_alternative")]
        string RejectedAlternative,
    [property: JsonPropertyName("teacher_comparison_verified")]
        bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_terminal_outcome_verified")]
        bool NativeTerminalOutcomeVerified);

public sealed record MoneyThresholdTeacherCriterion(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("target")] uint Target,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("teacher_goal_context_id")]
        string TeacherGoalContextId,
    [property: JsonPropertyName("terminal_assertion")]
        string TerminalAssertion,
    [property: JsonPropertyName("preferred_alternative")]
        string PreferredAlternative,
    [property: JsonPropertyName("rejected_alternative")]
        string RejectedAlternative,
    [property: JsonPropertyName("teacher_comparison_verified")]
        bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_terminal_outcome_verified")]
        bool NativeTerminalOutcomeVerified);
