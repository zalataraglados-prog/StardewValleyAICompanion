namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // Controller-pinned published certificate only; this read does not adopt a cursor or authorize native work.
    public async Task<CropTeacherManagedProductionEvidence> ReadPreparedTerminalProductionAsync(CropTeacherTerminalProductionReference reference)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired || terminalTarget is not null)
                throw new InvalidOperationException("Prepared production requires the original recovered pre-terminal owner head.");
            var path = Path.GetFullPath(reference.ManifestPath);
            if (!Path.IsPathFullyQualified(reference.ManifestPath) || !Within(options.EvidenceDirectory, path) ||
                SamePath(options.EvidenceDirectory, path) || ReservedRuntimePath(path))
                throw new IOException("Prepared production must be this owner's published certificate.");
            RequireDirectoryPath(Path.GetDirectoryName(path)!);
            RequireOutsideNativeSaveSlot(new[] { path }, options.NativeSaveBinding);
            var production = ReadObservedProductionEvidence(reference);
            var selection = CropTeacherTerminalShippingSelection.OpenJointNativeVariants(production,
                options.NativeRunPlanPath, headSnapshotPath, headLedgerPath, HasCurrentSaleStockForPlan());
            selection.ValidateInputsUnchanged();
            ValidateState();
            return production;
        }
        finally { gate.Release(); }
    }
}
