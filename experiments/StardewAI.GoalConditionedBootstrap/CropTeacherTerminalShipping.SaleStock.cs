using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherTerminalShipping
{
    private static MaterialReservation[] ReadSaleStockReservations(StrategyCommitmentLedger ledger, MoneyRevenueCropOutputRetentionProof proof) =>
        CropTeacherSaleStockInventory.ReadActive(ledger, proof.CommitmentId, proof.OutputRetentionKey, proof.HarvestItemQualifiedId);

    public static MaterialReservation[] ReadVerifiedSaleStock(SnapshotEnvelope source, StrategyCommitmentLedger ledger,
        MoneyRevenueCropOutputRetentionProof proof) => CropTeacherSaleStockInventory.ReadVerified(source, ledger,
            proof.CommitmentId, proof.OutputRetentionKey, proof.HarvestItemQualifiedId);

    private static MaterialReservation SelectSaleStockReservation(SnapshotEnvelope source, StrategyCommitmentLedger ledger,
        MoneyRevenueCropOutputRetentionProof proof, CropTeacherTerminalShippingTarget target)
    {
        var claims = ReadVerifiedSaleStock(source, ledger, proof);
        var count = target.AcceptedDepositCount;
        var ordered = target.NativeVariants?.DepositOrder is not null;
        var nextSlot = ordered ? target.VariantAt(count).SlotIndex : -1;
        MaterialReservation? selected = null;
        foreach (var variant in target.ReadNativeVariants())
        {
            var consumed = ordered ? target.CountOrderedDeposits(variant.SlotIndex, count) : Math.Min(count, variant.PlannedQuantity);
            if (!ordered) count -= consumed;
            var required = variant.PlannedQuantity - consumed;
            if (required == 0) continue;
            var matching = claims.Where(claim => claim.SlotIndex == variant.SlotIndex)
                .OrderBy(claim => claim.ReservationId, StringComparer.Ordinal).ToArray();
            Require(matching.Sum(claim => (long)claim.Quantity) >= required,
                "Reserved terminal sale stock cannot replace missing owned variant reservations with foreign inventory.");
            if (!ordered || variant.SlotIndex == nextSlot) selected ??= matching.First();
        }
        return selected ?? throw new InvalidDataException("Reserved terminal sale stock has no pending original unit.");
    }
}
