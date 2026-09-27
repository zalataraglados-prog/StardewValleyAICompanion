using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class SkullKeyTeacherCorpusVersions
{
    public const string Request = "skull_key_teacher_corpus_request.v1";
    public const string Corpus = "skull_key_teacher_corpus.v1";
}

public sealed class SkullKeyTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        SkullKeyTeacherCorpusVersions.Request;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public SkullKeyTeacherCorpusSource[] Sources { get; set; } =
        Array.Empty<SkullKeyTeacherCorpusSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record SkullKeyTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")] string ExecutionEpisodePath);

public sealed class SkullKeyTeacherCorpus
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        SkullKeyTeacherCorpusVersions.Corpus;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } =
        GoalMethodTeacherCoverageGoalIds.Authoritative;

    [JsonPropertyName("direction_id")]
    public string DirectionId { get; set; } = "obtain_skull_key";

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } =
        "grandpa.direct.obtain_skull_key";

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("partition_count")]
    public int PartitionCount { get; set; }

    [JsonPropertyName("split_complete")]
    public bool SplitComplete { get; set; }

    [JsonPropertyName("rows")]
    public SkullKeyTeacherEvidenceRow[] Rows { get; set; } =
        Array.Empty<SkullKeyTeacherEvidenceRow>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("corpus_policy")]
    public string CorpusPolicy { get; set; } =
        "A row is admitted only from one fresh plan_execution_episode that binds an exact ordinary-mine floor-120 Skull Key chest, its compiled executor.interact queue item, a verified native claim_skull_key result, and a false-to-true player.has_skull_key transition. The positive Teacher alternative claims the already-reachable native reward now; the negative alternative defers the same terminal interaction. Dataset partitions are derived from save, player, and total-day identity rather than caller labels.";
}

public sealed record SkullKeyTeacherEvidenceRow(
    [property: JsonPropertyName("row_id")] string RowId,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("execution_episode_path")] string ExecutionEpisodePath,
    [property: JsonPropertyName("execution_episode_sha256")] string ExecutionEpisodeSha256,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("before_snapshot_sha256")] string BeforeSnapshotSha256,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("after_snapshot_sha256")] string AfterSnapshotSha256,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("execution_result_path")] string ExecutionResultPath,
    [property: JsonPropertyName("execution_result_sha256")] string ExecutionResultSha256,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("total_day")] int TotalDay,
    [property: JsonPropertyName("chest_tile_x")] int ChestTileX,
    [property: JsonPropertyName("chest_tile_y")] int ChestTileY,
    [property: JsonPropertyName("preferred_alternative")] string PreferredAlternative,
    [property: JsonPropertyName("rejected_alternative")] string RejectedAlternative,
    [property: JsonPropertyName("teacher_comparison_verified")] bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_terminal_outcome_verified")] bool NativeTerminalOutcomeVerified);
