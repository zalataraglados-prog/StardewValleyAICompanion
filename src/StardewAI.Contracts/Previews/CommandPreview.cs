using System.Text.Json.Serialization;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;

namespace StardewAI.Contracts.Previews
{
    public sealed class CommandPreview
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "command_preview.v1";

        [JsonPropertyName("command_id")]
        public string CommandId { get; set; } = string.Empty;

        [JsonPropertyName("goal")]
        public GoalSpec Goal { get; set; } = new GoalSpec();

        [JsonPropertyName("selected_option")]
        public OptionSpec SelectedOption { get; set; } = new OptionSpec();

        [JsonPropertyName("option_instance")]
        public OptionInstance OptionInstance { get; set; } = new OptionInstance();

        [JsonPropertyName("plan")]
        public Plan Plan { get; set; } = new Plan();

        [JsonPropertyName("feasibility")]
        public string Feasibility { get; set; } = "unknown";

        [JsonPropertyName("preview_only")]
        public bool PreviewOnly { get; set; } = true;

        [JsonPropertyName("execution_permission")]
        public string ExecutionPermission { get; set; } = "disabled";

        [JsonPropertyName("would_be_executable")]
        public bool WouldBeExecutable { get; set; }

        [JsonPropertyName("would_be_read_eligible")]
        public bool WouldBeReadEligible { get; set; }

        [JsonPropertyName("would_bind")]
        public bool WouldBind { get; set; }

        [JsonPropertyName("would_compile")]
        public bool WouldCompile { get; set; }

        [JsonPropertyName("would_require_confirmation")]
        public bool WouldRequireConfirmation { get; set; }

        [JsonPropertyName("would_be_execution_authorized")]
        public bool WouldBeExecutionAuthorized { get; set; }

        [JsonPropertyName("required_state_factors")]
        public string[] RequiredStateFactors { get; set; } = new string[0];

        [JsonPropertyName("missing_state_factors")]
        public string[] MissingStateFactors { get; set; } = new string[0];

        [JsonPropertyName("precondition_results")]
        public PreconditionResult[] PreconditionResults { get; set; } = new PreconditionResult[0];

        [JsonPropertyName("expected_effects")]
        public string[] ExpectedEffects { get; set; } = new string[0];

        [JsonPropertyName("irreversible_effects")]
        public string[] IrreversibleEffects { get; set; } = new string[0];

        [JsonPropertyName("risk_level")]
        public string RiskLevel { get; set; } = "unknown";

        [JsonPropertyName("recoverability")]
        public string Recoverability { get; set; } = "unknown";

        [JsonPropertyName("blocking_reasons")]
        public string[] BlockingReasons { get; set; } = new string[0];
    }
}
