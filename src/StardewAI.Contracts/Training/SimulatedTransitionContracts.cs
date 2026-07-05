using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class SimulatedTransitionResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "simulated_transition.v1";

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

        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class SimulatedFactChange
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("before")]
        public string Before { get; set; } = string.Empty;

        [JsonPropertyName("after")]
        public string After { get; set; } = string.Empty;
    }

    public sealed class SimulatedResourceCost
    {
        [JsonPropertyName("resource")]
        public string Resource { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }
    }
}
