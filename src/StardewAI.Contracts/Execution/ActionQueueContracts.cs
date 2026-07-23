using System;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

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

    public static class ExecutionTargetProfiles
    {
        public const string TrainingSingleplayer = "training_singleplayer";
        public const string CoopCompanion = "coop_companion";
        public const string DedicatedHostAi = "dedicated_host_ai";

        public static bool IsSupported(string executionMode)
        {
            return string.Equals(executionMode, TrainingSingleplayer, StringComparison.Ordinal) ||
                string.Equals(executionMode, CoopCompanion, StringComparison.Ordinal) ||
                string.Equals(executionMode, DedicatedHostAi, StringComparison.Ordinal);
        }

        public static ActionActorRef CreateActor(string executionMode)
        {
            if (string.Equals(executionMode, DedicatedHostAi, StringComparison.Ordinal))
            {
                return new ActionActorRef
                {
                    ActorId = "ai_host.main",
                    ActorType = "ai_host",
                    ControlSurface = "dedicated_host_actor"
                };
            }

            if (string.Equals(executionMode, CoopCompanion, StringComparison.Ordinal))
            {
                return new ActionActorRef
                {
                    ActorId = "ai_companion.main",
                    ActorType = "ai_companion",
                    ControlSurface = "companion_actor"
                };
            }

            return new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            };
        }
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

    public sealed class SmallModelPlanEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "small_model_plan.v1";

        [JsonPropertyName("plan_id")]
        public string PlanId { get; set; } = string.Empty;

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

        [JsonPropertyName("plan_type")]
        public string PlanType { get; set; } = "mechanical_plan";

        [JsonPropertyName("steps")]
        public SmallModelPlanStep[] Steps { get; set; } = Array.Empty<SmallModelPlanStep>();

        [JsonPropertyName("candidate_audit")]
        public SmallModelPlanCandidateAudit[] CandidateAudit { get; set; } = Array.Empty<SmallModelPlanCandidateAudit>();
    }

    public sealed class SmallModelPlanCandidateAudit
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("decision")]
        public string Decision { get; set; } = string.Empty;

        [JsonPropertyName("reasons")]
        public string[] Reasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("candidate_minutes")]
        public int CandidateMinutes { get; set; }

        [JsonPropertyName("candidate_energy_cost")]
        public int CandidateEnergyCost { get; set; }

        [JsonPropertyName("remaining_minutes_before")]
        public int? RemainingMinutesBefore { get; set; }

        [JsonPropertyName("remaining_minutes_after")]
        public int? RemainingMinutesAfter { get; set; }

        [JsonPropertyName("remaining_energy_before")]
        public int? RemainingEnergyBefore { get; set; }

        [JsonPropertyName("remaining_energy_after")]
        public int? RemainingEnergyAfter { get; set; }
    }

    public sealed class SmallModelPlanStep
    {
        [JsonPropertyName("step_id")]
        public string StepId { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("target_location")]
        public string TargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("target_tile_x")]
        public int? TargetTileX { get; set; }

        [JsonPropertyName("target_tile_y")]
        public int? TargetTileY { get; set; }

        [JsonPropertyName("direction")]
        public int? Direction { get; set; }

        [JsonPropertyName("wait_ticks")]
        public int? WaitTicks { get; set; }

        [JsonPropertyName("estimated_minutes")]
        public int? EstimatedMinutes { get; set; }

        [JsonPropertyName("preconditions")]
        public string[] Preconditions { get; set; } = Array.Empty<string>();

        [JsonPropertyName("expected_effects")]
        public string[] ExpectedEffects { get; set; } = Array.Empty<string>();

        [JsonPropertyName("safety_constraints")]
        public string[] SafetyConstraints { get; set; } = Array.Empty<string>();

        [JsonPropertyName("failure_policy")]
        public string[] FailurePolicy { get; set; } = Array.Empty<string>();

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
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

        [JsonPropertyName("candidate_audit")]
        public SmallModelPlanCandidateAudit[] CandidateAudit { get; set; } = Array.Empty<SmallModelPlanCandidateAudit>();

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

        [JsonPropertyName("social_plan")]
        public SocialPlanEnvelope? SocialPlan { get; set; }

        [JsonPropertyName("quest_plan")]
        public QuestPlanEnvelope? QuestPlan { get; set; }
    }

    public sealed class QuestPlanEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "quest_compiler.v1";

        [JsonPropertyName("selected_candidate_id")]
        public string SelectedCandidateId { get; set; } = string.Empty;

        [JsonPropertyName("selected_quest_id")]
        public string SelectedQuestId { get; set; } = string.Empty;

        [JsonPropertyName("selected_quest_key")]
        public string SelectedQuestKey { get; set; } = string.Empty;

        [JsonPropertyName("selected_runtime_type")]
        public string SelectedRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("family")]
        public string Family { get; set; } = string.Empty;

        [JsonPropertyName("next_action_category")]
        public string NextActionCategory { get; set; } = string.Empty;

        [JsonPropertyName("required_target_npc")]
        public string RequiredTargetNpc { get; set; } = string.Empty;

        [JsonPropertyName("required_target_location")]
        public string RequiredTargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("required_item_id")]
        public string RequiredItemId { get; set; } = string.Empty;

        [JsonPropertyName("required_target_count")]
        public int RequiredTargetCount { get; set; }

        [JsonPropertyName("current_progress_count")]
        public int CurrentProgressCount { get; set; }

        [JsonPropertyName("selected_objective_index")]
        public int SelectedObjectiveIndex { get; set; } = -1;

        [JsonPropertyName("time_estimate")]
        public string TimeEstimate { get; set; } = "unknown";

        [JsonPropertyName("energy_cost")]
        public string EnergyCost { get; set; } = "unknown";

        [JsonPropertyName("executor_block_reason")]
        public string ExecutorBlockReason { get; set; } = "quest_native_executor_not_implemented";

        [JsonPropertyName("live_evidence")]
        public QuestCompilerEvidence? LiveEvidence { get; set; }
    }

    public sealed class SocialPlanEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "social_plan.v1";

        [JsonPropertyName("action_kind")]
        public string ActionKind { get; set; } = string.Empty;

        [JsonPropertyName("requested_npc_name")]
        public string RequestedNpcName { get; set; } = string.Empty;

        [JsonPropertyName("requested_slot_index")]
        public int? RequestedSlotIndex { get; set; }

        [JsonPropertyName("requested_qualified_item_id")]
        public string RequestedQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("live_legality_evidence")]
        public SmallModelActionParameter[] LiveLegalityEvidence { get; set; } = Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("time_route_constraints")]
        public SmallModelActionParameter[] TimeRouteConstraints { get; set; } = Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("expected_deterministic_outcome")]
        public SmallModelActionParameter[] ExpectedDeterministicOutcome { get; set; } = Array.Empty<SmallModelActionParameter>();

        [JsonPropertyName("required_executor_profile")]
        public string RequiredExecutorProfile { get; set; } = "social_native_executor.v1";

        [JsonPropertyName("training_recording_contract")]
        public string[] TrainingRecordingContract { get; set; } = Array.Empty<string>();
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
