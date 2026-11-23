using System;
using System.Linq;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private static SaleStockBudget? ReadSaleStockBudget(SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger)
    {
        if (ledger is null) return null;
        var protectsCropStock = ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == "player:" + ledger.PlayerId && CropSaleStockReservationContract.TryRead(row, out _));
        var bundleLedger = CommunityCenterShipmentProtection.Applies(ledger) ? ledger : null;
        if (!protectsCropStock) return bundleLedger is null ? null : new(snapshot, bundleLedger, null, false, null);
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            ledger.SaveId != snapshot.SaveId.Value || ledger.PlayerId != snapshot.PlayerId.Value ||
            ledger.SourceStateHash != snapshot.StateHash || !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph!.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) != ledger.PlayerId)
            return new(snapshot, bundleLedger, ledger.PlayerId, true, null);
        return new(snapshot, bundleLedger, ledger.PlayerId, true, new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations));
    }

    private sealed class SaleStockBudget
    {
        private readonly string playerNode;
        private readonly MaterialSupplyProjectionResult? supply;
        private readonly SnapshotEnvelope snapshot;
        private readonly StrategyCommitmentLedger? bundleLedger;
        private readonly bool protectsCropStock;

        internal SaleStockBudget(SnapshotEnvelope snapshot, StrategyCommitmentLedger? bundleLedger,
            string? playerId, bool protectsCropStock, MaterialSupplyProjectionResult? supply)
        {
            this.snapshot = snapshot;
            this.bundleLedger = bundleLedger;
            this.protectsCropStock = protectsCropStock;
            playerNode = "player:" + playerId;
            this.supply = supply;
        }

        internal string BlockingReason(int? slot, string itemId) =>
            CommunityCenterShipmentProtection.BlockReason(snapshot, bundleLedger, slot, itemId, 1) ??
            (supply?.Status == "available" ? "inventory_item_reserved_for_crop_sale" : "crop_sale_stock_material_supply_unavailable");

        internal int AvailableQuantity(int slot, string itemId, int physicalQuantity)
        {
            var bundleFree = CommunityCenterShipmentProtection.FreeQuantity(snapshot, bundleLedger, slot, itemId, out _) ?? physicalQuantity;
            if (!protectsCropStock) return Math.Min(bundleFree, physicalQuantity);
            if (supply?.Status != "available") return 0;
            var rows = supply.Slots.Where(row => row.NodeId == playerNode && row.SlotIndex == slot && row.QualifiedItemId == itemId).ToArray();
            return rows is [var row] && row.Stack == physicalQuantity ? Math.Min(bundleFree, row.AvailableQuantity) : 0;
        }

        internal void LimitSale(EconomicCandidate candidate)
        {
            var quantity = candidate.SlotIndex.HasValue ?
                AvailableQuantity(candidate.SlotIndex.Value, candidate.QualifiedItemId, candidate.Quantity) : 0;
            candidate.Quantity = quantity;
            candidate.TotalValue = checked(quantity * candidate.UnitPrice);
            if (quantity > 0) return;
            candidate.Available = false;
            candidate.BlockReasons = candidate.BlockReasons.Concat(new[] { BlockingReason(candidate.SlotIndex, candidate.QualifiedItemId) }).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
