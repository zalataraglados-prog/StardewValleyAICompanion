namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    private CropTeacherSeedLiveBoundary? verifiedBoundary;
    private Func<CropTeacherSeedLiveBoundary>? verifiedBoundaryFactory;
    private bool managedBoundaryRequested;

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
}
