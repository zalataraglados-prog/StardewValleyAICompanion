using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record RustyKeyTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "rusty_key_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public RustyKeyTeacherCorpusSource[] Sources { get; init; } = Array.Empty<RustyKeyTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record RustyKeyTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_receipt_path")] string JoinReceiptPath);

public sealed record RustyKeyTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("rows")] RustyKeyTeacherCorpusRow[] Rows)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "rusty_key_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => "obtain_rusty_key";
    [JsonPropertyName("method_id")] public string MethodId => "grandpa.direct.obtain_rusty_key";
    [JsonPropertyName("row_count")] public int RowCount => Rows.Length;
    [JsonPropertyName("partition_count")] public int PartitionCount => Rows.Select(row => row.Evidence.DatasetPartition).Distinct(StringComparer.Ordinal).Count();
    [JsonPropertyName("split_complete")] public bool SplitComplete => new[] { "train", "validation", "test" }
        .All(partition => Rows.Any(row => row.Evidence.DatasetPartition == partition));
    [JsonPropertyName("status")] public string Status => SplitComplete ? "ready_split_complete_rusty_key_teacher_corpus" : "verified_partial_rusty_key_teacher_corpus";
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;
    [JsonPropertyName("full_criterion_verified")] public bool FullCriterionVerified => false;
}

public sealed record RustyKeyTeacherCorpusRow(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_receipt_path")] string JoinReceiptPath,
    [property: JsonPropertyName("join_receipt_sha256")] string JoinReceiptSha256,
    [property: JsonPropertyName("evidence")] RustyKeyTeacherNativeJoin Evidence);
