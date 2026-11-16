using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
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

            var source = ReadOriginalContinuationMetadata();
            ValidateState();
            return source;
        }
        finally { gate.Release(); }
    }

    // Metadata only. The explicit live entry must separately bind its actual process and bridge read.
    internal CropTeacherManagedContinuationSource ReadLiveContinuationSource(SnapshotEnvelope live, DateTimeOffset processStartedAt)
    {
        gate.Wait();
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Same-live metadata requires the recovered original head without a pending operation.");
            var accepted = Read<SnapshotEnvelope>(headSnapshotPath);
            var initial = Read<SnapshotEnvelope>(options.InitialSnapshotPath);
            if (!DateTimeOffset.TryParse(initial.RealTimestamp, out var originalCapturedAt) || processStartedAt > originalCapturedAt ||
                live.SchemaVersion != "snapshot.v1" || live.StateHash != accepted.StateHash || live.GameTick != accepted.GameTick ||
                live.SaveId.Value != saveId || live.PlayerId.Value != playerId || live.GameVersion != accepted.GameVersion ||
                SnapshotHash.ComputeStateHash(live.State) != accepted.StateHash)
                throw new InvalidDataException("Same-live source differs from the original accepted world/actor/process lifetime.");
            var source = ReadOriginalContinuationMetadata();
            ValidateState();
            return source;
        }
        finally { gate.Release(); }
    }

    private CropTeacherManagedContinuationSource ReadOriginalContinuationMetadata()
    {
        // Never rebase the original source/day/schedule or expose the owner's mutable private plan.
        var (request, originalPlan) = CropTeacherResumeLoader.ReadVerifiedPlan(options.NativeRunPlanPath);
        return new(request, originalPlan, options.NativeRunPlanPath, CurrentSnapshotPath, CurrentLedgerPath);
    }
}
