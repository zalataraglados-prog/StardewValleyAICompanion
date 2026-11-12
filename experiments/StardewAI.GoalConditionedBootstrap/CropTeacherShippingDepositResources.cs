using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherShippingDepositResourceEffects(
    int SlotIndex, int Quality, int UnitPrice, int BinTileX, int BinTileY,
    long PendingValueBefore, long PendingValueAfter)
{
    public bool SpecificPersistentResourceBoundaryVerified => true;
    public bool NativeOutcomeVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool FormalProductTrainingAuthorized => false;
}

// A resource witness for one original native deposit, not a settlement or an owner outcome.
public static class CropTeacherShippingDepositResources
{
    public static CropTeacherShippingDepositResourceEffects Verify(
        SnapshotEnvelope before, SnapshotEnvelope after, ActionQueueEnvelope queue,
        QueueExecutionReceiptEnvelope receipt, MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string qualifiedItemId)
    {
        var reasons = QueueExecutionReceiptValidator.Validate(queue, before, receipt, after,
            transition.RunId, transition.ExecutorVersion, transition.SelectedCandidateId,
            before.StateHash, requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0, "Shipping resource receipt rejected: " + string.Join(",", reasons));
        Require(queue.Items is [var item] && item.OptionId == "executor.ship_inventory_item_to_bin",
            "Shipping resources require exactly one native deposit item.");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "shipping resource before");
        Require(MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "total_days") == transition.TotalDay,
            "Shipping resource source day differs from the original transition.");
        CropTeacherNativeStepBoundary.Verify(before, after, before, receipt.StepResults.Single(),
            transition.TotalDay, "Shipping resources");
        var deposit = NativeShippingDepositEvidenceVerifier.Verify(queue.Items, receipt, before, after,
            qualifiedItemId, expectedQuantity: 1);
        Require(deposit.Verified && deposit.SlotIndex.HasValue && deposit.Quality.HasValue &&
                deposit.UnitPrice.HasValue && deposit.BinTileX.HasValue && deposit.BinTileY.HasValue &&
                deposit.BeforePendingSettlementValue.HasValue && deposit.AfterPendingSettlementValue.HasValue,
            "Shipping native deposit rejected: " + string.Join(",", deposit.BlockingReasons));
        CropSeedResourceBoundary.VerifyShippingStock(before, after, deposit);
        CropSeedResourceBoundary.VerifyShippingBinsAndPersistentState(before, after, deposit);
        return new(deposit.SlotIndex!.Value, deposit.Quality!.Value, deposit.UnitPrice!.Value,
            deposit.BinTileX!.Value, deposit.BinTileY!.Value,
            deposit.BeforePendingSettlementValue!.Value, deposit.AfterPendingSettlementValue!.Value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
