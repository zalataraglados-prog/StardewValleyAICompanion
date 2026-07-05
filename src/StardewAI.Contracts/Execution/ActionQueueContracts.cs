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

        [JsonPropertyName("actions")]
        public SmallModelAction[] Actions { get; set; } = Array.Empty<SmallModelAction>();
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

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
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

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("results")]
        public ExecutionItemResult[] Results { get; set; } = Array.Empty<ExecutionItemResult>();
    }

    public sealed class ExecutionItemResult
    {
        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }
}
