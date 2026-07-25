using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed partial class TrainingExecutionRequest
    {
        [JsonPropertyName("quest_candidate_id")]
        public string QuestCandidateId { get; set; } = string.Empty;

        [JsonPropertyName("quest_family")]
        public string QuestFamily { get; set; } = string.Empty;

        [JsonPropertyName("quest_id")]
        public string QuestId { get; set; } = string.Empty;

        [JsonPropertyName("quest_key")]
        public string QuestKey { get; set; } = string.Empty;

        [JsonPropertyName("quest_runtime_type")]
        public string QuestRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("quest_interaction_kind")]
        public string QuestInteractionKind { get; set; } = string.Empty;

        [JsonPropertyName("quest_objective_index")]
        public int? QuestObjectiveIndex { get; set; }

        [JsonPropertyName("quest_expected_current_count")]
        public int? QuestExpectedCurrentCount { get; set; }

        [JsonPropertyName("quest_expected_target_count")]
        public int? QuestExpectedTargetCount { get; set; }
    }
}
