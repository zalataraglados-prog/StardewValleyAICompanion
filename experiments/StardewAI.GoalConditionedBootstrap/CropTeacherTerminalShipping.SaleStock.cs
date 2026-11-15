using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherTerminalShipping
{
    private static MaterialReservation[] ReadSaleStockReservations(StrategyCommitmentLedger ledger, MoneyRevenueCropOutputRetentionProof proof) =>
        ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == "player:" + ledger.PlayerId &&
            row.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == ledger.PlayerId &&
            row.QualifiedItemId == proof.HarvestItemQualifiedId && CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            purpose!.CommitmentId == proof.CommitmentId && (string.IsNullOrWhiteSpace(proof.OutputRetentionKey) ||
                purpose.OutputRetentionKey == proof.OutputRetentionKey)).ToArray();

    private static MaterialReservation SelectSaleStockReservation(SnapshotEnvelope source, StrategyCommitmentLedger ledger,
        MoneyRevenueCropOutputRetentionProof proof, CropTeacherTerminalShippingTarget target)
    {
        var readable = MaterialInventoryGraphReader.TryRead(source, out var graph);
        Require(!string.IsNullOrWhiteSpace(proof.OutputRetentionKey) && readable &&
                graph!.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == ledger.PlayerId,
            "Reserved terminal sale stock requires its original wave key and current material graph.");
        var projection = new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations);
        Require(projection.Status == "available", "Reserved terminal sale stock has an unavailable material projection.");
        var claims = ReadSaleStockReservations(ledger, proof);
        var inventory = source.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        var count = target.AcceptedDepositCount;
        MaterialReservation? selected = null;
        foreach (var variant in target.ReadNativeVariants())
        {
            var consumed = Math.Min(count, variant.PlannedQuantity); count -= consumed;
            var required = variant.PlannedQuantity - consumed;
            if (required == 0) continue;
            var row = inventory.Single(item => item.GetProperty("slot_index").GetInt32() == variant.SlotIndex);
            var identity = MoneyRevenueCropRetentionAccount.InventoryIdentity(row);
            var matching = claims.Where(claim => claim.SlotIndex == variant.SlotIndex &&
                CropSaleStockReservationContract.TryRead(claim, out var purpose) && purpose!.InventoryIdentitySha256 == identity)
                .OrderBy(claim => claim.ReservationId, StringComparer.Ordinal).ToArray();
            Require(matching.Sum(claim => (long)claim.Quantity) >= required,
                "Reserved terminal sale stock cannot replace missing owned variant reservations with foreign inventory.");
            selected ??= matching.First();
        }
        return selected ?? throw new InvalidDataException("Reserved terminal sale stock has no pending original unit.");
    }
}
