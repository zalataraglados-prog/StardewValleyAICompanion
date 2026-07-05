using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingSampleEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_sample.v1";

        [JsonPropertyName("sample_id")]
        public string SampleId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("source_world_model_schema")]
        public string SourceWorldModelSchema { get; set; } = string.Empty;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public TrainingTarget Target { get; set; } = new();

        [JsonPropertyName("planner_state")]
        public PlannerGoalState PlannerState { get; set; } = new();

        [JsonPropertyName("candidate_directions")]
        public CandidateDirection[] CandidateDirections { get; set; } = Array.Empty<CandidateDirection>();

        [JsonPropertyName("feedback")]
        public TrainingFeedback Feedback { get; set; } = new();

        [JsonPropertyName("audit")]
        public TrainingSampleAudit Audit { get; set; } = new();
    }

    public sealed class TrainingTarget
    {
        [JsonPropertyName("metric")]
        public string Metric { get; set; } = "grandpa_score";

        [JsonPropertyName("target_value")]
        public int TargetValue { get; set; }

        [JsonPropertyName("current_value")]
        public int CurrentValue { get; set; }

        [JsonPropertyName("points_needed")]
        public int PointsNeeded { get; set; }

        [JsonPropertyName("complete")]
        public bool Complete { get; set; }
    }

    public sealed class PlannerGoalState
    {
        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("missing_fact_paths")]
        public string[] MissingFactPaths { get; set; } = Array.Empty<string>();

        [JsonPropertyName("evaluation_context")]
        public string EvaluationContext { get; set; } = string.Empty;
    }

    public sealed class CandidateDirection
    {
        [JsonPropertyName("direction_id")]
        public string DirectionId { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("related_factor_ids")]
        public string[] RelatedFactorIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("potential_points")]
        public int PotentialPoints { get; set; }

        [JsonPropertyName("known")]
        public bool Known { get; set; }

        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("priority_score")]
        public double PriorityScore { get; set; }

        [JsonPropertyName("feedback_key")]
        public string FeedbackKey { get; set; } = string.Empty;
    }

    public sealed class TrainingFeedback
    {
        [JsonPropertyName("feedback_mode")]
        public string FeedbackMode { get; set; } = "observed_state_delta";

        [JsonPropertyName("executor_required")]
        public bool ExecutorRequired { get; set; }

        [JsonPropertyName("available_now")]
        public bool AvailableNow { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "future transparent snapshots after human or external execution";

        [JsonPropertyName("observed_delta")]
        public ObservedStateDelta ObservedDelta { get; set; } = new();
    }

    public sealed class ObservedStateDelta
    {
        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("score_delta")]
        public int? ScoreDelta { get; set; }

        [JsonPropertyName("completed_direction_ids")]
        public string[] CompletedDirectionIds { get; set; } = Array.Empty<string>();
    }

    public sealed class TrainingSampleAudit
    {
        [JsonPropertyName("adapter")]
        public string Adapter { get; set; } = "StardewAI.Core.Training.GrandpaTrainingSampleAdapter";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Deterministic adapter; no action execution and no guessed feedback.";
    }
}
