using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Options
{
    public sealed class OptionSpec
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "option_spec.v1";

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("required_state_factors")]
        public string[] RequiredStateFactors { get; set; } = new string[0];

        [JsonPropertyName("estimated_effects")]
        public string[] EstimatedEffects { get; set; } = new string[0];

        [JsonPropertyName("irreversible_effects")]
        public string[] IrreversibleEffects { get; set; } = new string[0];

        [JsonPropertyName("safety_constraints")]
        public string[] SafetyConstraints { get; set; } = new string[0];

        [JsonPropertyName("recoverability")]
        public string Recoverability { get; set; } = "recoverable";

        [JsonPropertyName("risk_level")]
        public string RiskLevel { get; set; } = "low";
    }

    public sealed class OptionInstance
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "option_instance.v1";

        [JsonPropertyName("instance_id")]
        public string InstanceId { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("bound_goal_id")]
        public string BoundGoalId { get; set; } = string.Empty;

        [JsonPropertyName("bound_parameters")]
        public object BoundParameters { get; set; } = new object();
    }
}
