using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    internal Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingAsync(CropTeacherManagedProductionEvidence evidence, bool nativeVariants = false) =>
        SelectTerminalShippingCoreAsync(evidence, nativeVariants, consumeSaleStock: false);

    internal Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingConsumingSaleStockAsync(CropTeacherManagedProductionEvidence evidence) =>
        SelectTerminalShippingCoreAsync(evidence, nativeVariants: true, consumeSaleStock: true);

    internal Task<CropTeacherTerminalShippingSelection> SelectJointTerminalShippingAsync(CropTeacherManagedProductionEvidence evidence) =>
        SelectTerminalShippingCoreAsync(evidence, nativeVariants: true, consumeSaleStock: false, useAcceptedSaleStock: true);

    private async Task<CropTeacherTerminalShippingSelection> SelectTerminalShippingCoreAsync(CropTeacherManagedProductionEvidence evidence,
        bool nativeVariants, bool consumeSaleStock, bool useAcceptedSaleStock = false)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original crop phase before selecting terminal shipping.");
            if (useAcceptedSaleStock)
                consumeSaleStock = HasCurrentSaleStockForPlan();
            var result = consumeSaleStock ? CropTeacherTerminalShippingSelection.OpenNativeVariantsConsumingSaleStock(
                evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath) :
                nativeVariants ? CropTeacherTerminalShippingSelection.OpenNativeVariants(evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath) :
                new CropTeacherTerminalShippingSelection(evidence, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath);
            ValidateState(); result.ValidateInputsUnchanged();
            return result;
        }
        finally { gate.Release(); }
    }

    private bool HasCurrentSaleStockForPlan() => Read<StrategyCommitmentLedger>(headLedgerPath).MaterialReservations.Any(row =>
        row.Status == StrategyCommitmentStatuses.Active && CropSaleStockReservationContract.TryRead(row, out var purpose) &&
        plan.CoveredCommitmentIds.Contains(purpose!.CommitmentId, StringComparer.Ordinal));
}
