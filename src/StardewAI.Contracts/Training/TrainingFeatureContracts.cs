using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingFeatureRowEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_feature_row.v1";

        [JsonPropertyName("row_id")]
        public string RowId { get; set; } = string.Empty;

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("state_features")]
        public FeatureVector StateFeatures { get; set; } = new();

        [JsonPropertyName("action_features")]
        public ActionFeatureVector ActionFeatures { get; set; } = new();

        [JsonPropertyName("labels")]
        public TrainingLabelVector Labels { get; set; } = new();

        [JsonPropertyName("audit")]
        public TrainingFeatureRowAudit Audit { get; set; } = new();
    }

    public sealed class FeatureVector
    {
        [JsonPropertyName("numeric")]
        public NumericFeature[] Numeric { get; set; } = Array.Empty<NumericFeature>();

        [JsonPropertyName("categorical")]
        public CategoricalFeature[] Categorical { get; set; } = Array.Empty<CategoricalFeature>();

        [JsonPropertyName("boolean")]
        public BooleanFeature[] Boolean { get; set; } = Array.Empty<BooleanFeature>();
    }

    public sealed class NumericFeature
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    public sealed class CategoricalFeature
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public sealed class BooleanFeature
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public bool Value { get; set; }
    }

    public sealed class ActionFeatureVector
    {
        [JsonPropertyName("option_ids")]
        public string[] OptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = "unknown";

        [JsonPropertyName("learning_scope")]
        public string LearningScope { get; set; } = "unknown";

        [JsonPropertyName("exclude_from_policy_training")]
        public bool ExcludeFromPolicyTraining { get; set; }

        [JsonPropertyName("features")]
        public FeatureVector Features { get; set; } = new();
    }

    public sealed class TrainingLabelVector
    {
        [JsonPropertyName("goal_progress_delta")]
        public double GoalProgressDelta { get; set; }

        [JsonPropertyName("total_reward")]
        public double TotalReward { get; set; }

        [JsonPropertyName("hard_blocked")]
        public bool HardBlocked { get; set; }

        [JsonPropertyName("required_minutes")]
        public int RequiredMinutes { get; set; }

        [JsonPropertyName("available_minutes")]
        public int AvailableMinutes { get; set; }

        [JsonPropertyName("reward_term_names")]
        public string[] RewardTermNames { get; set; } = Array.Empty<string>();

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class TrainingFeatureRowAudit
    {
        [JsonPropertyName("exporter")]
        public string Exporter { get; set; } = "StardewAI.Core.Training.TrainingFeatureRowExporter";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Feature rows are exported from world_model.v1 and training_episode.v1 only; missing facts are encoded as explicit defaults or unknown categories, not guessed.";
    }

    public sealed class TrainingDatasetAppendResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_dataset_append.v1";

        [JsonPropertyName("dataset_path")]
        public string DatasetPath { get; set; } = string.Empty;

        [JsonPropertyName("row_id")]
        public string RowId { get; set; } = string.Empty;

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("bytes_written")]
        public int BytesWritten { get; set; }

        [JsonPropertyName("row_count")]
        public int RowCount { get; set; }
    }

    public sealed class BaselineTrainingReport
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "baseline_training_report.v1";

        [JsonPropertyName("dataset_path")]
        public string DatasetPath { get; set; } = string.Empty;

        [JsonPropertyName("row_count")]
        public int RowCount { get; set; }

        [JsonPropertyName("included_row_count")]
        public int IncludedRowCount { get; set; }

        [JsonPropertyName("excluded_calibration_row_count")]
        public int ExcludedCalibrationRowCount { get; set; }

        [JsonPropertyName("excluded_reasons")]
        public string[] ExcludedReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("option_scores")]
        public BaselineOptionScore[] OptionScores { get; set; } = Array.Empty<BaselineOptionScore>();

        [JsonPropertyName("audit")]
        public BaselineTrainingAudit Audit { get; set; } = new();
    }

    public sealed class BaselineOptionScore
    {
        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("example_count")]
        public int ExampleCount { get; set; }

        [JsonPropertyName("average_goal_progress_delta")]
        public double AverageGoalProgressDelta { get; set; }

        [JsonPropertyName("average_total_reward")]
        public double AverageTotalReward { get; set; }

        [JsonPropertyName("hard_block_rate")]
        public double HardBlockRate { get; set; }
    }

    public sealed class BaselineTrainingAudit
    {
        [JsonPropertyName("trainer")]
        public string Trainer { get; set; } = "StardewAI.Core.Training.BaselineFeatureRowTrainer";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Baseline trainer only aggregates feature-row labels by option id; it is a smoke-test trainer, not a learned policy model.";
    }

    public sealed class BaselinePredictionRequest
    {
        [JsonPropertyName("dataset_path")]
        public string? DatasetPath { get; set; }

        [JsonPropertyName("candidate_option_ids")]
        public string[] CandidateOptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("training_report")]
        public BaselineTrainingReport? TrainingReport { get; set; }
    }

    public sealed class PolicyPredictionEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "policy_prediction.v1";

        [JsonPropertyName("source")]
        public string Source { get; set; } = "baseline_training_report.v1";

        [JsonPropertyName("ranked_options")]
        public PolicyOptionPrediction[] RankedOptions { get; set; } = Array.Empty<PolicyOptionPrediction>();

        [JsonPropertyName("audit")]
        public PolicyPredictionAudit Audit { get; set; } = new();
    }

    public sealed class PolicyOptionPrediction
    {
        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("expected_reward")]
        public double ExpectedReward { get; set; }

        [JsonPropertyName("expected_goal_progress_delta")]
        public double ExpectedGoalProgressDelta { get; set; }

        [JsonPropertyName("hard_block_risk")]
        public double HardBlockRisk { get; set; }

        [JsonPropertyName("example_count")]
        public int ExampleCount { get; set; }

        [JsonPropertyName("evidence")]
        public string Evidence { get; set; } = "unseen_option";
    }

    public sealed class PolicyPredictionAudit
    {
        [JsonPropertyName("predictor")]
        public string Predictor { get; set; } = "StardewAI.Core.Training.BaselinePolicyPredictor";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Baseline prediction ranks options from aggregated training labels; unseen options are retained with neutral reward and high uncertainty penalty.";
    }
}
