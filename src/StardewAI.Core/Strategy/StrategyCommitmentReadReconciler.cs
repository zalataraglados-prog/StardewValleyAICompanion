using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class StrategyCommitmentReadReconciler
{
    public static StrategyCommitmentLedger Reconcile(StrategyCommitmentLedger current,
        SnapshotEnvelope snapshot, string updatedAt)
    {
        var reconciled = new CropCommitmentLedgerService().ReconcileCompleted(current, snapshot, updatedAt);
        reconciled = new MachineRelocationIntentLedgerService().ReconcileCompleted(reconciled, snapshot, updatedAt);
        return new MachineSupportIntentLedgerService().ReconcileCompleted(reconciled, snapshot, updatedAt);
    }
}
