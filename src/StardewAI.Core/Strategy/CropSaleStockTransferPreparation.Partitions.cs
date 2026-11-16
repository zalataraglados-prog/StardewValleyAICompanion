using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class CropSaleStockTransferPreparation
{
    private static bool AllocatePartitions(ReservationPortfolioCommitRequest request, StrategyCommitmentLedger ledger,
        MaterialReservation[] claims, MaterialTransferSlotChange[] destinations, out string[] reasons)
    {
        reasons = Array.Empty<string>();
        var changes = destinations.OrderBy(row => row.SlotIndex).ToArray();
        var capacity = changes.Select(row => checked(row.StackAfter - row.StackBefore)).ToArray();
        var preparation = request.MaterialTransferPreparation!;
        if (capacity.Any(value => value <= 0) || capacity.Sum(value => (long)value) != preparation.Intent.Quantity)
        { reasons = new[] { "crop_sale_stock_prepare_allocation_not_conserved" }; return false; }
        var partitions = new List<MaterialReservationUpsertRequest>();
        var moves = new List<ReservationPortfolioMaterialRelocation>();
        var stepHash = Hash(request.PortfolioId);
        var destinationIndex = 0;
        foreach (var claim in claims)
        {
            var pieces = new List<(int Slot, int Quantity)>();
            var remaining = claim.Quantity;
            while (remaining > 0 && destinationIndex < changes.Length)
            {
                var quantity = Math.Min(remaining, capacity[destinationIndex]);
                pieces.Add((changes[destinationIndex].SlotIndex, quantity));
                remaining -= quantity; capacity[destinationIndex] -= quantity;
                if (capacity[destinationIndex] == 0) destinationIndex++;
            }
            if (remaining != 0)
            { reasons = new[] { "crop_sale_stock_prepare_allocation_not_conserved" }; return false; }
            for (var index = 0; index < pieces.Count; index++)
            {
                var piece = pieces[index];
                var id = index == 0 ? claim.ReservationId :
                    CropSaleStockReservationContract.PartitionReservationId(claim.ReservationId, stepHash, index);
                if (index > 0 && (ledger.MaterialReservations.Any(row => row.ReservationId == id) ||
                    ledger.CurrencyReservations.Any(row => row.ReservationId == id) || ledger.History.Any(row => row.CommitmentId == id)))
                { reasons = new[] { "crop_sale_stock_prepare_partition_identity_exists" }; return false; }
                if (pieces.Count > 1)
                    partitions.Add(new MaterialReservationUpsertRequest
                    {
                        StateHash = request.StateHash, ExpectedLedgerRevision = request.ExpectedLedgerRevision,
                        ReservationId = id, SourceDecisionId = claim.SourceDecisionId, GoalId = claim.GoalId,
                        NodeId = claim.NodeId, SlotIndex = claim.SlotIndex, QualifiedItemId = claim.QualifiedItemId,
                        Quantity = piece.Quantity, Purpose = index == 0 ? claim.Purpose :
                            CropSaleStockReservationContract.PartitionedPurpose(claim, stepHash, index)
                    });
                moves.Add(new ReservationPortfolioMaterialRelocation
                {
                    MaterialReservationId = id, SourceNodeId = claim.NodeId, SourceSlotIndex = claim.SlotIndex,
                    DestinationNodeId = preparation.Intent.DestinationNodeId, DestinationSlotIndex = piece.Slot,
                    QualifiedItemId = claim.QualifiedItemId, Quantity = piece.Quantity
                });
            }
        }
        // Parent IDs sort before their children, so the original atomic service shrinks before adding.
        request.MaterialClaims = partitions.ToArray(); preparation.MaterialRelocations = moves.ToArray();
        return true;
    }
}
