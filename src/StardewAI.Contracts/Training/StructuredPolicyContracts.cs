using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public static class StructuredPolicyVersionPins
{
    public const string CheckpointSchema = "structured_policy_checkpoint.v1";
    public const string ModelKind = "return_weighted_pairwise_linear_ranker.v1";
}

public sealed class StructuredPolicyTrainingRequest
{
    [JsonPropertyName("dataset_manifest_path")]
    public string DatasetManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("hyperparameters")]
    public StructuredPolicyHyperparameters Hyperparameters { get; set; } = new();
}

public sealed class StructuredPolicyCheckpointEnvelope
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = StructuredPolicyVersionPins.CheckpointSchema;

    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("model_kind")]
    public string ModelKind { get; set; } = StructuredPolicyVersionPins.ModelKind;

    [JsonPropertyName("dataset")]
    public StructuredPolicyDatasetBinding Dataset { get; set; } = new();

    [JsonPropertyName("versions")]
    public PolicyDatasetVersionSet Versions { get; set; } = new();

    [JsonPropertyName("hyperparameters")]
    public StructuredPolicyHyperparameters Hyperparameters { get; set; } = new();

    [JsonPropertyName("model")]
    public StructuredPolicyLinearModel Model { get; set; } = new();

    [JsonPropertyName("training")]
    public StructuredPolicyTrainingSummary Training { get; set; } = new();

    [JsonPropertyName("audit")]
    public StructuredPolicyCheckpointAudit Audit { get; set; } = new();
}

public sealed class StructuredPolicyDatasetBinding
{
    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("cleaned_sha256")]
    public string CleanedSha256 { get; set; } = string.Empty;

    [JsonPropertyName("train_sha256")]
    public string TrainSha256 { get; set; } = string.Empty;

    [JsonPropertyName("validation_sha256")]
    public string ValidationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("test_sha256")]
    public string TestSha256 { get; set; } = string.Empty;
}

public sealed class StructuredPolicyHyperparameters
{
    [JsonPropertyName("epochs")]
    public int Epochs { get; set; } = 200;

    [JsonPropertyName("learning_rate")]
    public double LearningRate { get; set; } = 0.05;

    [JsonPropertyName("l2_regularization")]
    public double L2Regularization { get; set; } = 0.001;

    [JsonPropertyName("max_return_weight")]
    public double MaxReturnWeight { get; set; } = 5;
}

public sealed class StructuredPolicyLinearModel
{
    [JsonPropertyName("feature_names")]
    public string[] FeatureNames { get; set; } = Array.Empty<string>();

    [JsonPropertyName("feature_means")]
    public double[] FeatureMeans { get; set; } = Array.Empty<double>();

    [JsonPropertyName("feature_scales")]
    public double[] FeatureScales { get; set; } = Array.Empty<double>();

    [JsonPropertyName("weights")]
    public double[] Weights { get; set; } = Array.Empty<double>();
}

public sealed class StructuredPolicyTrainingSummary
{
    [JsonPropertyName("train_rows")]
    public int TrainRows { get; set; }

    [JsonPropertyName("train_pairs")]
    public int TrainPairs { get; set; }

    [JsonPropertyName("feature_count")]
    public int FeatureCount { get; set; }

    [JsonPropertyName("train_pair_accuracy")]
    public double TrainPairAccuracy { get; set; }

    [JsonPropertyName("validation_rows")]
    public int ValidationRows { get; set; }

    [JsonPropertyName("validation_pairs")]
    public int ValidationPairs { get; set; }

    [JsonPropertyName("validation_pair_accuracy")]
    public double? ValidationPairAccuracy { get; set; }

    [JsonPropertyName("test_rows")]
    public int TestRows { get; set; }

    [JsonPropertyName("test_pairs")]
    public int TestPairs { get; set; }

    [JsonPropertyName("test_pair_accuracy")]
    public double? TestPairAccuracy { get; set; }
}

public sealed class StructuredPolicyCheckpointAudit
{
    [JsonPropertyName("trainer")]
    public string Trainer { get; set; } = "StardewAI.Core.Training.StructuredPolicyTrainer";

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = "The learned model only scores complete evidence-admitted candidate sets. Candidate generation, hard constraints, compilation and execution remain deterministic authorities.";
}

public sealed class StructuredPolicyTrainingResult
{
    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint")]
    public StructuredPolicyCheckpointEnvelope Checkpoint { get; set; } = new();
}
