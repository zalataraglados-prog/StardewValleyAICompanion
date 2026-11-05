using System.Text.Json.Serialization;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "master_angler_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public MasterAnglerTeacherCorpusSource[] Sources { get; init; } = Array.Empty<MasterAnglerTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record MasterAnglerTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_receipt_path")] string JoinReceiptPath);

public sealed record MasterAnglerTeacherCorpusRow(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("join_receipt_path")] string JoinReceiptPath,
    [property: JsonPropertyName("join_receipt_sha256")] string JoinReceiptSha256,
    [property: JsonPropertyName("evidence")] MasterAnglerTeacherNativeJoin Evidence,
    [property: JsonPropertyName("criterion_terminal_verified")] bool CriterionTerminalVerified);

public sealed record MasterAnglerTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("rows")] MasterAnglerTeacherCorpusRow[] Rows)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "master_angler_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => "complete_master_angler";
    [JsonPropertyName("method_id")] public string MethodId => GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId).BindingRuleId;
    [JsonPropertyName("row_count")] public int RowCount => Rows.Length;
    [JsonPropertyName("criterion_terminal_row_count")] public int CriterionTerminalRowCount => Rows.Count(row => row.CriterionTerminalVerified);
    [JsonPropertyName("teacher_comparison_partitions")] public string[] TeacherComparisonPartitions => Partitions(Rows);
    [JsonPropertyName("collection_progress_partitions")] public string[] CollectionProgressPartitions => Partitions(Rows);
    [JsonPropertyName("native_outcome_partitions")] public string[] NativeOutcomePartitions => Partitions(Rows.Where(row => row.CriterionTerminalVerified));
    [JsonPropertyName("split_complete")] public bool SplitComplete => new[] { "train", "validation", "test" }
        .All(partition => TeacherComparisonPartitions.Contains(partition) && NativeOutcomePartitions.Contains(partition));
    [JsonPropertyName("status")] public string Status => SplitComplete ? "verified_split_complete_master_angler_corpus" : "verified_partial_master_angler_corpus";
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;
    [JsonPropertyName("full_criterion_verified")] public bool FullCriterionVerified => false;

    private static string[] Partitions(IEnumerable<MasterAnglerTeacherCorpusRow> rows) => rows.Select(row => row.Evidence.DatasetPartition)
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
