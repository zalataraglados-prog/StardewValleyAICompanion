namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal async Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingAsync(CropTeacherManagedProductionEvidence evidence)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original crop phase before selecting terminal shipping.");
            var result = new CropTeacherTerminalShippingSelection(evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath);
            ValidateState(); result.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }
}
