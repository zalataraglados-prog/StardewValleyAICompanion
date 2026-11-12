using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<CropTeacherManagedTerminalEvidence> BuildTerminalSettlementEvidenceAsync(
        CropTeacherManagedProductionEvidence production, CropTeacherTerminalShippingTarget target, string evidenceDirectory)
    {
        var trace = await ReadProductionTraceAsync().ConfigureAwait(false);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired || terminalTarget != target ||
                target.AcceptedDepositCount != target.RequiredQuantity ||
                !SamePath(trace.AcceptedSnapshotPath, headSnapshotPath) || !SamePath(trace.AcceptedLedgerPath, headLedgerPath))
                throw new InvalidOperationException("Recover the same owner's completed deposits/native sleep before terminal settlement proof.");
            var output = Path.GetFullPath(evidenceDirectory);
            if (!Within(options.EvidenceDirectory, output) || SamePath(options.EvidenceDirectory, output) || ReservedRuntimePath(output) ||
                trace.InputHashes.Keys.Append(production.ManifestPath).Any(path => Within(output, path)))
                throw new IOException("Managed terminal proof requires a separate owned output directory.");
            RequireDirectoryPath(output);
            RequireOutsideNativeSaveSlot(new[] { output }, options.NativeSaveBinding);
            var result = await CropTeacherTerminalSettlementEvidence.BuildAsync(trace, production, target, output, write).ConfigureAwait(false);
            ValidateState(); trace.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }
}
