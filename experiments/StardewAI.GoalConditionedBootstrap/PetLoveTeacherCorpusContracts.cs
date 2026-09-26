using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class PetLoveTeacherCorpusVersions
{
    public const string Request = "pet_love_teacher_corpus_request.v1";
    public const string Corpus = "pet_love_teacher_corpus.v1";
}

public sealed class PetLoveTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        PetLoveTeacherCorpusVersions.Request;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public PetLoveTeacherCorpusSource[] Sources { get; set; } =
        Array.Empty<PetLoveTeacherCorpusSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record PetLoveTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("before_snapshot_path")] string BeforeSnapshotPath,
    [property: JsonPropertyName("after_snapshot_path")] string AfterSnapshotPath,
    [property: JsonPropertyName("execution_result_path")] string ExecutionResultPath);

public sealed class PetLoveTeacherCorpus
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        PetLoveTeacherCorpusVersions.Corpus;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } =
        GoalMethodTeacherCoverageGoalIds.Authoritative;

    [JsonPropertyName("direction_id")]
    public string DirectionId { get; set; } = "earn_pet_love";

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } =
        "grandpa.direct.earn_pet_love";

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    [JsonPropertyName("partition_count")]
    public int PartitionCount { get; set; }

    [JsonPropertyName("split_complete")]
    public bool SplitComplete { get; set; }

    [JsonPropertyName("rows")]
    public PetLoveTeacherEvidenceRow[] Rows { get; set; } =
        Array.Empty<PetLoveTeacherEvidenceRow>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("corpus_policy")]
    public string CorpusPolicy { get; set; } =
        "A row is admitted only when one native pet interaction is bound to fresh before/after snapshots, changes friendship from 988-999 to exactly 1000, and causes the native petLoveMessage terminal receipt. The positive Teacher alternative executes that already-admitted method now; the negative alternative defers it and remains non-terminal in the same state. Dataset partitions are derived from save, player, and total-day identity rather than caller labels.";
}

public sealed record PetLoveTeacherEvidenceRow(
    [property: JsonPropertyName("row_id")] string RowId,
    [property: JsonPropertyName("source_id")] string SourceId,
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
    [property: JsonPropertyName("pet_id")] string PetId,
    [property: JsonPropertyName("friendship_before")] int FriendshipBefore,
    [property: JsonPropertyName("friendship_after")] int FriendshipAfter,
    [property: JsonPropertyName("preferred_alternative")] string PreferredAlternative,
    [property: JsonPropertyName("rejected_alternative")] string RejectedAlternative,
    [property: JsonPropertyName("teacher_comparison_verified")] bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_terminal_outcome_verified")] bool NativeTerminalOutcomeVerified);
