using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public static class PolicyDatasetPartitions
{
    public const string Train = "train";
    public const string Validation = "validation";
    public const string Test = "test";
}

public static class PolicyHorizonKinds
{
    public const string Day = "day";
    public const string Season = "season";
    public const string Year = "year";
    public const string Grandpa21 = "grandpa_21";
}

public sealed class PolicyHorizonObservationEnvelope
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "policy_horizon_observation.v1";

    [JsonPropertyName("observation_id")]
    public string ObservationId { get; set; } = string.Empty;

    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("horizon")]
    public string Horizon { get; set; } = string.Empty;

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("grandpa_score")]
    public int? GrandpaScore { get; set; }

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("evidence_kind")]
    public string EvidenceKind { get; set; } = string.Empty;

    [JsonPropertyName("evidence_path")]
    public string EvidencePath { get; set; } = string.Empty;
}

public sealed class PolicyDatasetManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "policy_dataset_manifest.v1";

    [JsonPropertyName("input")]
    public PolicyDatasetFileDigest Input { get; set; } = new();

    [JsonPropertyName("horizon_observations")]
    public PolicyDatasetFileDigest? HorizonObservations { get; set; }

    [JsonPropertyName("cleaned")]
    public PolicyDatasetFileDigest Cleaned { get; set; } = new();

    [JsonPropertyName("split_policy")]
    public PolicyDatasetSplitPolicy SplitPolicy { get; set; } = new();

    [JsonPropertyName("counts")]
    public PolicyDatasetCounts Counts { get; set; } = new();

    [JsonPropertyName("partitions")]
    public PolicyDatasetPartitionDigest[] Partitions { get; set; } = Array.Empty<PolicyDatasetPartitionDigest>();

    [JsonPropertyName("rejections")]
    public PolicyDatasetRejectionCount[] Rejections { get; set; } = Array.Empty<PolicyDatasetRejectionCount>();

    [JsonPropertyName("version_sets")]
    public PolicyDatasetVersionSet[] VersionSets { get; set; } = Array.Empty<PolicyDatasetVersionSet>();

    [JsonPropertyName("returns")]
    public PolicyDatasetReturnCoverage Returns { get; set; } = new();

    [JsonPropertyName("audit")]
    public PolicyDatasetAudit Audit { get; set; } = new();
}

public class PolicyDatasetFileDigest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("rows")]
    public int Rows { get; set; }
}

public sealed class PolicyDatasetPartitionDigest : PolicyDatasetFileDigest
{
    [JsonPropertyName("partition")]
    public string Partition { get; set; } = string.Empty;

    [JsonPropertyName("split_key_count")]
    public int SplitKeyCount { get; set; }
}

public sealed class PolicyDatasetSplitPolicy
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "save_id:year:season:day";

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "sha256_first_uint32_mod_100";

    [JsonPropertyName("train_range")]
    public string TrainRange { get; set; } = "0-79";

    [JsonPropertyName("validation_range")]
    public string ValidationRange { get; set; } = "80-89";

    [JsonPropertyName("test_range")]
    public string TestRange { get; set; } = "90-99";
}

public sealed class PolicyDatasetCounts
{
    [JsonPropertyName("input_lines")]
    public int InputLines { get; set; }

    [JsonPropertyName("accepted_rows")]
    public int AcceptedRows { get; set; }

    [JsonPropertyName("rejected_rows")]
    public int RejectedRows { get; set; }

    [JsonPropertyName("duplicate_rows")]
    public int DuplicateRows { get; set; }

    [JsonPropertyName("conflicting_duplicate_rows")]
    public int ConflictingDuplicateRows { get; set; }
}

public sealed class PolicyDatasetRejectionCount
{
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public sealed class PolicyDatasetRejection
{
    [JsonPropertyName("line_number")]
    public int LineNumber { get; set; }

    [JsonPropertyName("trajectory_id")]
    public string TrajectoryId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

public sealed class PolicyDatasetVersionSet
{
    [JsonPropertyName("feature_schema")]
    public string FeatureSchema { get; set; } = string.Empty;

    [JsonPropertyName("candidate_vocabulary")]
    public string CandidateVocabulary { get; set; } = string.Empty;

    [JsonPropertyName("capability_registry")]
    public string CapabilityRegistry { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_dictionary")]
    public string KnowledgeDictionary { get; set; } = string.Empty;

    [JsonPropertyName("compiler")]
    public string Compiler { get; set; } = string.Empty;

    [JsonPropertyName("executor")]
    public string Executor { get; set; } = string.Empty;

    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }
}

public sealed class PolicyDatasetReturnCoverage
{
    [JsonPropertyName("day_complete")]
    public int DayComplete { get; set; }

    [JsonPropertyName("season_complete")]
    public int SeasonComplete { get; set; }

    [JsonPropertyName("year_complete")]
    public int YearComplete { get; set; }

    [JsonPropertyName("grandpa_21_complete")]
    public int Grandpa21Complete { get; set; }

    [JsonPropertyName("fully_complete")]
    public int FullyComplete { get; set; }

    [JsonPropertyName("partial_observed")]
    public int PartialObserved { get; set; }

    [JsonPropertyName("pending")]
    public int Pending { get; set; }
}

public sealed class PolicyDatasetAudit
{
    [JsonPropertyName("builder")]
    public string Builder { get; set; } = "StardewAI.Core.Training.PolicyTrajectoryDatasetBuilder";

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "Invalid, conflicting, cross-schema, non-admitted, unavailable and source-incomplete rows fail closed; unclosed horizon labels remain null/pending; save-day split keys never cross partitions.";
}

public sealed class PolicyDatasetBuildResult
{
    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("rejections_path")]
    public string RejectionsPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest")]
    public PolicyDatasetManifest Manifest { get; set; } = new();
}
