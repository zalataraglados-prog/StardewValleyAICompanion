using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioSupervisionCorpusRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_supervision_corpus_request.v1";

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public AcquisitionRoutePortfolioSupervisionCorpusSource[] Sources
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioSupervisionCorpusSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusSource
{
    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("proof_manifest_path")]
    public string ProofManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("proof_receipt_path")]
    public string ProofReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("rollout_admission_receipt_path")]
    public string RolloutAdmissionReceiptPath { get; set; } = string.Empty;
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusRow
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_supervision_corpus_row.v1";

    [JsonPropertyName("source_dataset_sha256")]
    public string SourceDatasetSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("supervision_row")]
    public AcquisitionRoutePortfolioSupervisionRow SupervisionRow
    { get; set; } = new();
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_supervision_corpus_manifest.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("corpus_id")]
    public string CorpusId { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public AcquisitionRoutePortfolioSupervisionCorpusSourceDigest[] Sources
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioSupervisionCorpusSourceDigest>();

    [JsonPropertyName("cleaned")]
    public PolicyDatasetFileDigest Cleaned { get; set; } = new();

    [JsonPropertyName("split_policy")]
    public PolicyDatasetSplitPolicy SplitPolicy { get; set; } = new();

    [JsonPropertyName("partitions")]
    public PolicyDatasetPartitionDigest[] Partitions { get; set; } =
        Array.Empty<PolicyDatasetPartitionDigest>();

    [JsonPropertyName("counts")]
    public AcquisitionRoutePortfolioSupervisionCorpusCounts Counts
    { get; set; } = new();

    [JsonPropertyName("game_versions")]
    public string[] GameVersions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("bridge_versions")]
    public string[] BridgeVersions { get; set; } = Array.Empty<string>();

    [JsonPropertyName("teacher_training_evidence_eligible")]
    public bool TeacherTrainingEvidenceEligible { get; set; }

    [JsonPropertyName("goal_method_trainer_input_ready")]
    public bool GoalMethodTrainerInputReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("corpus_policy")]
    public string CorpusPolicy { get; set; } =
        "Every source dataset is rebuilt from its rollout proof and admission. Exact duplicate rows are deduplicated; conflicting row identities fail closed. Splits derive only from the verified decision snapshot save/day identity. Teacher evidence may be retained without becoming trainer input: pairwise signal and immutable version coverage are independent gates. This manifest never authorizes formal product training.";
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusSourceDigest
{
    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("dataset_sha256")]
    public string DatasetSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proof_manifest_path")]
    public string ProofManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("proof_manifest_sha256")]
    public string ProofManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proof_receipt_path")]
    public string ProofReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("proof_receipt_sha256")]
    public string ProofReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_admission_receipt_path")]
    public string RolloutAdmissionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("rollout_admission_receipt_sha256")]
    public string RolloutAdmissionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("rollout_id")]
    public string RolloutId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusCounts
{
    [JsonPropertyName("input_source_entries")]
    public int InputSourceEntries { get; set; }

    [JsonPropertyName("unique_source_datasets")]
    public int UniqueSourceDatasets { get; set; }

    [JsonPropertyName("input_rows")]
    public int InputRows { get; set; }

    [JsonPropertyName("accepted_rows")]
    public int AcceptedRows { get; set; }

    [JsonPropertyName("exact_duplicate_rows")]
    public int ExactDuplicateRows { get; set; }

    [JsonPropertyName("teacher_pairwise_preferences")]
    public int TeacherPairwisePreferences { get; set; }

    [JsonPropertyName("unavailable_candidate_count")]
    public int UnavailableCandidateCount { get; set; }

    [JsonPropertyName("student_observation_count")]
    public int StudentObservationCount { get; set; }
}

public sealed class AcquisitionRoutePortfolioSupervisionCorpusBuildResult
{
    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest")]
    public AcquisitionRoutePortfolioSupervisionCorpusManifest Manifest
    { get; set; } = new();
}
