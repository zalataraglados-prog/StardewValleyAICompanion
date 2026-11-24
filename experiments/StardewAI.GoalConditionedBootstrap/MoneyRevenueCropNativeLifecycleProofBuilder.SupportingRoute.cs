using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    // A leaf witness reuses the lifecycle kernels, not its whole-cycle success flag.
    internal static SnapshotEnvelope VerifySupportingRoute(SnapshotEnvelope source,
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string cropLocation, string? verifiedHopTarget = null)
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
        var target = verifiedHopTarget ?? SupportingRouteTarget(source, request.TransitionKind, cropLocation);
        Require(!string.IsNullOrWhiteSpace(target), "Crop supporting-route witness lacks its original target location.");
        ValidateSupportingRoutes(request.TotalDay, new List<LoadedTransition> { transition }, target);
        return transition.After;
    }

    internal static string SupportingRouteTarget(SnapshotEnvelope source, string kind, string cropLocation)
    {
        Require(kind is CropIngressRouteKind or SleepIngressRouteKind, "Crop supporting route kind is invalid.");
        return kind == SleepIngressRouteKind ? ReadHomeLocation(source) : cropLocation;
    }

    private static void ValidateQueueReceipt(LoadedTransition transition)
    {
        if (transition.NativeHarvest is { } harvest)
        {
            Require(transition.Request.NativeHarvestSettlement is not null && harvest.PickupSettlementPath is not null &&
                    harvest.AppliedTileCount == transition.NativeAppliedItemCount && SamePath(harvest.AfterSnapshotPath, transition.AfterPath),
                "Native crop lifecycle lost its original harvest and pickup receipt binding.");
            return;
        }
        if (transition.NativeWateringMaintenance is { } watering)
        {
            Require(transition.Request.NativeWateringPreparation is not null && watering.SelectedWateringResourcesVerified &&
                watering.AppliedTileCount == transition.NativeAppliedItemCount,
                "Native crop lifecycle lost its verified original watering receipt.");
            return;
        }
        if (transition.NativeWorkReceiptVerified)
        {
            // The explicit shared-source kernel already verified the original queue and progress policy.
            Require(transition.NativeSharedSource && transition.SharedInputGuard is not null &&
                    transition.Request.NativeSharedSource?.AppliedNativeWorkQueue == true,
                "Native crop lifecycle lost its verified shared queue binding.");
            return;
        }
        if (transition.NativeAppliedItemCount is { } applied)
        {
            Require(CropTeacherSeedPlantingReceipt.Validate(transition.Queue, transition.Before, transition.Receipt,
                    transition.After, transition.Request) == applied,
                "Native crop lifecycle planting prefix differs from its original receipt.");
            return;
        }
        var reasons = QueueExecutionReceiptValidator.Validate(transition.Queue, transition.Before, transition.Receipt,
            transition.After, transition.Request.RunId, transition.Request.ExecutorVersion,
            transition.Request.SelectedCandidateId, transition.Before.StateHash, requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0, "Money Revenue crop lifecycle queue receipt was rejected: " + string.Join(",", reasons));
    }
}
