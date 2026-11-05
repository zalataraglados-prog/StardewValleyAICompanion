using System.Text.Json.Serialization;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record SkillTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "skill_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("knowledge_path")] public string KnowledgePath { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public SkillTeacherCorpusSource[] Sources { get; init; } = Array.Empty<SkillTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record SkillTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("teacher_row_path")] string TeacherRowPath);

public sealed record SkillTeacherCorpusRow(
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
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("request_nonce")] string RequestNonce,
    [property: JsonPropertyName("strict_pair_count")] int StrictPairCount,
    [property: JsonPropertyName("native_outcome_verified")] bool NativeOutcomeVerified);

public sealed record SkillTeacherCriterionCoverage(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("teacher_comparison_partitions")] string[] TeacherComparisonPartitions,
    [property: JsonPropertyName("native_outcome_partitions")] string[] NativeOutcomePartitions)
{
    [JsonPropertyName("split_complete")] public bool SplitComplete => new[] { "train", "validation", "test" }
        .All(partition => TeacherComparisonPartitions.Contains(partition) && NativeOutcomePartitions.Contains(partition));
}

public sealed record SkillTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("knowledge_sha256")] string KnowledgeSha256,
    [property: JsonPropertyName("rows")] SkillTeacherCorpusRow[] Rows)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "skill_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => "raise_skill_levels";
    [JsonPropertyName("method_id")] public string MethodId => GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId).BindingRuleId;
    [JsonPropertyName("game_version")] public string GameVersion => NativeSkillExperienceRules.GameVersion;
    [JsonPropertyName("knowledge_dictionary_version")] public string KnowledgeDictionaryVersion => StardewAI.Contracts.Training.PolicyTrajectoryVersionPins.KnowledgeDictionary;
    [JsonPropertyName("row_count")] public int RowCount => Rows.Length;
    [JsonPropertyName("criterion_coverage")] public SkillTeacherCriterionCoverage[] CriterionCoverage => new[] { "player_level_15", "player_level_25" }
        .Select(id => new SkillTeacherCriterionCoverage(id, Partitions(id, true), Partitions(id, false))).ToArray();
    [JsonPropertyName("split_complete")] public bool SplitComplete => CriterionCoverage.All(row => row.SplitComplete);
    [JsonPropertyName("status")] public string Status => SplitComplete ? "verified_split_complete_skill_corpus" : "verified_partial_skill_corpus";
    [JsonPropertyName("prior_skill_acquisition_verified")] public bool PriorSkillAcquisitionVerified => false;
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;

    private string[] Partitions(string id, bool teacher) => new[] { "train", "validation", "test" }
        .Where(partition => Rows.Any(row => row.CriterionId == id && row.DatasetPartition == partition &&
            (teacher ? row.StrictPairCount > 0 : row.NativeOutcomeVerified))).ToArray();
}
