using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    public static MoneyRevenueCropSeedAcquisitionTransitionProof BuildSupportSlice(
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string qualifiedItemId,
        string shopId, int maximumUnitPrice, int deadlineTotalDay) => VerifySupportSlice(
            request, qualifiedItemId, shopId, maximumUnitPrice, deadlineTotalDay).Proof;

    internal static (MoneyRevenueCropSeedAcquisitionTransitionProof Proof, SnapshotEnvelope Before,
        SnapshotEnvelope After) VerifySupportSlice(
        MoneyRevenueCropNativeLifecycleTransitionRequest request, string qualifiedItemId,
        string shopId, int maximumUnitPrice, int deadlineTotalDay)
    {
        Require(!string.IsNullOrWhiteSpace(qualifiedItemId) && !string.IsNullOrWhiteSpace(shopId) &&
                maximumUnitPrice > 0 && deadlineTotalDay >= 0,
            "Money Revenue crop support-slice scope is incomplete.");
        var transition = LoadTransition(request);
        ValidateSnapshot(transition.Before, "support-slice before");
        ValidateSnapshot(transition.After, "support-slice after");
        Require(SameSnapshotIdentity(transition.Before, transition.After),
            "Money Revenue crop support-slice actor identity changed.");
        var day = ReadInt(transition.Before, "time", "total_days");
        Require(request.TotalDay == day && ReadInt(transition.After, "time", "total_days") == day &&
                day <= deadlineTotalDay,
            "Money Revenue crop support-slice crossed its same-day deadline boundary.");
        var reasons = QueueExecutionReceiptValidator.ValidateContinuationSlice(
            transition.Queue, transition.Before, transition.Receipt, transition.After,
            transition.ReceiptPath, request.RunId, request.ExecutorVersion,
            request.SelectedCandidateId, transition.Before.StateHash, out var effectiveItems,
            allowAppliedVerifiedProgress: true);
        Require(reasons.Length == 0,
            "Money Revenue crop support-slice queue receipt rejected: " + string.Join(",", reasons));
        var binding = ReadPurchaseBinding(transition.Queue, qualifiedItemId);
        var effectiveBinding = ReadPurchaseBinding(new ActionQueueEnvelope { Items = effectiveItems }, qualifiedItemId);
        Require(binding == effectiveBinding && binding.ShopId == shopId && binding.MaxUnitPrice <= maximumUnitPrice,
            "Money Revenue crop support-slice continuation exceeds its bound shop/seed/price scope.");
        var beforeQuantity = ReadSeedQuantity(transition.Before, qualifiedItemId);
        var afterQuantity = ReadSeedQuantity(transition.After, qualifiedItemId);
        var beforeMoney = ReadInt(transition.Before, "player", "money");
        var afterMoney = ReadInt(transition.After, "player", "money");
        var options = effectiveItems.Select(item => item.OptionId).ToArray();
        var purchase = options.Contains("executor.buy_shop_item", StringComparer.Ordinal);
        int? price = null;
        if (purchase)
            price = VerifyPurchaseSlice(transition, binding, effectiveItems,
                beforeQuantity, afterQuantity, beforeMoney, afterMoney, 0);
        else
            VerifySupportingStage(transition, effectiveItems,
                beforeQuantity, afterQuantity, beforeMoney, afterMoney, 0);
        var proof = TransitionProof(transition, binding, options, 0,
            purchase ? "purchase_slice" : "supporting_route_or_shop", price,
            beforeQuantity, afterQuantity, beforeMoney, afterMoney);
        return (proof, transition.Before, transition.After);
    }

    private static MoneyRevenueCropSeedAcquisitionTransitionProof TransitionProof(
        LoadedTransition transition, PurchaseBinding binding, string[] options, int index, string stage,
        int? price, int beforeQuantity, int afterQuantity, int beforeMoney, int afterMoney) => new(
            index, stage, transition.QueuePath, Hash(transition.QueuePath),
            transition.BeforePath, Hash(transition.BeforePath), transition.ReceiptPath, Hash(transition.ReceiptPath),
            transition.AfterPath, Hash(transition.AfterPath), transition.Queue.QueueId,
            transition.Before.StateHash, transition.After.StateHash,
            transition.Before.GameTick, transition.After.GameTick, options,
            binding.ShopId, binding.StockId, binding.OutputStack, binding.OutputQuality, binding.MaxUnitPrice,
            price, beforeQuantity, afterQuantity, afterQuantity - beforeQuantity,
            beforeMoney, afterMoney, beforeMoney - afterMoney, true);
}
