using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingExecutionRequest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_execution_request.v1";

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public string Actor { get; set; } = "training_farmer.main";

        [JsonPropertyName("save_isolation_path")]
        public string SaveIsolationPath { get; set; } = string.Empty;

        [JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("max_crops")]
        public int MaxCrops { get; set; } = 512;
    }

    public sealed class TrainingExecutionResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_execution_result.v1";

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("feedback_available")]
        public bool FeedbackAvailable { get; set; }

        [JsonPropertyName("watered_count")]
        public int WateredCount { get; set; }

        [JsonPropertyName("energy_before")]
        public double EnergyBefore { get; set; }

        [JsonPropertyName("energy_after")]
        public double EnergyAfter { get; set; }

        [JsonPropertyName("started_at")]
        public string StartedAt { get; set; } = string.Empty;

        [JsonPropertyName("completed_at")]
        public string CompletedAt { get; set; } = string.Empty;

        [JsonPropertyName("changed_facts")]
        public SimulatedFactChange[] ChangedFacts { get; set; } = Array.Empty<SimulatedFactChange>();

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }
}
