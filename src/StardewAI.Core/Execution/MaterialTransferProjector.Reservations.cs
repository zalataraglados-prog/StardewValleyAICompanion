using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

public sealed partial class MaterialTransferProjector
{
    // This predicts physical claim locations; it is not dispatch or settlement authority.
    public MaterialTransferReservationProjection ProjectReservations(
        MaterialInventoryGraph graph,
        MaterialTransferIntent intent,
        StrategyCommitmentLedger ledger,
        ReservationPortfolioMaterialRelocation[] movedClaims)
    {
        var reasons = new List<string>();
        if (graph.InventoryNodes is null || graph.InventoryNodes.Any(node => node is null ||
                node.Slots is null || node.Slots.Any(slot => slot is null)) ||
            graph.AccessPoints is null || graph.AccessPoints.Any(row => row is null) ||
            !long.TryParse(ledger.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerId) ||
            playerId != graph.PlayerId || ledger.SchemaVersion != "strategy_commitment_ledger.v1" ||
            string.IsNullOrWhiteSpace(ledger.LedgerId) || string.IsNullOrWhiteSpace(ledger.SaveId) || ledger.Revision < 0 ||
            ledger.MaterialReservations is null || ledger.MaterialReservations.Any(row => row is null) ||
            movedClaims is null || movedClaims.Any(row => row is null))
        {
            return ReservationProjectionBlocked(ledger, new[] { "material_transfer_reservation_context_invalid" });
        }

        var physical = Project(graph, intent);
        if (physical.Status != "projected")
            return ReservationProjectionBlocked(ledger, physical.BlockingReasons);

        var active = ledger.MaterialReservations.Where(row =>
            row.Status == StrategyCommitmentStatuses.Active).ToArray();
        if (ledger.MaterialReservations.Any(row => string.IsNullOrWhiteSpace(row.ReservationId)) ||
            ledger.MaterialReservations.Select(row => row.ReservationId).Distinct(StringComparer.Ordinal).Count() !=
                ledger.MaterialReservations.Length ||
            active.Any(row => row.Revision < 1 || row.Revision > ledger.Revision || row.OwnerPlayerId != graph.PlayerId ||
                string.IsNullOrWhiteSpace(row.GoalId) || string.IsNullOrWhiteSpace(row.SourceDecisionId)))
            reasons.Add("material_transfer_reservation_identity_invalid");
        reasons.AddRange(new MaterialSupplyProjection().Project(graph, active).BlockingReasons);
        if (active.Any(row => row.OutputCustody is not null &&
                !MaterialOutputCustodyContract.IsAuthorized(row, ledger, MaterialOutputCustodyContract.MuseumConsumer)))
            reasons.Add("material_transfer_reservation_custody_provenance_invalid");
        if (movedClaims.Select(row => row.MaterialReservationId).Distinct(StringComparer.Ordinal).Count() != movedClaims.Length)
            reasons.Add("material_transfer_reservation_move_set_invalid");
        if (reasons.Count > 0)
            return ReservationProjectionBlocked(ledger, reasons);

        foreach (var move in movedClaims)
        {
            var row = active.SingleOrDefault(value => value.ReservationId == move.MaterialReservationId);
            if (row is null || row.NodeId != intent.SourceNodeId || row.SlotIndex != intent.SourceSlotIndex ||
                move.SourceNodeId != row.NodeId || move.SourceSlotIndex != row.SlotIndex ||
                move.DestinationNodeId != intent.DestinationNodeId ||
                move.QualifiedItemId != intent.QualifiedItemId || row.QualifiedItemId != intent.QualifiedItemId ||
                move.Quantity != row.Quantity || move.Quantity <= 0 ||
                !physical.DestinationSlotChanges.Any(change => change.SlotIndex == move.DestinationSlotIndex))
                reasons.Add("material_transfer_reservation_move_binding_invalid");
        }
        foreach (var group in movedClaims.GroupBy(move => move.DestinationSlotIndex))
        {
            var change = physical.DestinationSlotChanges.SingleOrDefault(row => row.SlotIndex == group.Key);
            if (change is null || group.Sum(row => (long)row.Quantity) > (long)change.StackAfter - change.StackBefore)
                reasons.Add("material_transfer_reservation_destination_contribution_exceeded");
        }
        var movedIds = movedClaims.Select(row => row.MaterialReservationId).ToHashSet(StringComparer.Ordinal);
        var retainedSourceQuantity = active.Where(row => row.NodeId == intent.SourceNodeId &&
                row.SlotIndex == intent.SourceSlotIndex && !movedIds.Contains(row.ReservationId))
            .Sum(row => (long)row.Quantity);
        if (movedClaims.Sum(row => (long)row.Quantity) > intent.Quantity ||
            retainedSourceQuantity > physical.SourceStackAfter)
            reasons.Add("material_transfer_reserved_quantity_not_preserved");
        if (reasons.Count > 0)
            return ReservationProjectionBlocked(ledger, reasons);

        var movesById = movedClaims.ToDictionary(row => row.MaterialReservationId, StringComparer.Ordinal);
        var transitions = new List<MaterialTransferReservationTransition>();
        foreach (var row in active.Where(row => row.NodeId == intent.SourceNodeId || row.NodeId == intent.DestinationNodeId)
                     .OrderBy(row => row.ReservationId, StringComparer.Ordinal))
        {
            string destinationNode;
            int destinationSlot;
            if (movesById.TryGetValue(row.ReservationId, out var move))
            {
                destinationNode = move.DestinationNodeId;
                destinationSlot = move.DestinationSlotIndex;
            }
            else
            {
                var slot = physical.NativeSlotTransitions!.SingleOrDefault(value =>
                    value.NodeId == row.NodeId && value.BeforeSlotIndex == row.SlotIndex);
                if (slot?.AfterSlotIndex is not int afterSlot)
                {
                    reasons.Add("material_transfer_reservation_retained_slot_missing");
                    continue;
                }
                destinationNode = row.NodeId;
                destinationSlot = afterSlot;
            }
            transitions.Add(new MaterialTransferReservationTransition
            {
                ReservationId = row.ReservationId,
                ExpectedReservationRevision = row.Revision,
                SourceNodeId = row.NodeId,
                SourceSlotIndex = row.SlotIndex,
                DestinationNodeId = destinationNode,
                DestinationSlotIndex = destinationSlot,
                QualifiedItemId = row.QualifiedItemId,
                Quantity = row.Quantity
            });
        }
        foreach (var group in transitions.GroupBy(row => (row.DestinationNodeId, row.DestinationSlotIndex)))
        {
            var slot = physical.NativeSlotTransitions!.SingleOrDefault(value =>
                value.NodeId == group.Key.DestinationNodeId && value.AfterSlotIndex == group.Key.DestinationSlotIndex);
            if (slot is null || group.Any(row => row.QualifiedItemId != slot.QualifiedItemId) ||
                group.Sum(row => (long)row.Quantity) > slot.StackAfter)
                reasons.Add("material_transfer_reservation_projected_slot_overbooked");
        }
        if (reasons.Count > 0)
            return ReservationProjectionBlocked(ledger, reasons);
        return new MaterialTransferReservationProjection
        {
            Status = "projected",
            LedgerId = ledger.LedgerId,
            ExpectedLedgerRevision = ledger.Revision,
            ReservationTransitions = transitions.ToArray()
        };
    }

    private static MaterialTransferReservationProjection ReservationProjectionBlocked(
        StrategyCommitmentLedger ledger, IEnumerable<string> reasons) => new()
    {
        LedgerId = ledger.LedgerId,
        ExpectedLedgerRevision = ledger.Revision,
        BlockingReasons = reasons.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };
}
