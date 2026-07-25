using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution;

public sealed class MaterialTransferProjector
{
    public MaterialTransferProjection Project(
        MaterialInventoryGraph graph,
        MaterialTransferIntent intent)
    {
        var reasons = new List<string>();
        if (graph.SchemaVersion != "material_inventory_graph.v1" || graph.Status != "available")
        {
            reasons.Add("material_inventory_graph_unavailable");
        }

        if (intent.SchemaVersion != "material_transfer_intent.v1")
        {
            reasons.Add("material_transfer_intent_schema_unsupported");
        }

        var source = SingleNode(graph, intent.SourceNodeId, "source", reasons);
        var destination = SingleNode(graph, intent.DestinationNodeId, "destination", reasons);
        if (source is null || destination is null)
        {
            return Blocked(reasons);
        }

        if (source.NodeId == destination.NodeId)
        {
            reasons.Add("material_transfer_nodes_must_differ");
        }

        if (source.SupplyState != "available" || destination.SupplyState != "available")
        {
            reasons.Add("material_transfer_nodes_not_available");
        }

        var sourceIsPlayer = source.InventoryKind == "player_inventory";
        var destinationIsPlayer = destination.InventoryKind == "player_inventory";
        if (sourceIsPlayer == destinationIsPlayer)
        {
            reasons.Add("material_transfer_requires_one_player_inventory");
        }

        var chest = sourceIsPlayer ? destination : source;
        var player = sourceIsPlayer ? source : destination;
        if (chest.InventoryKind != "chest")
        {
            reasons.Add("material_transfer_only_supports_normal_placed_chest");
        }

        var access = graph.AccessPoints
            .Where(row => row.NodeId == chest.NodeId)
            .ToArray();
        if (access.Length != 1 ||
            access[0].AccessKind != "placed_chest" ||
            access[0].SpecialChestType != "None")
        {
            reasons.Add("material_transfer_requires_unique_normal_chest_access");
        }
        else if (access[0].LockedByOtherPlayer)
        {
            reasons.Add("material_transfer_chest_locked_by_other_player");
        }

        if (player.LocationId != chest.LocationId)
        {
            reasons.Add("material_transfer_player_not_in_chest_location");
        }

        var sourceSlots = source.Slots
            .Where(slot => slot.SlotIndex == intent.SourceSlotIndex)
            .ToArray();
        var sourceSlot = sourceSlots.Length == 1 ? sourceSlots[0] : null;
        if (sourceSlot is null)
        {
            reasons.Add("material_transfer_source_slot_not_unique");
        }
        else
        {
            if (sourceSlot.QualifiedItemId != intent.QualifiedItemId ||
                sourceSlot.Quality != intent.Quality)
            {
                reasons.Add("material_transfer_source_identity_drifted");
            }

            if (sourceSlot.Stack != intent.ExpectedSourceStack)
            {
                reasons.Add("material_transfer_source_stack_drifted");
            }

            if (sourceSlot.MaximumStackSize <= 0)
            {
                reasons.Add("material_transfer_source_not_stackable");
            }
        }

        if (intent.Quantity <= 0 ||
            sourceSlot is not null && intent.Quantity > sourceSlot.Stack)
        {
            reasons.Add("material_transfer_quantity_invalid");
        }

        if (reasons.Count > 0 || sourceSlot is null)
        {
            return Blocked(reasons);
        }

        var changes = ProjectNativeInsertion(destination, sourceSlot, intent.Quantity, reasons);
        if (reasons.Count > 0)
        {
            return Blocked(reasons);
        }

        var destinationBefore = destination.Slots
            .Where(slot => SameStackIdentity(slot, sourceSlot))
            .Sum(slot => slot.Stack);
        return new MaterialTransferProjection
        {
            Status = "projected",
            NativeBranch = sourceIsPlayer
                ? "Chest.ShowMenu->ItemGrabMenu.receiveRightClick(player)->Chest.grabItemFromInventory"
                : "Chest.ShowMenu->ItemGrabMenu.receiveRightClick(chest)->Chest.grabItemFromChest",
            SourceStackAfter = sourceSlot.Stack - intent.Quantity,
            DestinationQuantityBefore = destinationBefore,
            DestinationQuantityAfter = destinationBefore + intent.Quantity,
            DestinationSlotChanges = changes
        };
    }

    private static MaterialInventoryNode? SingleNode(
        MaterialInventoryGraph graph,
        string nodeId,
        string role,
        ICollection<string> reasons)
    {
        var rows = graph.InventoryNodes.Where(row => row.NodeId == nodeId).ToArray();
        if (rows.Length != 1)
        {
            reasons.Add("material_transfer_" + role + "_node_not_unique");
            return null;
        }

        return rows[0];
    }

    private static MaterialTransferSlotChange[] ProjectNativeInsertion(
        MaterialInventoryNode destination,
        MaterialInventorySlot source,
        int quantity,
        ICollection<string> reasons)
    {
        var remaining = quantity;
        var changes = new List<MaterialTransferSlotChange>();
        var compactBeforeInsertion = destination.InventoryKind == "chest";
        var projectedSlots = destination.Slots
            .OrderBy(slot => slot.SlotIndex)
            .Select((slot, index) => new
            {
                Slot = slot,
                ProjectedIndex = compactBeforeInsertion ? index : slot.SlotIndex
            })
            .ToArray();
        foreach (var row in projectedSlots
            .Where(row =>
                SameStackIdentity(row.Slot, source) &&
                row.Slot.Stack < row.Slot.MaximumStackSize))
        {
            var moved = Math.Min(
                remaining,
                row.Slot.MaximumStackSize - row.Slot.Stack);
            changes.Add(new MaterialTransferSlotChange
            {
                SlotIndex = row.ProjectedIndex,
                StackBefore = row.Slot.Stack,
                StackAfter = row.Slot.Stack + moved
            });
            remaining -= moved;
            if (remaining == 0)
            {
                break;
            }
        }

        var occupied = projectedSlots
            .Select(row => row.ProjectedIndex)
            .ToHashSet();
        for (var slotIndex = 0; remaining > 0 && slotIndex < destination.Capacity; slotIndex++)
        {
            if (occupied.Contains(slotIndex))
            {
                continue;
            }

            var moved = Math.Min(remaining, source.MaximumStackSize);
            changes.Add(new MaterialTransferSlotChange
            {
                SlotIndex = slotIndex,
                StackBefore = 0,
                StackAfter = moved
            });
            remaining -= moved;
        }

        if (remaining > 0)
        {
            reasons.Add("material_transfer_destination_capacity_insufficient");
            return Array.Empty<MaterialTransferSlotChange>();
        }

        return changes.ToArray();
    }

    private static bool SameStackIdentity(
        MaterialInventorySlot left,
        MaterialInventorySlot right) =>
        left.QualifiedItemId == right.QualifiedItemId &&
        left.Quality == right.Quality &&
        left.RuntimeType == right.RuntimeType;

    private static MaterialTransferProjection Blocked(IEnumerable<string> reasons) => new()
    {
        BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray()
    };
}
