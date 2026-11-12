using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    internal static SnapshotEnvelope VerifyOwnedSleepBoundary(SnapshotEnvelope source, MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        Require(request.TransitionKind == DayRolloverKind, "Owned crop sleep requires its original day-rollover transition.");
        var transition = LoadTransition(request);
        var calendar = ReadCalendar(source);
        ValidateSnapshotIdentity(source, source, calendar);
        ValidateSnapshotIdentity(transition.Before, source, calendar);
        ValidateSnapshotIdentity(transition.After, source, calendar);
        Require(EqualJson(source, transition.Before), "Owned crop sleep does not continue its exact accepted source.");
        ValidateQueueReceipt(transition);
        // Selected crop growth and the actual save boundary are consumed separately by the owner.
        ValidateDayRollover(transition, Array.Empty<LifecycleCommitmentContext>());
        Require(ReadPlayerLocation(source) == ReadHomeLocation(source) && ReadPlayerLocation(transition.After) == ReadHomeLocation(source),
            "Owned crop sleep left its original native home.");
        return transition.After;
    }
}
