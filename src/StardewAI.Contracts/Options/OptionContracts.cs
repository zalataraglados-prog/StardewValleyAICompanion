using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Options
{
    public static class OptionBehaviorCategories
    {
        public const string Mechanical = "mechanical";
        public const string ParameterizedMechanical = "parameterized_mechanical";
        public const string SpatialPlanning = "spatial_planning";
        public const string EconomicStrategic = "economic_strategic";
        public const string SocialStrategic = "social_strategic";
        public const string ExplorationUncertain = "exploration_uncertain";
        public const string LongTermStrategic = "long_term_strategic";
        public const string Recovery = "recovery";
        public const string Unknown = "unknown";
    }

    public static class CompilerResponsibilities
    {
        public const string FullActionExpansion = "full_action_expansion";
        public const string ParameterExpansion = "parameter_expansion";
        public const string PlanValidation = "plan_validation";
        public const string StrategySelectionOnly = "strategy_selection_only";
        public const string Unsupported = "unsupported";
        public const string Unknown = "unknown";
    }

    public static class TrainingRoles
    {
        public const string ExecutorCalibration = "executor_calibration";
        public const string StrategyValue = "strategy_value";
        public const string Mixed = "mixed";
        public const string Unknown = "unknown";
    }

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

        [JsonPropertyName("behavior_category")]
        public string BehaviorCategory { get; set; } = OptionBehaviorCategories.Unknown;

        [JsonPropertyName("compiler_responsibility")]
        public string CompilerResponsibility { get; set; } = CompilerResponsibilities.Unknown;

        [JsonPropertyName("training_role")]
        public string TrainingRole { get; set; } = TrainingRoles.Unknown;

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
