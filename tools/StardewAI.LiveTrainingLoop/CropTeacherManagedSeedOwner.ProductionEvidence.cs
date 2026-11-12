namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedProductionEvidence> BuildProductionEvidenceAsync(string evidenceDirectory)
    {
        var trace = await ReadProductionTraceAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired || !SamePath(trace.AcceptedSnapshotPath, headSnapshotPath) ||
                !SamePath(trace.AcceptedLedgerPath, headLedgerPath))
                throw new InvalidOperationException("Managed crop production source changed before evidence assembly.");
            var output = Path.GetFullPath(evidenceDirectory);
            if (!Within(options.EvidenceDirectory, output) || SamePath(options.EvidenceDirectory, output) || ReservedRuntimePath(output) ||
                trace.InputHashes.Keys.Any(path => Within(output, path)))
                throw new IOException("Managed crop production evidence requires a separate owned output directory.");
            RequireDirectoryPath(output);
            RequireOutsideNativeSaveSlot(new[] { output }, options.NativeSaveBinding);
            var result = await CropTeacherProductionEvidence.BuildAsync(trace, output, write).ConfigureAwait(false);
            ValidateState(); trace.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }
}
