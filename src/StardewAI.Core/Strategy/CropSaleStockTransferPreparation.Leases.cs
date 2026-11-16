using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public sealed partial class CropSaleStockTransferPreparation
{
    private static CropSaleStockTransferPreparation? CreateLease(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        MaterialInventoryGraph graph, MaterialTransferIntent intent, out string[] reasons)
    {
        reasons = Array.Empty<string>();
        if (!ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
            (row.NodeId == intent.SourceNodeId || row.NodeId == intent.DestinationNodeId) &&
            CropSaleStockReservationContract.TryRead(row, out _))) return null;
        var supply = new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations);
        var source = supply.Slots.SingleOrDefault(row => row.NodeId == intent.SourceNodeId && row.SlotIndex == intent.SourceSlotIndex);
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            snapshot.SaveId.Value != ledger.SaveId || snapshot.PlayerId.Value != ledger.PlayerId ||
            supply.Status != "available" || source is null || source.AvailableQuantity < intent.Quantity)
        { reasons = new[] { "crop_sale_stock_transfer_lease_free_quantity_unavailable" }; return null; }
        var physical = new MaterialTransferProjector().Project(graph, intent);
        if (physical.Status != "projected") { reasons = physical.BlockingReasons; return null; }
        var changes = physical.DestinationSlotChanges.Where(row => row.StackAfter > row.StackBefore).OrderBy(row => row.SlotIndex).ToArray();
        if (changes.Length == 0 || changes.Sum(row => (long)row.StackAfter - row.StackBefore) != intent.Quantity)
        { reasons = new[] { "crop_sale_stock_transfer_lease_allocation_not_conserved" }; return null; }
        var portfolio = "inventory-transfer-lease:" + Hash(JsonSerializer.Serialize(new
            { snapshot.StateHash, ledger.LedgerId, ledger.Revision, Intent = intent }));
        var claims = changes.Select(row => new MaterialReservationUpsertRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            ReservationId = CropSaleStockTransferLeaseContract.ReservationId(portfolio, row.SlotIndex),
            SourceDecisionId = portfolio, GoalId = "earn_money", NodeId = intent.SourceNodeId, SlotIndex = intent.SourceSlotIndex,
            QualifiedItemId = intent.QualifiedItemId, Quantity = row.StackAfter - row.StackBefore,
            Purpose = CropSaleStockTransferLeaseContract.Purpose(portfolio, snapshot.StateHash, row.SlotIndex)
        }).ToArray();
        if (claims.Any(claim => ledger.MaterialReservations.Any(row => row.ReservationId == claim.ReservationId) ||
            ledger.CurrencyReservations.Any(row => row.ReservationId == claim.ReservationId) || ledger.History.Any(row => row.CommitmentId == claim.ReservationId)))
        { reasons = new[] { "crop_sale_stock_transfer_lease_identity_exists" }; return null; }
        var request = new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = portfolio, SourceDecisionId = portfolio, GoalId = "earn_money", MaterialClaims = claims,
            MaterialTransferPreparation = new()
            {
                RouteSourceDecisionId = portfolio, Intent = intent,
                MaterialRelocations = changes.Select(row => new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = CropSaleStockTransferLeaseContract.ReservationId(portfolio, row.SlotIndex),
                    SourceNodeId = intent.SourceNodeId, SourceSlotIndex = intent.SourceSlotIndex,
                    DestinationNodeId = intent.DestinationNodeId, DestinationSlotIndex = row.SlotIndex,
                    QualifiedItemId = intent.QualifiedItemId, Quantity = row.StackAfter - row.StackBefore
                }).ToArray()
            }
        };
        var preview = new ReservationPortfolioLedgerService().Commit(ledger, snapshot, request, ledger.UpdatedAt);
        if (!preview.Accepted || preview.Ledger is null || !CropSaleStockTransferLeaseContract.IsBound(preview.Ledger))
        { reasons = preview.Errors; return null; }
        return new(request, preview.Ledger);
    }
}
