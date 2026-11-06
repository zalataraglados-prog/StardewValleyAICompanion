using System;
using System.Collections.Generic;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class PolicyStateFeatureProjector
{
    private FeatureVector EnrichMarriage(FeatureVector features, SnapshotEnvelope snapshot)
    {
        var current = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        if (current.Status != "ready_current_state_only")
            throw new InvalidOperationException("Marriage policy state is unreadable: " + string.Join(",", current.BlockingReasons));
        features.Numeric = new List<NumericFeature>(features.Numeric)
        {
            Number("marriage.house_levels_missing", current.HouseLevelsMissing!.Value),
            Number("marriage.house_days_remaining", current.DaysUntilFarmhouseUpgrade!.Value)
        }.ToArray();
        features.Boolean = new List<BooleanFeature>(features.Boolean)
        {
            Flag("marriage.engaged", current.Engaged!.Value),
            Flag("marriage.current_or_pending_roommate", current.HasCurrentOrPendingRoommate!.Value),
            Flag("marriage.current_joint_satisfied", current.CurrentCriterionSatisfied!.Value),
            Flag("marriage.native_settlement_pending", current.NativeSettlementPending!.Value)
        }.ToArray();
        return features;
    }
}
