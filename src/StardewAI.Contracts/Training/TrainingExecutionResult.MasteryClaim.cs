using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("mastery_skill_id")]
    public int? MasterySkillId { get; set; }

    [JsonPropertyName("mastery_levels_spent_after")]
    public int? MasteryLevelsSpentAfter { get; set; }

    [JsonPropertyName("mastery_skill_stat_after")]
    public int? MasterySkillStatAfter { get; set; }

    [JsonPropertyName("mastery_trinket_slots_after")]
    public int? MasteryTrinketSlotsAfter { get; set; }

    [JsonPropertyName("mastery_all_plaques_completed_after")]
    public bool? MasteryAllPlaquesCompletedAfter { get; set; }

    [JsonPropertyName("mastery_direct_reward_total_delta")]
    public int? MasteryDirectRewardTotalDelta { get; set; }
}
