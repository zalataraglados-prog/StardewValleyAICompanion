using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MarriageTeacherCorpusRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "marriage_teacher_corpus_request.v1";
    [JsonPropertyName("corpus_id")] public string CorpusId { get; init; } = string.Empty;
    [JsonPropertyName("knowledge_path")] public string KnowledgePath { get; init; } = string.Empty;
    [JsonPropertyName("sources")] public MarriageTeacherCorpusSource[] Sources { get; init; } = Array.Empty<MarriageTeacherCorpusSource>();
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record MarriageTeacherCorpusSource(
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("native_receipt_path")] string NativeReceiptPath,
    [property: JsonPropertyName("teacher_row_paths")] string[] TeacherRowPaths);

public sealed record MarriageTeacherCorpusDecision(
    [property: JsonPropertyName("preparation_index")] int PreparationIndex,
    [property: JsonPropertyName("teacher_row_path")] string TeacherRowPath,
    [property: JsonPropertyName("teacher_row_sha256")] string TeacherRowSha256,
    [property: JsonPropertyName("trajectory_id")] string TrajectoryId,
    [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("selected_candidate_id")] string SelectedCandidateId,
    [property: JsonPropertyName("candidate_denominator_sha256")] string CandidateDenominatorSha256,
    [property: JsonPropertyName("decision_candidate_count")] int DecisionCandidateCount,
    [property: JsonPropertyName("queue_id")] string QueueId,
    [property: JsonPropertyName("queue_item_ids")] string[] QueueItemIds,
    [property: JsonPropertyName("primitive_option_ids")] string[] PrimitiveOptionIds,
    [property: JsonPropertyName("request_nonces")] string[] RequestNonces,
    [property: JsonPropertyName("strict_pair_count")] int StrictPairCount,
    [property: JsonPropertyName("uncompared_candidate_ids")] string[] UncomparedCandidateIds,
    [property: JsonPropertyName("complete_order_verified")] bool CompleteOrderVerified);

public sealed record MarriageTeacherCorpusRun
{
    [JsonPropertyName("source_id")] public string SourceId { get; init; } = string.Empty;
    [JsonPropertyName("native_receipt_path")] public string NativeReceiptPath { get; init; } = string.Empty;
    [JsonPropertyName("native_receipt_sha256")] public string NativeReceiptSha256 { get; init; } = string.Empty;
    [JsonPropertyName("run_id")] public string RunId { get; init; } = string.Empty;
    [JsonPropertyName("save_id")] public string SaveId { get; init; } = string.Empty;
    [JsonPropertyName("player_id")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("origin_split_key")] public string OriginSplitKey { get; init; } = string.Empty;
    [JsonPropertyName("dataset_partition")] public string DatasetPartition { get; init; } = string.Empty;
    [JsonPropertyName("before_state_hash")] public string BeforeStateHash { get; init; } = string.Empty;
    [JsonPropertyName("after_state_hash")] public string AfterStateHash { get; init; } = string.Empty;
    [JsonPropertyName("total_day_before")] public int TotalDayBefore { get; init; }
    [JsonPropertyName("total_day_after")] public int TotalDayAfter { get; init; }
    [JsonPropertyName("final_snapshot_sha256")] public string FinalSnapshotSha256 { get; init; } = string.Empty;
    [JsonPropertyName("queue_chain")] public JsonElement QueueChain { get; init; }
    [JsonPropertyName("joint_criterion_transition")] public JsonElement JointCriterionTransition { get; init; }
    [JsonPropertyName("preparation_decisions")] public MarriageTeacherCorpusDecision[] PreparationDecisions { get; init; } = Array.Empty<MarriageTeacherCorpusDecision>();
    [JsonPropertyName("native_outcome_verified")] public bool NativeOutcomeVerified { get; init; }
}

public sealed record MarriageTeacherCriterionCoverage(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("teacher_comparison_partitions")] string[] TeacherComparisonPartitions,
    [property: JsonPropertyName("native_outcome_partitions")] string[] NativeOutcomePartitions)
{
    internal static readonly IReadOnlyList<string> RequiredPartitions = Array.AsReadOnly(new[] { "train", "validation", "test" });
    [JsonPropertyName("split_complete")] public bool SplitComplete => RequiredPartitions.All(partition =>
        TeacherComparisonPartitions.Contains(partition) && NativeOutcomePartitions.Contains(partition));
}

public sealed record MarriageTeacherCorpus(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("corpus_id")] string CorpusId,
    [property: JsonPropertyName("knowledge_sha256")] string KnowledgeSha256,
    [property: JsonPropertyName("runs")] MarriageTeacherCorpusRun[] Runs)
{
    public const string CriterionId = "married_or_roommate_house_2";
    public const string CurrentDirectionId = "marriage_and_house_upgrade";
    [JsonPropertyName("schema_version")] public string SchemaVersion => "marriage_teacher_corpus.v1";
    [JsonPropertyName("goal_id")] public string GoalId => GoalMethodTeacherCoverageGoalIds.Authoritative;
    [JsonPropertyName("direction_id")] public string DirectionId => CurrentDirectionId;
    [JsonPropertyName("method_id")] public string MethodId => GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId).BindingRuleId;
    [JsonPropertyName("game_version")] public string GameVersion => "1.6.15";
    [JsonPropertyName("knowledge_dictionary_version")] public string KnowledgeDictionaryVersion => StardewAI.Contracts.Training.PolicyTrajectoryVersionPins.KnowledgeDictionary;
    [JsonPropertyName("row_count")] public int RowCount => Runs.Sum(run => run.PreparationDecisions.Length);
    [JsonPropertyName("native_outcome_count")] public int NativeOutcomeCount => Runs.Count(run => run.NativeOutcomeVerified);
    [JsonPropertyName("comparison_admission")] public string ComparisonAdmission => "independent_preparation_pairs_linked_to_complete_joint_outcome";
    [JsonPropertyName("criterion_coverage")] public MarriageTeacherCriterionCoverage[] CriterionCoverage => new[]
    {
        new MarriageTeacherCriterionCoverage(CriterionId, Partitions(true), Partitions(false))
    };
    [JsonPropertyName("split_complete")] public bool SplitComplete => CriterionCoverage.All(row => row.SplitComplete);
    [JsonPropertyName("status")] public string Status => SplitComplete ? "verified_split_complete_marriage_corpus" : "verified_partial_marriage_corpus";
    [JsonPropertyName("prior_house_acquisition_verified")] public bool PriorHouseAcquisitionVerified => false;
    [JsonPropertyName("prior_relationship_acquisition_verified")] public bool PriorRelationshipAcquisitionVerified => false;
    [JsonPropertyName("mechanical_controls_are_preference_labels")] public bool MechanicalControlsArePreferenceLabels => false;
    [JsonPropertyName("full_root_verified")] public bool FullRootVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;

    private string[] Partitions(bool teacher) => MarriageTeacherCriterionCoverage.RequiredPartitions.Where(partition =>
        Runs.Any(run => run.DatasetPartition == partition && (teacher
            ? run.PreparationDecisions.Length > 0 && run.PreparationDecisions.All(row => row.StrictPairCount > 0)
            : run.NativeOutcomeVerified))).ToArray();
}
