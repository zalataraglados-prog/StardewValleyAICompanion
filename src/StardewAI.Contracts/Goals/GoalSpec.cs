using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Goals
{
    public sealed class GoalSpec
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "goal.v1";

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = string.Empty;

        [JsonPropertyName("raw_text")]
        public string RawText { get; set; } = string.Empty;

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "recovery.stabilize_day";

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "relaxed";

        [JsonPropertyName("extracted_parameters")]
        public GoalParameter[] ExtractedParameters { get; set; } = new GoalParameter[0];
    }

    public sealed class GoalParameter
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }
}
