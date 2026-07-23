using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed partial class TrainingExecutionResult
    {
        [JsonPropertyName("quest_candidate_id")]
        public string QuestCandidateId { get; set; } = string.Empty;

        [JsonPropertyName("quest_family")]
        public string QuestFamily { get; set; } = string.Empty;

        [JsonPropertyName("quest_id")]
        public string QuestId { get; set; } = string.Empty;

        [JsonPropertyName("quest_key")]
        public string QuestKey { get; set; } = string.Empty;

        [JsonPropertyName("quest_objective_index")]
        public int? QuestObjectiveIndex { get; set; }

        [JsonPropertyName("quest_progress_before")]
        public int? QuestProgressBefore { get; set; }

        [JsonPropertyName("quest_progress_after")]
        public int? QuestProgressAfter { get; set; }

        [JsonPropertyName("quest_target_count")]
        public int? QuestTargetCount { get; set; }

        [JsonPropertyName("quest_present_before")]
        public bool? QuestPresentBefore { get; set; }

        [JsonPropertyName("quest_present_after")]
        public bool? QuestPresentAfter { get; set; }

        [JsonPropertyName("quest_completed_before")]
        public bool? QuestCompletedBefore { get; set; }

        [JsonPropertyName("quest_completed_after")]
        public bool? QuestCompletedAfter { get; set; }
    }
}
