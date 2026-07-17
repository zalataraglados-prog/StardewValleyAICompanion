using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object? ReadSkillsDetail(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        var skills = new[]
        {
            ReadSkill(player, 0, "farming"),
            ReadSkill(player, 1, "fishing"),
            ReadSkill(player, 2, "foraging"),
            ReadSkill(player, 3, "mining"),
            ReadSkill(player, 4, "combat"),
            ReadSkill(player, 5, "luck")
        };

        return new
        {
            scoring_level = player.Level,
            scoring_formula = "floor((farming+fishing+foraging+combat+mining+luck)/2)",
            vanilla_level_cap = 10,
            skills
        };
    }

    private static object ReadSkill(Farmer player, int index, string skillId)
    {
        var unmodifiedLevel = player.GetUnmodifiedSkillLevel(index);
        var effectiveLevel = player.GetSkillLevel(index);
        var experience = player.experiencePoints[index];
        var nextLevel = unmodifiedLevel < 10 ? unmodifiedLevel + 1 : (int?)null;
        var nextLevelExperience = nextLevel.HasValue
            ? Farmer.getBaseExperienceForLevel(nextLevel.Value)
            : (int?)null;

        return new
        {
            skill_id = skillId,
            skill_index = index,
            unmodified_level = unmodifiedLevel,
            effective_level = effectiveLevel,
            temporary_buff_delta = effectiveLevel - unmodifiedLevel,
            experience,
            next_level = nextLevel,
            next_level_experience = nextLevelExperience,
            experience_to_next_level = nextLevelExperience.HasValue
                ? Math.Max(0, nextLevelExperience.Value - experience)
                : (int?)null,
            at_vanilla_level_cap = unmodifiedLevel >= 10,
            has_native_experience_sources = true,
            experience_candidate_mapping_status = "pending_source_specific_binding"
        };
    }

    private static object? ReadLuckContext(Farmer? player)
    {
        if (player is null)
        {
            return null;
        }

        var specialCharmModifier = player.hasSpecialCharm ? 0.025d : 0d;
        var activeLuckBuffs = player.buffs.AppliedBuffs.Values
            .Where(buff => buff.effects.LuckLevel.Value != 0f)
            .OrderBy(buff => buff.id, StringComparer.Ordinal)
            .Select(buff => new
            {
                buff_id = buff.id,
                source = buff.source,
                luck_level_delta = buff.effects.LuckLevel.Value,
                milliseconds_remaining = buff.millisecondsDuration,
                total_milliseconds = buff.totalMillisecondsDuration,
                lasts_all_day = buff.millisecondsDuration == Buff.ENDLESS
            })
            .ToArray();

        return new
        {
            shared_daily_luck_base = player.team.sharedDailyLuck.Value,
            has_special_charm = player.hasSpecialCharm,
            special_charm_daily_luck_modifier = specialCharmModifier,
            effective_daily_luck = player.DailyLuck,
            daily_luck_clamp_min = -0.2d,
            daily_luck_clamp_max = 0.2d,
            unmodified_luck_skill_level = player.GetUnmodifiedSkillLevel(Farmer.luckSkill),
            active_luck_level_buff = player.buffs.LuckLevel,
            effective_luck_skill_level = player.LuckLevel,
            active_luck_buffs = activeLuckBuffs,
            fortune_teller_is_observation_only = true,
            rule_composition = "gameplay_rules_weight_effective_daily_luck_and_effective_luck_skill_level_separately"
        };
    }
}
