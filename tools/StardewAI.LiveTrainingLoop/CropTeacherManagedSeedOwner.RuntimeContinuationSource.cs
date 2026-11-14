using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropTeacherManagedContinuationSource(
    MoneyRevenueCropNativeProductionRunPlanRequest RunPlanRequest,
    MoneyRevenueCropNativeProductionRunPlan RunPlan,
    string NativeRunPlanPath,
    string SnapshotPath,
    string LedgerPath);

public sealed partial class CropTeacherManagedSeedOwner
{
    // Read-only metadata from the original accepted owner, not live-process execution authority.
    internal CropTeacherManagedContinuationSource ReadContinuationSource()
    {
        gate.Wait();
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired || !AcceptedSavedContentReadbackVerified)
                throw new InvalidOperationException("Managed continuation source requires the recovered original owner with an accepted saved-content readback and no pending operation.");

            // Rebuild the immutable original plan/request instead of exposing this owner's mutable
            // private plan or rebasing its original source/day/schedule onto the accepted head.
            var (request, originalPlan) = CropTeacherResumeLoader.ReadVerifiedPlan(options.NativeRunPlanPath);
            var source = new CropTeacherManagedContinuationSource(request, originalPlan,
                options.NativeRunPlanPath, CurrentSnapshotPath, CurrentLedgerPath);
            ValidateState();
            return source;
        }
        finally { gate.Release(); }
    }
}
