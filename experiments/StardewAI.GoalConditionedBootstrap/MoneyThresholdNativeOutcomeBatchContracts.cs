using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeOutcomeBatchVersions
{
    public const string LegacyRequest =
        "money_threshold_native_outcome_batch_request.v1";
    public const string ComparisonRequest =
        "money_threshold_native_outcome_batch_request.v2";
    public const string SelectedEpisodeRequest =
        "money_threshold_native_outcome_batch_request.v3";
    public const string Request =
        "money_threshold_native_outcome_batch_request.v4";
    public const string Batch =
        "money_threshold_native_outcome_batch.v3";
}

public sealed class MoneyThresholdNativeOutcomeBatchRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdNativeOutcomeBatchVersions.Request;

    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    [JsonPropertyName("run_summary_paths")]
    public string[] RunSummaryPaths { get; set; } = Array.Empty<string>();

    [JsonPropertyName("comparison_admissions")]
    public MoneyThresholdNativeOutcomeComparisonAdmission[]
        ComparisonAdmissions { get; set; } =
            Array.Empty<MoneyThresholdNativeOutcomeComparisonAdmission>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyThresholdNativeOutcomeComparisonAdmission(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("admission_manifest_path")]
        string AdmissionManifestPath,
    [property: JsonPropertyName("admission_receipt_path")]
        string AdmissionReceiptPath,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath = "",
    [property: JsonPropertyName("native_trajectory_receipt_path")]
        string NativeTrajectoryReceiptPath = "");

public sealed class MoneyThresholdNativeOutcomeRunSummary
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_threshold_native_outcome_run_summary.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("source_save_slot_path")]
    public string SourceSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_save_sha256_before")]
    public string SourceSaveSha256Before { get; set; } = string.Empty;

    [JsonPropertyName("source_save_sha256_after")]
    public string SourceSaveSha256After { get; set; } = string.Empty;

    [JsonPropertyName("isolated_save_slot_path")]
    public string IsolatedSaveSlotPath { get; set; } = string.Empty;

    [JsonPropertyName("isolated_save_sha256_before")]
    public string IsolatedSaveSha256Before { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("natural_service_wait_applied")]
    public bool NaturalServiceWaitApplied { get; set; }

    [JsonPropertyName("selection_path")]
    public string SelectionPath { get; set; } = string.Empty;

    [JsonPropertyName("selection_sha256")]
    public string SelectionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_path")]
    public string CandidateDenominatorPath { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_sha256")]
    public string CandidateDenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_episode_path")]
    public string ExecutionEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("money_threshold_corpus_path")]
    public string MoneyThresholdCorpusPath { get; set; } = string.Empty;

    [JsonPropertyName("crossed_criterion_ids")]
    public string[] CrossedCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyThresholdNativeOutcomeBatch
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdNativeOutcomeBatchVersions.Batch;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("batch_id")]
    public string BatchId { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("verified_run_count")]
    public int VerifiedRunCount { get; set; }

    [JsonPropertyName("native_complete_cell_count")]
    public int NativeCompleteCellCount { get; set; }

    [JsonPropertyName("verified_comparison_admission_count")]
    public int VerifiedComparisonAdmissionCount { get; set; }

    [JsonPropertyName("teacher_comparison_complete_cell_count")]
    public int TeacherComparisonCompleteCellCount { get; set; }

    [JsonPropertyName("fully_complete_cell_count")]
    public int FullyCompleteCellCount { get; set; }

    [JsonPropertyName("required_cell_count")]
    public int RequiredCellCount { get; set; } = 18;

    [JsonPropertyName("cells")]
    public MoneyThresholdNativeOutcomeCellStatus[] Cells { get; set; } =
        Array.Empty<MoneyThresholdNativeOutcomeCellStatus>();

    [JsonPropertyName("verified_runs")]
    public MoneyThresholdNativeOutcomeVerifiedRun[] VerifiedRuns { get; set; } =
        Array.Empty<MoneyThresholdNativeOutcomeVerifiedRun>();

    [JsonPropertyName("corpus")]
    public MoneyThresholdTeacherCorpus Corpus { get; set; } = new();

    [JsonPropertyName("corpus_output_path")]
    public string CorpusOutputPath { get; set; } = string.Empty;

    [JsonPropertyName("corpus_output_sha256")]
    public string CorpusOutputSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("batch_policy")]
    public string BatchPolicy { get; set; } =
        "Every seed run summary is replayed through its candidate denominator, native selection and Money corpus before admission. Optional comparison admissions are keyed by an existing run and a criterion actually crossed by that seed. Request v4 may replace the provisional seed sale with one common, independently verified native Revenue trajectory selected by the exact comparison; v3 retains direct-sale episode override compatibility. The canonical corpus replays the selected outcome and criterion-local join. Native and Teacher-comparison criterion-partition cells are derived from that verified corpus, not caller counts. Duplicate split keys, duplicate run/criterion attachments and multiple selected outcomes for one seed are rejected. This batch cannot create evidence or authorize formal training.";
}

public sealed record MoneyThresholdNativeOutcomeCellStatus(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("dataset_partition")]
        string DatasetPartition,
    [property: JsonPropertyName("native_outcome_verified")]
        bool NativeOutcomeVerified,
    [property: JsonPropertyName("teacher_comparison_verified")]
        bool TeacherComparisonVerified,
    [property: JsonPropertyName("native_outcome_row_ids")]
        string[] NativeOutcomeRowIds,
    [property: JsonPropertyName("teacher_comparison_row_ids")]
        string[] TeacherComparisonRowIds);

public sealed record MoneyThresholdNativeOutcomeVerifiedRun(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("summary_path")] string SummaryPath,
    [property: JsonPropertyName("summary_sha256")] string SummarySha256,
    [property: JsonPropertyName("selection_sha256")]
        string SelectionSha256,
    [property: JsonPropertyName("candidate_denominator_sha256")]
        string CandidateDenominatorSha256,
    [property: JsonPropertyName("execution_episode_path")]
        string ExecutionEpisodePath,
    [property: JsonPropertyName("dataset_partition")]
        string DatasetPartition,
    [property: JsonPropertyName("crossed_criterion_ids")]
        string[] CrossedCriterionIds);
