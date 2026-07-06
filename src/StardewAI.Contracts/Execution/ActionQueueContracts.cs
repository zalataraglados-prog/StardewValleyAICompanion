using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Execution
{
    public sealed class SmallModelActionEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "small_model_action.v1";

        [JsonPropertyName("model_output_id")]
        public string ModelOutputId { get; set; } = string.Empty;

        [JsonPropertyName("source_model")]
        public string SourceModel { get; set; } = string.Empty;

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();

        [JsonPropertyName("actions")]
        public SmallModelAction[] Actions { get; set; } = Array.Empty<SmallModelAction>();
    }

    public sealed class ActionActorRef
    {
        [JsonPropertyName("actor_id")]
        public string ActorId { get; set; } = string.Empty;

        [JsonPropertyName("actor_type")]
        public string ActorType { get; set; } = "ai_companion";

        [JsonPropertyName("control_surface")]
        public string ControlSurface { get; set; } = "companion_actor";
    }

    public sealed class SmallModelAction
    {
        [JsonPropertyName("action_id")]
        public string ActionId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("rationale")]
        public string Rationale { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
    }

    public sealed class SmallModelActionParameter
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public sealed class ActionQueueEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "action_queue.v1";

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("source_model_output_id")]
        public string SourceModelOutputId { get; set; } = string.Empty;

        [JsonPropertyName("source_model")]
        public string SourceModel { get; set; } = string.Empty;

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [JsonPropertyName("items")]
        public ActionQueueItem[] Items { get; set; } = Array.Empty<ActionQueueItem>();

        [JsonPropertyName("compiler_diagnostics")]
        public string[] CompilerDiagnostics { get; set; } = Array.Empty<string>();

        [JsonPropertyName("audit")]
        public ActionQueueAudit Audit { get; set; } = new();
    }

    public sealed class ActionQueueItem
    {
        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("source_action_id")]
        public string SourceActionId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [JsonPropertyName("permission_required")]
        public string PermissionRequired { get; set; } = "executor";

        [JsonPropertyName("behavior_category")]
        public string BehaviorCategory { get; set; } = "unknown";

        [JsonPropertyName("compiler_responsibility")]
        public string CompilerResponsibility { get; set; } = "unknown";

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = "unknown";

        [JsonPropertyName("required_state_factors")]
        public string[] RequiredStateFactors { get; set; } = Array.Empty<string>();

        [JsonPropertyName("missing_state_factors")]
        public string[] MissingStateFactors { get; set; } = Array.Empty<string>();

        [JsonPropertyName("precondition_results")]
        public ActionQueuePrecondition[] PreconditionResults { get; set; } = Array.Empty<ActionQueuePrecondition>();

        [JsonPropertyName("blocking_reasons")]
        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("normalized_command")]
        public NormalizedCommand NormalizedCommand { get; set; } = new();
    }

    public sealed class ActionQueuePrecondition
    {
        [JsonPropertyName("state_factor")]
        public string StateFactor { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "unknown";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class NormalizedCommand
    {
        [JsonPropertyName("command_type")]
        public string CommandType { get; set; } = "option_request";

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("behavior_category")]
        public string BehaviorCategory { get; set; } = "unknown";

        [JsonPropertyName("compiler_responsibility")]
        public string CompilerResponsibility { get; set; } = "unknown";

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = "unknown";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("steps")]
        public CompiledActionStep[] Steps { get; set; } = Array.Empty<CompiledActionStep>();

        [JsonPropertyName("strategy_plan")]
        public StrategyPlanStep[] StrategyPlan { get; set; } = Array.Empty<StrategyPlanStep>();
    }

    public sealed class CompiledActionStep
    {
        [JsonPropertyName("step_id")]
        public string StepId { get; set; } = string.Empty;

        [JsonPropertyName("step_type")]
        public string StepType { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("expected_effect")]
        public string ExpectedEffect { get; set; } = string.Empty;

        [JsonPropertyName("estimated_ticks")]
        public int EstimatedTicks { get; set; }
    }

    public sealed class StrategyPlanStep
    {
        [JsonPropertyName("step_id")]
        public string StepId { get; set; } = string.Empty;

        [JsonPropertyName("direction_id")]
        public string DirectionId { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("potential_points")]
        public int PotentialPoints { get; set; }

        [JsonPropertyName("priority_score")]
        public double PriorityScore { get; set; }

        [JsonPropertyName("feedback_key")]
        public string FeedbackKey { get; set; } = string.Empty;

        [JsonPropertyName("required_minutes")]
        public int RequiredMinutes { get; set; }

        [JsonPropertyName("optional_minutes")]
        public int OptionalMinutes { get; set; }

        [JsonPropertyName("hard_preconditions")]
        public string[] HardPreconditions { get; set; } = Array.Empty<string>();

        [JsonPropertyName("resource_budget")]
        public string[] ResourceBudget { get; set; } = Array.Empty<string>();

        [JsonPropertyName("executor_handoff_option")]
        public string ExecutorHandoffOption { get; set; } = string.Empty;
    }

    public sealed class ActionQueueAudit
    {
        [JsonPropertyName("compiler")]
        public string Compiler { get; set; } = "StardewAI.Core.Execution.ActionQueueCompiler";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "Small-model output must compile to registered options before any executor can consume it.";
    }

    public sealed class ExecutionBatchResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "execution_batch_result.v1";

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("executor_mode")]
        public string ExecutorMode { get; set; } = "dry_run";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("after_state_hash")]
        public string AfterStateHash { get; set; } = string.Empty;

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("feedback_available")]
        public bool FeedbackAvailable { get; set; }

        [JsonPropertyName("completed_option_ids")]
        public string[] CompletedOptionIds { get; set; } = Array.Empty<string>();

        [JsonPropertyName("results")]
        public ExecutionItemResult[] Results { get; set; } = Array.Empty<ExecutionItemResult>();
    }

    public sealed class ExecutionItemResult
    {
        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("actor")]
        public ActionActorRef Actor { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("feedback_key")]
        public string FeedbackKey { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
