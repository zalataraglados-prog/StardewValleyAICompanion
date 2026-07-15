using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingEpisodeEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_episode.v1";

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("source_model")]
        public string SourceModel { get; set; } = string.Empty;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("action_summary")]
        public EpisodeActionSummary ActionSummary { get; set; } = new();

        [JsonPropertyName("strategy_value")]
        public StrategyValueFeedback StrategyValue { get; set; } = new();

        [JsonPropertyName("hard_feasibility")]
        public HardFeasibilityFeedback HardFeasibility { get; set; } = new();

        [JsonPropertyName("executor_calibration")]
        public ExecutorCalibrationFeedback ExecutorCalibration { get; set; } = new();

        [JsonPropertyName("candidate_audit")]
        public SmallModelPlanCandidateAudit[] CandidateAudit { get; set; } = Array.Empty<SmallModelPlanCandidateAudit>();

        [JsonPropertyName("audit")]
        public TrainingEpisodeAudit Audit { get; set; } = new();
    }

    public sealed class EpisodeActionSummary
    {
        [JsonPropertyName("option_ids")]
        public string[] OptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();
    }

    public sealed class StrategyValueFeedback
    {
        [JsonPropertyName("goal_progress_delta")]
        public double GoalProgressDelta { get; set; }

        [JsonPropertyName("reward_terms")]
        public EpisodeRewardTerm[] RewardTerms { get; set; } = Array.Empty<EpisodeRewardTerm>();

        [JsonPropertyName("excluded_executor_failures")]
        public string[] ExcludedExecutorFailures { get; set; } = Array.Empty<string>();
    }

    public sealed class EpisodeRewardTerm
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    public sealed class HardFeasibilityFeedback
    {
        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("time_budget")]
        public TimeBudgetReport TimeBudget { get; set; } = new();
    }

    public sealed class ExecutorCalibrationFeedback
    {
        [JsonPropertyName("execution_profile")]
        public string ExecutionProfile { get; set; } = "perfect_human_player";

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("applied_option_ids")]
        public string[] AppliedOptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("changed_facts")]
        public SimulatedFactChange[] ChangedFacts { get; set; } = Array.Empty<SimulatedFactChange>();

        [JsonPropertyName("resource_costs")]
        public SimulatedResourceCost[] ResourceCosts { get; set; } = Array.Empty<SimulatedResourceCost>();

        [JsonPropertyName("duration_items")]
        public TimeBudgetItem[] DurationItems { get; set; } = Array.Empty<TimeBudgetItem>();

        [JsonPropertyName("calibration_notes")]
        public string[] CalibrationNotes { get; set; } = Array.Empty<string>();
    }

    public sealed class TrainingEpisodeAudit
    {
        [JsonPropertyName("adapter")]
        public string Adapter { get; set; } = "StardewAI.Core.Training.TrainingEpisodeAdapter";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Strategy value, hard feasibility, and executor calibration are separated to avoid training preference from low-level executor errors.";
    }

    public sealed class PlanExecutionEpisodeEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "plan_execution_episode.v1";

        [JsonPropertyName("episode_id")]
        public string EpisodeId { get; set; } = string.Empty;

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("state_hash_changed")]
        public bool StateHashChanged { get; set; }

        [JsonPropertyName("before_game_tick")]
        public long BeforeGameTick { get; set; }

        [JsonPropertyName("after_game_tick")]
        public long AfterGameTick { get; set; }

        [JsonPropertyName("after_snapshot_fresh")]
        public bool AfterSnapshotFresh { get; set; }

        [JsonPropertyName("after_snapshot_note")]
        public string AfterSnapshotNote { get; set; } = string.Empty;

        [JsonPropertyName("model_plan_path")]
        public string ModelPlanPath { get; set; } = string.Empty;

        [JsonPropertyName("compiled_queue_path")]
        public string CompiledQueuePath { get; set; } = string.Empty;

        [JsonPropertyName("execution_result_path")]
        public string ExecutionResultPath { get; set; } = string.Empty;

        [JsonPropertyName("before_snapshot_path")]
        public string BeforeSnapshotPath { get; set; } = string.Empty;

        [JsonPropertyName("after_snapshot_path")]
        public string AfterSnapshotPath { get; set; } = string.Empty;

        [JsonPropertyName("dataset_path")]
        public string DatasetPath { get; set; } = string.Empty;

        [JsonPropertyName("row_id")]
        public string RowId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("reward")]
        public double Reward { get; set; }

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = "executor_calibration";

        [JsonPropertyName("failure_attribution")]
        public string FailureAttribution { get; set; } = string.Empty;

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("effective_queue_item")]
        public JsonElement? EffectiveQueueItem { get; set; }

        [JsonPropertyName("primitive_kind")]
        public string PrimitiveKind { get; set; } = string.Empty;

        [JsonPropertyName("primitive_verification_status")]
        public string PrimitiveVerificationStatus { get; set; } = string.Empty;

        [JsonPropertyName("primitive_verification_reasons")]
        public string[] PrimitiveVerificationReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("requested_effect")]
        public string RequestedEffect { get; set; } = string.Empty;

        [JsonPropertyName("observed_effect")]
        public string ObservedEffect { get; set; } = string.Empty;

        [JsonPropertyName("changed_facts")]
        public JsonElement ChangedFacts { get; set; }

        [JsonPropertyName("audit")]
        public PlanExecutionEpisodeAudit Audit { get; set; } = new();
    }

    public sealed class PlanExecutionEpisodeAudit
    {
        [JsonPropertyName("writer")]
        public string Writer { get; set; } = "StardewAI.LiveTrainingLoop";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Plan execution episodes preserve model plan, compiled queue, runtime execution result, before/after snapshots, and labels so model training is grounded in execution feedback.";
    }
}
