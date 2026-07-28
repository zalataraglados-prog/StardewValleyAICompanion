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

        [JsonPropertyName("quest_slay_target_step")]
        public bool QuestSlayTargetStep { get; set; }

        [JsonPropertyName("quest_drop_box_id")]
        public string QuestDropBoxId { get; set; } = string.Empty;

        [JsonPropertyName("quest_drop_box_slot_index")]
        public int? QuestDropBoxSlotIndex { get; set; }

        [JsonPropertyName("quest_drop_box_qualified_item_id")]
        public string QuestDropBoxQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("quest_drop_box_expected_stack_before")]
        public int? QuestDropBoxExpectedStackBefore { get; set; }

        [JsonPropertyName("quest_drop_box_expected_accepted_count")]
        public int? QuestDropBoxExpectedAcceptedCount { get; set; }
    }
}
