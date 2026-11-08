namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    private CropTeacherSeedLiveBoundary? verifiedBoundary;
    private CropTeacherSeedNoDispatchBoundary? verifiedStopBoundary;
    private Func<CropTeacherSeedLiveBoundary>? verifiedBoundaryFactory;
    private bool managedBoundaryRequested;
    private Action? executionGuard;

    internal void BindExecutionGuard(Action guard)
    {
        if (executionGuard is not null) throw new InvalidOperationException("Original seed runtime already has an execution owner.");
        executionGuard = guard;
    }

    internal void RequireExecutionLease() => executionGuard?.Invoke();

    internal CropTeacherSeedPreparedBoundary ReadPreparedBoundary()
    {
        managedBoundaryRequested = true;
        void Validate()
        {
            scope.ValidateInputsUnchanged();
            if (PortfolioMutationRequestJournal.HashFile(Path.Combine(options.EvidenceDirectory, "support-intent.json")) != intentSha256)
                throw new InvalidDataException("Original crop seed runtime intent changed after preparation.");
            ValidateDispatchQueueCore(queuePath);
        }
        Validate();
        return new(options.EvidenceDirectory, options.BackendUrl, options.RunId, options.ExecutorVersion,
            scope.ReadPreparationRequest().PortfolioId, scope.CommitmentId, scope.InputHashes,
            scope.ReadPreparationRequest(), scope.PreparationDetails?.DeepClone().AsObject(), intentSha256, queuePath, queueSha256, Validate);
    }

    internal CropTeacherSeedLiveBoundary ReadVerifiedBoundary()
    {
        var result = verifiedBoundary ??= verifiedBoundaryFactory?.Invoke()
            ?? throw new InvalidOperationException("Original native crop completion is not verified.");
        result.Validate();
        return result;
    }

    internal CropTeacherSeedNoDispatchBoundary ReadVerifiedStopBoundary()
    {
        var result = verifiedStopBoundary ?? throw new InvalidOperationException("Original zero-dispatch controller stop is not verified.");
        result.Validate();
        return result;
    }
}
