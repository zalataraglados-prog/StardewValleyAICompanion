using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Execution
{
    public sealed class TimeBudgetReport
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "time_budget.v1";

        [JsonPropertyName("state_hash")]
        public string StateHash { get; set; } = string.Empty;

        [JsonPropertyName("current_time")]
        public int CurrentTime { get; set; }

        [JsonPropertyName("deadline_time")]
        public int DeadlineTime { get; set; } = 2600;

        [JsonPropertyName("safety_buffer_minutes")]
        public int SafetyBufferMinutes { get; set; }

        [JsonPropertyName("available_minutes")]
        public int AvailableMinutes { get; set; }

        [JsonPropertyName("required_minutes")]
        public int RequiredMinutes { get; set; }

        [JsonPropertyName("optional_minutes")]
        public int OptionalMinutes { get; set; }

        [JsonPropertyName("fits_required")]
        public bool FitsRequired { get; set; }

        [JsonPropertyName("fits_required_plus_optional")]
        public bool FitsRequiredPlusOptional { get; set; }

        [JsonPropertyName("execution_profile")]
        public string ExecutionProfile { get; set; } = "perfect_human_player";

        [JsonPropertyName("items")]
        public TimeBudgetItem[] Items { get; set; } = Array.Empty<TimeBudgetItem>();

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class TimeBudgetItem
    {
        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("schedule_role")]
        public string ScheduleRole { get; set; } = "required";

        [JsonPropertyName("estimated_minutes")]
        public int EstimatedMinutes { get; set; }

        [JsonPropertyName("estimator")]
        public string Estimator { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string[] Notes { get; set; } = Array.Empty<string>();
    }
}
