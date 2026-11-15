using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.Core.Execution;

// Public native slot effects only; no recipe/RNG simulator and no reservation-consumption authority.
public sealed partial class InventoryConsumptionProjector
{
    public InventoryConsumptionProjection Project(MaterialInventoryGraph graph, StrategyCommitmentLedger ledger,
        InventorySlotConsumption[] consumptions)
    {
        var errors = new List<string>();
        if (graph.SchemaVersion != "material_inventory_graph.v1" || graph.Status != "available" ||
            consumptions is not { Length: > 0 } || consumptions.Any(row => row is null || row.Quantity <= 0 || row.SlotIndex < 0 ||
                string.IsNullOrWhiteSpace(row.NodeId) || string.IsNullOrWhiteSpace(row.QualifiedItemId)) ||
            graph.InventoryNodes is null || graph.InventoryNodes.Any(node => node is null || node.Slots is null || node.Slots.Any(slot => slot is null)) ||
            ledger.MaterialReservations is null || ledger.MaterialReservations.Any(row => row is null) ||
            ledger.MaterialReservations.Select(row => row.ReservationId).Distinct(StringComparer.Ordinal).Count() != ledger.MaterialReservations.Length ||
            consumptions.GroupBy(row => (row.NodeId, row.SlotIndex)).Any(group => group.Count() != 1) ||
            ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
                (row.OwnerPlayerId != graph.PlayerId || row.Revision <= 0 || row.Revision > ledger.Revision ||
                 string.IsNullOrWhiteSpace(row.GoalId) || string.IsNullOrWhiteSpace(row.SourceDecisionId))))
            return Blocked("inventory_consumption_context_invalid");
        var supply = new MaterialSupplyProjection().Project(graph, ledger.MaterialReservations);
        errors.AddRange(supply.BlockingReasons);
        foreach (var consume in consumptions)
        {
            var slot = supply.Slots.SingleOrDefault(row => row.NodeId == consume.NodeId && row.SlotIndex == consume.SlotIndex &&
                row.QualifiedItemId == consume.QualifiedItemId);
            if (slot is null || slot.AvailableQuantity < consume.Quantity)
                errors.Add("inventory_consumption_reserved_or_missing_material:" + consume.NodeId + "#" + consume.SlotIndex);
        }
        if (errors.Count > 0) return Blocked(errors.ToArray());
        var player = "player:" + graph.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var touched = consumptions.Select(row => row.NodeId).Append(player).ToHashSet(StringComparer.Ordinal);
        var nodes = graph.InventoryNodes.Where(node => touched.Contains(node.NodeId)).ToArray();
        if (nodes.Length != touched.Count || nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count() != nodes.Length ||
            nodes.Any(node => !node.ActorUseAuthorized || node.SupplyState != "available" ||
                (node.NodeId == player ? node.InventoryKind != "player_inventory" || node.OwnerPlayerId != graph.PlayerId : node.InventoryKind != "fridge") ||
                node.Slots.Any(slot => slot.SlotIndex < 0 || slot.Stack <= 0 || string.IsNullOrWhiteSpace(slot.RuntimeType))))
            return Blocked("inventory_consumption_native_nodes_unavailable");
        var slots = new List<MaterialTransferNativeSlotTransition>();
        foreach (var node in nodes.OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            var consumed = consumptions.Where(row => row.NodeId == node.NodeId).ToDictionary(row => row.SlotIndex);
            // Both native ingredient and seasoning methods compact a fridge iff a stack became empty.
            var compact = node.InventoryKind == "fridge" && node.Slots.Any(slot => consumed.TryGetValue(slot.SlotIndex, out var row) && row.Quantity == slot.Stack);
            var next = 0;
            foreach (var slot in node.Slots.OrderBy(slot => slot.SlotIndex))
            {
                var remaining = slot.Stack - (consumed.TryGetValue(slot.SlotIndex, out var row) ? row.Quantity : 0);
                slots.Add(new() { NodeId = node.NodeId, BeforeSlotIndex = slot.SlotIndex,
                    AfterSlotIndex = remaining == 0 ? null : compact ? next++ : slot.SlotIndex,
                    QualifiedItemId = slot.QualifiedItemId, Quality = slot.Quality, RuntimeType = slot.RuntimeType,
                    StackBefore = slot.Stack, StackAfter = remaining });
            }
        }
        var passive = new List<MaterialTransferReservationTransition>();
        foreach (var claim in ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active && touched.Contains(row.NodeId)))
        {
            var slot = slots.Single(row => row.NodeId == claim.NodeId && row.BeforeSlotIndex == claim.SlotIndex);
            if (slot.AfterSlotIndex is not int after || slot.StackAfter < claim.Quantity)
                errors.Add("inventory_consumption_reserved_quantity_not_preserved:" + claim.ReservationId);
            else if (after != claim.SlotIndex)
            {
                if (claim.OutputCustody is not null)
                    errors.Add("inventory_consumption_custody_position_binding_required:" + claim.ReservationId);
                if (CropSaleStockReservationContract.TryRead(claim, out var purpose) && !CropSaleStockReservationContract.IsTransferable(purpose!))
                    errors.Add("inventory_consumption_fixed_sale_stock_position:" + claim.ReservationId);
                passive.Add(new() { ReservationId = claim.ReservationId, ExpectedReservationRevision = claim.Revision,
                    SourceNodeId = claim.NodeId, SourceSlotIndex = claim.SlotIndex, DestinationNodeId = claim.NodeId, DestinationSlotIndex = after,
                    QualifiedItemId = claim.QualifiedItemId, Quantity = claim.Quantity });
            }
        }
        return errors.Count > 0 ? Blocked(errors.ToArray()) : new() { Status = "projected", NativeSlotTransitions = slots.ToArray(),
            PassiveReservationTransitions = passive.OrderBy(row => row.ReservationId, StringComparer.Ordinal).ToArray() };
    }

    private static InventoryConsumptionProjection Blocked(params string[] errors) => new()
    { BlockingReasons = errors.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() };
}
