using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("mastery_skill_id")]
    public int? MasterySkillId { get; set; }

    [JsonPropertyName("mastery_skill_key")]
    public string MasterySkillKey { get; set; } = string.Empty;

    [JsonPropertyName("mastery_projection_fingerprint")]
    public string MasteryProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("mastery_option_fingerprint")]
    public string MasteryOptionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("mastery_experience_before")]
    public int? MasteryExperienceBefore { get; set; }

    [JsonPropertyName("mastery_level_before")]
    public int? MasteryLevelBefore { get; set; }

    [JsonPropertyName("mastery_levels_spent_before")]
    public int? MasteryLevelsSpentBefore { get; set; }

    [JsonPropertyName("mastery_skill_stat_before")]
    public int? MasterySkillStatBefore { get; set; }

    [JsonPropertyName("mastery_all_skill_stats_before_csv")]
    public string MasteryAllSkillStatsBeforeCsv { get; set; } = string.Empty;

    [JsonPropertyName("mastery_recipe_rewards_json")]
    public string MasteryRecipeRewardsJson { get; set; } = string.Empty;

    [JsonPropertyName("mastery_direct_rewards_json")]
    public string MasteryDirectRewardsJson { get; set; } = string.Empty;

    [JsonPropertyName("mastery_grants_trinket_slot")]
    public bool? MasteryGrantsTrinketSlot { get; set; }

    [JsonPropertyName("mastery_trinket_slots_before")]
    public int? MasteryTrinketSlotsBefore { get; set; }

    [JsonPropertyName("mastery_action_raw")]
    public string MasteryActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("mastery_fixture_case")]
    public string MasteryFixtureCase { get; set; } = string.Empty;
}
