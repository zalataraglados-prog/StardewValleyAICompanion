using System;
using System.Collections.Generic;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training;

public sealed partial class PolicyStateFeatureProjector
{
    public FeatureVector Project(WorldModelEnvelope worldModel, SnapshotEnvelope snapshot)
    {
        var features = Project(worldModel);
        if (worldModel.UserGoal != "goal.skills.raise_skill_levels") return features;
        if (snapshot is null || worldModel.StateHash != snapshot.StateHash)
            throw new ArgumentException("Skills policy features require their exact original snapshot.", nameof(snapshot));
        var deficit = new CurrentSkillExperienceDeficitBuilder().Build(snapshot);
        if (deficit.Status != "ready_current_state_only")
            throw new InvalidOperationException("Skills policy state is unreadable: " + string.Join(",", deficit.BlockingReasons));
        var numeric = new List<NumericFeature>(features.Numeric)
        {
            Number("player.skills.base_levels_missing_15", deficit.BaseLevelsMissingForFirstCriterion!.Value),
            Number("player.skills.base_levels_missing_25", deficit.BaseLevelsMissingForSecondCriterion!.Value),
            Number("player.skills.experience_to_five_level_ten", deficit.ExperienceMissingForFiveLevelTenSkills!.Value)
        };
        var flags = new List<BooleanFeature>(features.Boolean);
        foreach (var skill in deficit.Skills)
        {
            var prefix = "player.skills." + skill.SkillId + ".";
            numeric.Add(Number(prefix + "unmodified_level", skill.UnmodifiedLevel));
            numeric.Add(Number(prefix + "effective_level", skill.EffectiveLevel));
            numeric.Add(Number(prefix + "experience", skill.Experience));
            numeric.Add(Number(prefix + "experience_to_level_ten", skill.ExperienceToLevelTen));
            if (skill.ExperienceToNextLevel.HasValue)
                numeric.Add(Number(prefix + "experience_to_next_level", skill.ExperienceToNextLevel.Value));
            flags.Add(Flag(prefix + "at_vanilla_level_cap", skill.UnmodifiedLevel == 10));
        }
        features.Numeric = numeric.ToArray();
        features.Boolean = flags.ToArray();
        return features;
    }
}
