using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropTeacherManagedContinuationSource(
    MoneyRevenueCropNativeProductionRunPlanRequest RunPlanRequest,
    MoneyRevenueCropNativeProductionRunPlan RunPlan,
    string NativeRunPlanPath,
    string SnapshotPath,
    string LedgerPath)
{
    public MoneyRevenueCropAcceptedSavedReadbackReference? ProcessReadback { get; init; }
    public string AcceptedStateHash { get; init; } = string.Empty;
    public string LiveWorldMatchKind { get; init; } = "exact_state";
    public int? DeveloperSavedSourceTotalDay { get; init; }
}

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
        => ReadLiveContinuationSourceCore(live, processStartedAt, null, null);

    internal CropTeacherManagedContinuationSource ReadLiveProcessContinuationSource(SnapshotEnvelope live,
        int processId, string executablePath, DateTimeOffset processStartedAt)
    {
        if (processId <= 0 || !Path.IsPathFullyQualified(executablePath))
            throw new InvalidDataException("Same-live continuation requires an exact positive process ID and absolute executable.");
        return ReadLiveContinuationSourceCore(live, processStartedAt, processId, executablePath);
    }

    private CropTeacherManagedContinuationSource ReadLiveContinuationSourceCore(SnapshotEnvelope live,
        DateTimeOffset processStartedAt, int? processId, string? executablePath)
    {
        gate.Wait();
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Same-live metadata requires the recovered original head without a pending operation.");
            var accepted = Read<SnapshotEnvelope>(headSnapshotPath);
            var initial = Read<SnapshotEnvelope>(developerSavedSource?.SnapshotPath ?? options.InitialSnapshotPath);
            var exactState = live.StateHash == accepted.StateHash;
            var matchKind = "exact_state";
            if (!DateTimeOffset.TryParse(initial.RealTimestamp, out var originalCapturedAt) ||
                live.SchemaVersion != "snapshot.v1" || live.GameTick != accepted.GameTick ||
                live.SaveId.Value != saveId || live.PlayerId.Value != playerId || live.GameVersion != accepted.GameVersion ||
                SnapshotHash.ComputeStateHash(live.State) != live.StateHash ||
                !exactState && (processId is null || !SameLiveObservationBoundary(accepted, live, out matchKind)))
                throw new InvalidDataException("Same-live source differs from the original accepted world/actor/process lifetime.");
            var readback = processId is { } id && id > 0 && executablePath is { } path && Path.IsPathFullyQualified(path)
                ? ReadAcceptedLiveProcessReadback(id, path, processStartedAt) : null;
            if (readback is null && developerSavedSource is { } saved)
            {
                if (processId != saved.Process.ProcessId || executablePath is null || !SamePath(executablePath, saved.Process.ExecutablePath) ||
                    processStartedAt != DateTimeOffset.Parse(saved.Process.StartedAtUtc))
                    throw new InvalidDataException("Developer saved source belongs to a different native process.");
            }
            if (readback is null && processStartedAt > originalCapturedAt)
                throw new InvalidDataException("Same-live process has no original initial lifetime or accepted native reload.");
            var source = ReadOriginalContinuationMetadata() with
            {
                ProcessReadback = readback, AcceptedStateHash = accepted.StateHash,
                LiveWorldMatchKind = matchKind
            };
            ValidateState();
            return source;
        }
        finally { gate.Release(); }
    }

    private CropTeacherManagedContinuationSource ReadOriginalContinuationMetadata()
    {
        // Never rebase the original source/day/schedule or expose the owner's mutable private plan.
        var (request, originalPlan) = CropTeacherResumeLoader.ReadVerifiedPlan(options.NativeRunPlanPath);
        return new(request, originalPlan, options.NativeRunPlanPath, CurrentSnapshotPath, CurrentLedgerPath)
        {
            DeveloperSavedSourceTotalDay = developerSavedSource is null ? null :
                SnapshotInt(Read<SnapshotEnvelope>(developerSavedSource.SnapshotPath), "time", "total_days")
        };
    }
}
