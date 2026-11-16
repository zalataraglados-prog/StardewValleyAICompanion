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
        // Only the explicit new harvest protocol opts in; old ledger/candidate behavior is unchanged.
        if (ledger is null || !ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == "player:" + ledger.PlayerId && CropSaleStockReservationContract.TryRead(row, out _))) return null;
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            ledger.SaveId != snapshot.SaveId.Value || ledger.PlayerId != snapshot.PlayerId.Value ||
            ledger.SourceStateHash != snapshot.StateHash || !MaterialInventoryGraphReader.TryRead(snapshot, out var graph) ||
            graph!.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) != ledger.PlayerId)
            return new(ledger.PlayerId, null);
        return new(ledger.PlayerId, new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations));
    }

    private sealed class SaleStockBudget
    {
        private readonly string playerNode;
        private readonly MaterialSupplyProjectionResult? supply;
        internal SaleStockBudget(string playerId, MaterialSupplyProjectionResult? supply)
        { playerNode = "player:" + playerId; this.supply = supply; }
        internal string BlockingReason => supply?.Status == "available" ?
            "inventory_item_reserved_for_crop_sale" : "crop_sale_stock_material_supply_unavailable";

        internal int AvailableQuantity(int slot, string itemId, int physicalQuantity)
        {
            if (supply?.Status != "available") return 0;
            var rows = supply.Slots.Where(row => row.NodeId == playerNode && row.SlotIndex == slot && row.QualifiedItemId == itemId).ToArray();
            return rows is [var row] && row.Stack == physicalQuantity ? row.AvailableQuantity : 0;
        }

        internal void LimitSale(EconomicCandidate candidate)
        {
            var quantity = candidate.SlotIndex.HasValue ?
                AvailableQuantity(candidate.SlotIndex.Value, candidate.QualifiedItemId, candidate.Quantity) : 0;
            candidate.Quantity = quantity;
            candidate.TotalValue = checked(quantity * candidate.UnitPrice);
            if (quantity > 0) return;
            candidate.Available = false;
            candidate.BlockReasons = candidate.BlockReasons.Concat(new[] { BlockingReason }).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
