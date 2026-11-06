using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record FriendshipTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "friendship_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("knowledge_path")] public string KnowledgePath { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public FriendshipTeacherCorpusSource[] Sources { get; init; } = Array.Empty<FriendshipTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record FriendshipTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("teacher_row_path")] string TeacherRowPath);

public sealed record FriendshipTeacherCorpusRow(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("teacher_row_path")] string TeacherRowPath,
    [property: JsonPropertyName("teacher_row_sha256")] string TeacherRowSha256,
    [property: JsonPropertyName("native_receipt_path")] string NativeReceiptPath,
    [property: JsonPropertyName("native_receipt_sha256")] string NativeReceiptSha256,
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("trajectory_id")] string TrajectoryId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("player_id")] string PlayerId,
    [property: JsonPropertyName("split_key")] string SplitKey,
    [property: JsonPropertyName("dataset_partition")] string DatasetPartition,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("candidate_denominator_sha256")] string CandidateDenominatorSha256,
    [property: JsonPropertyName("decision_candidate_count")] int DecisionCandidateCount,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_ids")] string[] QueueItemIds,
    [property: JsonPropertyName("primitive_option_ids")] string[] PrimitiveOptionIds,
    [property: JsonPropertyName("request_nonces")] string[] RequestNonces,
    [property: JsonPropertyName("qualifying_count_before")] int QualifyingCountBefore,
    [property: JsonPropertyName("qualifying_count_after")] int QualifyingCountAfter,
    [property: JsonPropertyName("strict_pair_count")] int StrictPairCount,
    [property: JsonPropertyName("uncompared_candidate_ids")] string[] UncomparedCandidateIds,
    [property: JsonPropertyName("complete_order_verified")] bool CompleteOrderVerified,
    [property: JsonPropertyName("native_outcome_verified")] bool NativeOutcomeVerified);

public sealed record FriendshipTeacherCriterionCoverage(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("teacher_comparison_partitions")] string[] TeacherComparisonPartitions,
    [property: JsonPropertyName("native_outcome_partitions")] string[] NativeOutcomePartitions)
{
    internal static readonly IReadOnlyList<string> RequiredPartitions = Array.AsReadOnly(new[]
        { PolicyDatasetPartitions.Train, PolicyDatasetPartitions.Validation, PolicyDatasetPartitions.Test });
    [JsonPropertyName("split_complete")] public bool SplitComplete => RequiredPartitions
        .All(partition => TeacherComparisonPartitions.Contains(partition) && NativeOutcomePartitions.Contains(partition));
}

public sealed record FriendshipTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("knowledge_sha256")] string KnowledgeSha256,
    [property: JsonPropertyName("rows")] FriendshipTeacherCorpusRow[] Rows)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "friendship_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => "raise_friendships";
    [JsonPropertyName("method_id")] public string MethodId => GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId).BindingRuleId;
    [JsonPropertyName("game_version")] public string GameVersion => "1.6.15";
    [JsonPropertyName("knowledge_dictionary_version")] public string KnowledgeDictionaryVersion => PolicyTrajectoryVersionPins.KnowledgeDictionary;
    [JsonPropertyName("row_count")] public int RowCount => Rows.Length;
    [JsonPropertyName("comparison_admission")] public string ComparisonAdmission => "independent_pairs";
    [JsonPropertyName("criterion_coverage")] public FriendshipTeacherCriterionCoverage[] CriterionCoverage => new[] { "friendships_5", "friendships_10" }
        .Select(id => new FriendshipTeacherCriterionCoverage(id, Partitions(id, true), Partitions(id, false))).ToArray();
    [JsonPropertyName("split_complete")] public bool SplitComplete => CriterionCoverage.All(row => row.SplitComplete);
    [JsonPropertyName("status")] public string Status => SplitComplete ? "verified_split_complete_friendship_corpus" : "verified_partial_friendship_corpus";
    [JsonPropertyName("prior_friendship_acquisition_verified")] public bool PriorFriendshipAcquisitionVerified => false;
    [JsonPropertyName("prior_story_progress_verified")] public bool PriorStoryProgressVerified => false;
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;

    private string[] Partitions(string id, bool teacher) => FriendshipTeacherCriterionCoverage.RequiredPartitions
        .Where(partition => Rows.Any(row => row.CriterionId == id && row.DatasetPartition == partition &&
            (teacher ? row.StrictPairCount > 0 : row.NativeOutcomeVerified))).ToArray();
}
