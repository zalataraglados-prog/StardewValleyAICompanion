using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class PlanningGoalResolution
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } =
            "planning_goal_resolution.v1";

        [JsonPropertyName("status")]
        public string Status { get; set; } = "not_applicable";

        [JsonPropertyName("requested_goal_id")]
        public string RequestedGoalId { get; set; } = string.Empty;

        [JsonPropertyName("effective_goal_id")]
        public string EffectiveGoalId { get; set; } = string.Empty;

        [JsonPropertyName("direction_id")]
        public string DirectionId { get; set; } = string.Empty;

        [JsonPropertyName("demand_family")]
        public string DemandFamily { get; set; } = string.Empty;

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("source_state_hash")]
        public string SourceStateHash { get; set; } = string.Empty;

        [JsonPropertyName("bound_candidate_ids")]
        public string[] BoundCandidateIds { get; set; } =
            Array.Empty<string>();

        [JsonPropertyName("binding_rule_id")]
        public string BindingRuleId { get; set; } = string.Empty;

        [JsonPropertyName("considered_direction_ids")]
        public string[] ConsideredDirectionIds { get; set; } =
            Array.Empty<string>();
    }
}
