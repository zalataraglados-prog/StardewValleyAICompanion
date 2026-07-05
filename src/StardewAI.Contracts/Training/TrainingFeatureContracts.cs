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
}
