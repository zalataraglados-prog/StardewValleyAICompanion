using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class GoalMethodPairwiseVersionPins
{
    public const string CheckpointSchema =
        "acquisition_route_goal_method_checkpoint.v1";
    public const string ModelKind =
        "explicit_teacher_pairwise_portfolio_ranker.v1";
    public const string FeatureSchema =
        "acquisition_route_goal_method_features.v1";
    public const string CorpusSchema =
        "acquisition_route_portfolio_supervision_corpus_manifest.v1";
    public const string SupervisionRowSchema =
        "acquisition_route_portfolio_supervision_row.v1";
}

public sealed class GoalMethodPairwiseHyperparameters
{
    [JsonPropertyName("epochs")]
    public int Epochs { get; set; } = 200;

    [JsonPropertyName("learning_rate")]
    public double LearningRate { get; set; } = 0.05;

    [JsonPropertyName("l2_regularization")]
    public double L2Regularization { get; set; } = 0.001;
}

public sealed class GoalMethodPairwiseDatasetBinding
{
    [JsonPropertyName("corpus_manifest_path")]
    public string CorpusManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("corpus_manifest_sha256")]
    public string CorpusManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("cleaned_sha256")]
    public string CleanedSha256 { get; set; } = string.Empty;

    [JsonPropertyName("train_sha256")]
    public string TrainSha256 { get; set; } = string.Empty;

    [JsonPropertyName("validation_sha256")]
    public string ValidationSha256 { get; set; } = string.Empty;

    [JsonPropertyName("test_sha256")]
    public string TestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_count")]
    public int SourceCount { get; set; }
}

public sealed class GoalMethodPairwiseVersionBinding
{
    [JsonPropertyName("feature_schema")]
    public string FeatureSchema { get; set; } =
        GoalMethodPairwiseVersionPins.FeatureSchema;

    [JsonPropertyName("corpus_schema")]
    public string CorpusSchema { get; set; } =
        GoalMethodPairwiseVersionPins.CorpusSchema;

    [JsonPropertyName("supervision_row_schema")]
    public string SupervisionRowSchema { get; set; } =
        GoalMethodPairwiseVersionPins.SupervisionRowSchema;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("bridge_version")]
    public string BridgeVersion { get; set; } = string.Empty;
}

public sealed class GoalMethodPairwiseLinearModel
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

public sealed class GoalMethodPairwiseTrainingSummary
{
    [JsonPropertyName("train_rows")]
    public int TrainRows { get; set; }

    [JsonPropertyName("train_pairs")]
    public int TrainPairs { get; set; }

    [JsonPropertyName("train_pair_accuracy")]
    public double TrainPairAccuracy { get; set; }

    [JsonPropertyName("validation_rows")]
    public int ValidationRows { get; set; }

    [JsonPropertyName("validation_pairs")]
    public int ValidationPairs { get; set; }

    [JsonPropertyName("validation_pair_accuracy")]
    public double ValidationPairAccuracy { get; set; }

    [JsonPropertyName("test_rows")]
    public int TestRows { get; set; }

    [JsonPropertyName("test_pairs")]
    public int TestPairs { get; set; }

    [JsonPropertyName("test_pair_accuracy")]
    public double TestPairAccuracy { get; set; }

    [JsonPropertyName("feature_count")]
    public int FeatureCount { get; set; }
}

public sealed class GoalMethodPairwiseInitializationBinding
{
    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("inherited_feature_count")]
    public int InheritedFeatureCount { get; set; }

    [JsonPropertyName("new_feature_count")]
    public int NewFeatureCount { get; set; }
}

public sealed class GoalMethodPairwiseCheckpoint
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        GoalMethodPairwiseVersionPins.CheckpointSchema;

    [JsonPropertyName("checkpoint_id")]
    public string CheckpointId { get; set; } = string.Empty;

    [JsonPropertyName("model_kind")]
    public string ModelKind { get; set; } =
        GoalMethodPairwiseVersionPins.ModelKind;

    [JsonPropertyName("dataset")]
    public GoalMethodPairwiseDatasetBinding Dataset { get; set; } = new();

    [JsonPropertyName("versions")]
    public GoalMethodPairwiseVersionBinding Versions { get; set; } = new();

    [JsonPropertyName("hyperparameters")]
    public GoalMethodPairwiseHyperparameters Hyperparameters { get; set; } =
        new();

    [JsonPropertyName("model")]
    public GoalMethodPairwiseLinearModel Model { get; set; } = new();

    [JsonPropertyName("training")]
    public GoalMethodPairwiseTrainingSummary Training { get; set; } = new();

    [JsonPropertyName("initialization")]
    public GoalMethodPairwiseInitializationBinding? Initialization
    { get; set; }

    [JsonPropertyName("teacher_label_source")]
    public string TeacherLabelSource { get; set; } =
        "explicit_teacher_pairwise_preferences";

    [JsonPropertyName("uses_candidate_selected_flag")]
    public bool UsesCandidateSelectedFlag { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("checkpoint_policy")]
    public string CheckpointPolicy { get; set; } =
        "The trainer rebuilds every source supervision dataset from its rollout proof and admission, then learns only from explicit Teacher pairwise preferences. Save identity, proposal hashes, learner scores, unavailable candidates, native outcomes and Student missingness do not create labels. This checkpoint scores admitted goal-to-method portfolios only; deterministic admission, compilation and execution remain authoritative, and formal product training is not authorized.";
}

public sealed class GoalMethodPairwiseTrainingResult
{
    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_sha256")]
    public string CheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint")]
    public GoalMethodPairwiseCheckpoint Checkpoint { get; set; } = new();
}
