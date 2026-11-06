using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MuseumTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "museum_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public MuseumTeacherCorpusSource[] Sources { get; init; } = Array.Empty<MuseumTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record MuseumTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_path")] string JoinPath);

public sealed record MuseumTeacherCorpusRow(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_path")] string JoinPath,
    [property: JsonPropertyName("join_sha256")] string JoinSha256,
    [property: JsonPropertyName("evidence")] MuseumTerminalNativeJoin Evidence);

public sealed record MuseumTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("rows")] MuseumTeacherCorpusRow[] Rows)
{
    public const string CriterionId = "achievement_complete_collection";
    public const string Direction = "complete_museum_collection";
    public const string Method = "grandpa.direct.complete_museum_collection";
    [JsonPropertyName("schema_version")] public string SchemaVersion => "museum_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => Direction;
    [JsonPropertyName("method_id")] public string MethodId => Method;
    [JsonPropertyName("criterion_id")] public string Criterion => CriterionId;
    [JsonPropertyName("knowledge_sha256")] public string KnowledgeSha256 => Rows[0].Evidence.SourceSha256["comparison.knowledge"];
    [JsonPropertyName("requirement_inventory_sha256")] public string RequirementInventorySha256 => Rows[0].Evidence.SourceSha256["comparison.requirement_inventory"];
    [JsonPropertyName("original_dependency_expansion_sha256")] public string OriginalDependencyExpansionSha256 => Rows[0].Evidence.SourceSha256["comparison.dependencies"];
    [JsonPropertyName("row_count")] public int RowCount => Rows.Length;
    [JsonPropertyName("native_outcome_count")] public int NativeOutcomeCount => Rows.Length;
    [JsonPropertyName("strict_pair_count")] public int StrictPairCount => Rows.Sum(row => row.Evidence.PairwisePreferences.Length);
    [JsonPropertyName("teacher_comparison_partitions")] public string[] TeacherComparisonPartitions => Partitions();
    [JsonPropertyName("native_outcome_partitions")] public string[] NativeOutcomePartitions => Partitions();
    [JsonPropertyName("split_complete")] public bool SplitComplete => Partitions().Length == 3;
    [JsonPropertyName("status")] public string Status => SplitComplete ? "ready_split_complete_museum_teacher_corpus" : "verified_partial_museum_teacher_corpus";
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;

    private string[] Partitions() => new[] { "train", "validation", "test" }
        .Where(partition => Rows.Any(row => row.Evidence.DatasetPartition == partition)).ToArray();
}
