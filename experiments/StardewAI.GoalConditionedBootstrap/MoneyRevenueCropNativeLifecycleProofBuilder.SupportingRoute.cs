using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    // A leaf witness reuses the lifecycle kernels, not its whole-cycle success flag.
    internal static SnapshotEnvelope VerifySupportingRoute(SnapshotEnvelope source,
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string cropLocation)
    {
        Require(request.TransitionKind is CropIngressRouteKind or SleepIngressRouteKind,
            "Crop supporting-route witness requires an original ingress route kind.");
        var transition = LoadTransition(request);
        var calendar = ReadCalendar(source);
        ValidateSnapshotIdentity(source, source, calendar);
        ValidateSnapshotIdentity(transition.Before, source, calendar);
        ValidateSnapshotIdentity(transition.After, source, calendar);
        Require(EqualJson(source, transition.Before),
            "Crop supporting-route witness does not continue its exact accepted source.");
        ValidateQueueReceipt(transition);
        var target = request.TransitionKind == SleepIngressRouteKind ? ReadHomeLocation(source) : cropLocation;
        Require(!string.IsNullOrWhiteSpace(target), "Crop supporting-route witness lacks its original target location.");
        ValidateSupportingRoutes(request.TotalDay, new List<LoadedTransition> { transition }, target);
        return transition.After;
    }

    private static void ValidateQueueReceipt(LoadedTransition transition)
    {
        var reasons = QueueExecutionReceiptValidator.Validate(transition.Queue, transition.Before, transition.Receipt,
            transition.After, transition.Request.RunId, transition.Request.ExecutorVersion,
            transition.Request.SelectedCandidateId, transition.Before.StateHash, requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0, "Money Revenue crop lifecycle queue receipt was rejected: " + string.Join(",", reasons));
    }
}
