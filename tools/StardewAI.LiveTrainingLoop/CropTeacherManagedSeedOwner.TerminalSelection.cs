namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingAsync(CropTeacherManagedProductionEvidence evidence, bool nativeVariants = false) =>
        SelectTerminalShippingCoreAsync(evidence, nativeVariants, consumeSaleStock: false);

    internal Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingConsumingSaleStockAsync(CropTeacherManagedProductionEvidence evidence) =>
        SelectTerminalShippingCoreAsync(evidence, nativeVariants: true, consumeSaleStock: true);

    private async Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingCoreAsync(CropTeacherManagedProductionEvidence evidence,
        bool nativeVariants, bool consumeSaleStock)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original crop phase before selecting terminal shipping.");
            var result = consumeSaleStock ? CropTeacherTerminalShippingSelection.OpenNativeVariantsConsumingSaleStock(
                evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath) :
                nativeVariants ? CropTeacherTerminalShippingSelection.OpenNativeVariants(evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath) :
                new CropTeacherTerminalShippingSelection(evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath);
            ValidateState(); result.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }
}
