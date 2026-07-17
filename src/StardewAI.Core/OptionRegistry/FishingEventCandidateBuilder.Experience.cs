using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.OptionRegistry
{
    internal static partial class FishingEventCandidateBuilder
    {
        private static IEnumerable<SmallModelActionParameter> FishingOutcomeExperienceParameters(
            int? effectiveFishDifficulty,
            bool isBossFish,
            int? maximumRawFishQuality,
            int waterDepth)
        {
            if (!effectiveFishDifficulty.HasValue || !maximumRawFishQuality.HasValue)
            {
                yield break;
            }

            yield return Parameter("effective_fish_difficulty", effectiveFishDifficulty.Value);
            yield return Parameter("is_boss_fish", isBossFish);
            yield return Parameter("maximum_raw_fish_quality", maximumRawFishQuality.Value);
            yield return Parameter("fishing_experience_on_success_min", CatchExperience(
                effectiveFishDifficulty.Value,
                rawQuality: 0,
                treasureCaught: false,
                perfectCatch: false,
                isBossFish: isBossFish));
            yield return Parameter("fishing_experience_on_success_max", CatchExperience(
                effectiveFishDifficulty.Value,
                maximumRawFishQuality.Value,
                treasureCaught: true,
                perfectCatch: true,
                isBossFish: isBossFish));
            yield return Parameter("conditional_luck_experience", 10 * (waterDepth + 1));
            yield return Parameter("conditional_luck_experience_condition", "native_fishing_treasure_chest_opened");
        }

        private static IEnumerable<SmallModelActionParameter> AggregatedFishingExperienceParameters(
            FishingOutcomeProjection[] outcomes,
            EventCandidate first)
        {
            if (outcomes.Length == 0 || outcomes.Any(outcome =>
                !outcome.effective_fish_difficulty.HasValue ||
                !outcome.maximum_raw_fish_quality.HasValue))
            {
                yield return Parameter("skill_experience_projection_status", "incomplete_outcome_fish_difficulty");
                yield break;
            }

            var minimum = outcomes.Min(outcome => CatchExperience(
                outcome.effective_fish_difficulty!.Value,
                rawQuality: 0,
                treasureCaught: false,
                perfectCatch: false,
                isBossFish: outcome.is_boss_fish));
            var maximum = outcomes.Max(outcome => CatchExperience(
                outcome.effective_fish_difficulty!.Value,
                outcome.maximum_raw_fish_quality!.Value,
                treasureCaught: true,
                perfectCatch: true,
                isBossFish: outcome.is_boss_fish));
            var waterDepth = CandidateInt(first, "water_depth") ?? 0;

            yield return Parameter("skill_experience_skill_id", "fishing");
            yield return Parameter("skill_experience_on_success_min", minimum);
            yield return Parameter("skill_experience_on_success_max", maximum);
            yield return Parameter("skill_experience_condition", "native_object_fish_catch_outside_festival_and_fish_pond");
            yield return Parameter("skill_experience_projection_status", "exact_bounded_from_current_outcome_distribution");
            yield return Parameter("conditional_luck_experience", 10 * (waterDepth + 1));
            yield return Parameter("conditional_luck_experience_condition", "native_fishing_treasure_chest_opened");
        }

        private static int CatchExperience(
            int effectiveFishDifficulty,
            int rawQuality,
            bool treasureCaught,
            bool perfectCatch,
            bool isBossFish)
        {
            var experience = Math.Max(1, (rawQuality + 1) * 3 + effectiveFishDifficulty / 3);
            if (treasureCaught)
            {
                experience += (int)(experience * 1.2f);
            }
            if (perfectCatch)
            {
                experience += (int)(experience * 1.4f);
            }
            if (isBossFish)
            {
                experience *= 5;
            }
            return experience;
        }
    }
}
