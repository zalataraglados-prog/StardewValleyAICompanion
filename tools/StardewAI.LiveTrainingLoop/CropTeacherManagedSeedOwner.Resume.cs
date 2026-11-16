namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal bool AcceptedSavedContentReadbackVerified => lastAcceptedOperation?.IsSavedContentReadback == true;
    internal CropTeacherManagedSeedOperation? AcceptedSavedReadbackOperation => AcceptedSavedContentReadbackVerified ? lastAcceptedOperation : null;
    internal bool HasCompletedTerminalDeposits => terminalTarget is { RequiredQuantity: > 0 } target &&
        target.AcceptedDepositCount == target.RequiredQuantity;

    internal async Task<string> ReadAcceptedNativeSaveBoundaryPathAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the retained owner before observing another native reload.");
            return AcceptedNativeSaveBoundaryPath();
        }
        finally { gate.Release(); }
    }

    internal void ValidateResumeOutputDirectory(string path)
    {
        ValidateState();
        path = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(path);
        if (parent is null || !SamePath(options.EvidenceDirectory, parent) || ReservedRuntimePath(path) ||
            File.Exists(path) || Directory.Exists(path) || fixedHashes.Keys.Concat(frontierHashes.Keys).Any(input => Within(path, input)))
            throw new IOException("Retained owner resume requires a separate new output directory inside its original owner.");
        RequireDirectoryPath(path);
        RequireOutsideNativeSaveSlot(new[] { path }, options.NativeSaveBinding);
    }
}
