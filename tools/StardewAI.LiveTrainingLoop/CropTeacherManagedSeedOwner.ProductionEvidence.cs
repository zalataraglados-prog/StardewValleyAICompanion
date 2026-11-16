namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public Task<CropTeacherManagedProductionEvidence> BuildProductionEvidenceAsync(string evidenceDirectory) =>
        BuildProductionEvidenceCoreAsync(evidenceDirectory, joint: false);

    public Task<CropTeacherManagedProductionEvidence> BuildJointProductionEvidenceAsync(string evidenceDirectory) =>
        BuildProductionEvidenceCoreAsync(evidenceDirectory, joint: true);

    public Task<CropTeacherManagedProductionEvidence> BuildJointSaleStockProductionEvidenceAsync(string evidenceDirectory) =>
        BuildProductionEvidenceCoreAsync(evidenceDirectory, joint: true, retainSaleStock: true);

    private async Task<CropTeacherManagedProductionEvidence> BuildProductionEvidenceCoreAsync(string evidenceDirectory, bool joint, bool retainSaleStock = false)
    {
        var combined = joint ? await ReadJointProductionTraceAsync().ConfigureAwait(false) : null;
        var trace = combined?.CropTrace ?? await ReadProductionTraceAsync().ConfigureAwait(false);
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
            var result = combined is null ? await CropTeacherProductionEvidence.BuildAsync(trace, output, write).ConfigureAwait(false) :
                await CropTeacherProductionEvidence.BuildAcceptedJointAsync(combined, output, write,
                    retainSaleStock || HasCurrentSaleStockForPlan()).ConfigureAwait(false);
            ValidateState(); trace.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }
}
