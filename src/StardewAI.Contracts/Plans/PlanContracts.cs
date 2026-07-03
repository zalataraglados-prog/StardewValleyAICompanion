using System.Text.Json.Serialization;
using StardewAI.Contracts.Options;

namespace StardewAI.Contracts.Plans
{
    public sealed class Plan
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "plan.v1";

        [JsonPropertyName("plan_id")]
        public string PlanId { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public OptionInstance[] Options { get; set; } = new OptionInstance[0];
    }

    public sealed class PreconditionResult
    {
        [JsonPropertyName("state_factor")]
        public string StateFactor { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "unknown";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SafetyResult
    {
        [JsonPropertyName("feasibility")]
        public string Feasibility { get; set; } = "unknown";

        [JsonPropertyName("missing_state_factors")]
        public string[] MissingStateFactors { get; set; } = new string[0];

        [JsonPropertyName("precondition_results")]
        public PreconditionResult[] PreconditionResults { get; set; } = new PreconditionResult[0];

        [JsonPropertyName("blocking_reasons")]
        public string[] BlockingReasons { get; set; } = new string[0];
    }
}
