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
        if (worldModel.UserGoal is not ("goal.skills.raise_skill_levels" or FriendshipPolicyInputProjector.GoalId or MarriagePolicyInputProjector.GoalId or MuseumPolicyInputProjector.GoalId)) return features;
        if (snapshot is null || worldModel.StateHash != snapshot.StateHash)
            throw new ArgumentException(worldModel.UserGoal == "goal.skills.raise_skill_levels"
                ? "Skills policy features require their exact original snapshot."
                : worldModel.UserGoal == MuseumPolicyInputProjector.GoalId ? "Museum policy features require their exact original snapshot."
                : worldModel.UserGoal == MarriagePolicyInputProjector.GoalId ? "Marriage policy features require their exact original snapshot."
                : "Friendship policy features require their exact original snapshot.", nameof(snapshot));
        if (worldModel.UserGoal == "goal.skills.raise_skill_levels") return EnrichSkills(features, snapshot);
        if (worldModel.UserGoal == MuseumPolicyInputProjector.GoalId) return new MuseumPolicyInputProjector(snapshot).ProjectState(features);
        if (worldModel.UserGoal == MarriagePolicyInputProjector.GoalId) return EnrichMarriage(features, snapshot);
        var deficit = new CurrentFriendshipDeficitBuilder().Build(snapshot);
        if (deficit.Status != "ready_current_state_only")
            throw new InvalidOperationException("Friendship policy state is unreadable: " + string.Join(",", deficit.BlockingReasons));
        var numeric = new List<NumericFeature>(features.Numeric)
        {
            Number("npcs.friendships.eligible_villagers", deficit.EligibleVillagerCount!.Value),
            Number("npcs.friendships.qualifying_count", deficit.QualifyingVillagerCount!.Value),
            Number("npcs.friendships.missing_for_5", deficit.VillagersMissingForFive!.Value),
            Number("npcs.friendships.missing_for_10", deficit.VillagersMissingForTen!.Value)
        };
        var flags = new List<BooleanFeature>(features.Boolean)
        {
            Flag("npcs.friendships.five_met", deficit.VillagersMissingForFive == 0),
            Flag("npcs.friendships.ten_met", deficit.VillagersMissingForTen == 0)
        };
        features.Numeric = numeric.ToArray();
        features.Boolean = flags.ToArray();
        return features;
    }
}
