using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    private static AcquisitionMaterialTransferTransitionEvidence
        VerifyMaterialTransfer(
            ActionQueueEnvelope queue,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var reasons = new List<string>();
        var transferItems = queue.Items.Where(item =>
                item.OptionId == "executor.transfer_material" &&
                item.NormalizedCommand?.Steps is
                    [{ StepType: "transfer_material" }])
            .ToArray();
        var moveItems = queue.Items.Where(item =>
                item.OptionId == "executor.move_to_tile")
            .ToArray();
        if (queue.Items.Length != 2 ||
            moveItems.Length != 1 ||
            transferItems.Length != 1 ||
            Array.IndexOf(queue.Items, moveItems[0]) != 0 ||
            Array.IndexOf(queue.Items, transferItems[0]) != 1)
        {
            reasons.Add("supporting_transition_material_transfer_queue_invalid");
        }
        var parameters = transferItems.Length == 1
            ? transferItems[0].NormalizedCommand?.Parameters
            : null;
        var sourceNodeId = UniqueParameter(parameters, "source_node_id");
        var destinationNodeId = UniqueParameter(
            parameters,
            "destination_node_id");
        var sourceSlot = UniqueIntParameter(parameters, "source_slot_index");
        var qualifiedItemId = UniqueParameter(
            parameters,
            "qualified_item_id");
        var quality = UniqueIntParameter(parameters, "quality");
        var quantity = UniqueIntParameter(parameters, "quantity");
        var expectedSourceStack = UniqueIntParameter(
            parameters,
            "expected_source_stack");
        MaterialTransferIntent? intent = null;
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId) ||
            !sourceSlot.HasValue ||
            string.IsNullOrWhiteSpace(qualifiedItemId) ||
            !quality.HasValue || !quantity.HasValue || quantity <= 0 ||
            !expectedSourceStack.HasValue)
        {
            reasons.Add("supporting_transition_material_transfer_intent_invalid");
        }
        else
        {
            intent = new MaterialTransferIntent
            {
                SourceNodeId = sourceNodeId,
                DestinationNodeId = destinationNodeId,
                SourceSlotIndex = sourceSlot.Value,
                QualifiedItemId = qualifiedItemId,
                Quality = quality.Value,
                Quantity = quantity.Value,
                ExpectedSourceStack = expectedSourceStack.Value
            };
        }

        MaterialTransferProjection? projection = null;
        if (intent is not null &&
            AcquisitionMachineInputMaterialStaging.TryReadGraph(
                before,
                out var graph))
        {
            projection = new MaterialTransferProjector().Project(
                graph!,
                intent);
            if (projection.Status != "projected")
                reasons.AddRange(projection.BlockingReasons);
        }
        else if (intent is not null)
        {
            reasons.Add("supporting_transition_material_graph_unavailable");
        }
        var destination = projection?.DestinationSlotChanges is [var only]
            ? only
            : null;
        if (destination is null)
        {
            reasons.Add(
                "supporting_transition_material_transfer_destination_ambiguous");
        }

        var relocations = ReadMaterialRelocations(parameters, reasons);
        var relocation = relocations.Length == 1
            ? relocations[0]
            : null;
        if (intent is not null && destination is not null &&
            (relocation is null ||
             relocation.SourceNodeId != intent.SourceNodeId ||
             relocation.SourceSlotIndex != intent.SourceSlotIndex ||
             relocation.DestinationNodeId != intent.DestinationNodeId ||
             relocation.DestinationSlotIndex != destination.SlotIndex ||
             relocation.QualifiedItemId != intent.QualifiedItemId ||
             relocation.Quantity != intent.Quantity))
        {
            reasons.Add(
                "supporting_transition_material_relocation_lineage_mismatch");
        }

        int? sourceBefore = null;
        int? sourceAfter = null;
        int? destinationBefore = null;
        int? destinationAfter = null;
        var sourceBeforeValue = 0;
        var sourceAfterValue = 0;
        var destinationBeforeValue = 0;
        var destinationAfterValue = 0;
        var quantitiesResolved = intent is not null &&
            TryMaterialNodeQuantity(
                before,
                intent.SourceNodeId,
                intent.QualifiedItemId,
                intent.Quality,
                out sourceBeforeValue) &&
            TryMaterialNodeQuantity(
                after,
                intent.SourceNodeId,
                intent.QualifiedItemId,
                intent.Quality,
                out sourceAfterValue) &&
            TryMaterialNodeQuantity(
                before,
                intent.DestinationNodeId,
                intent.QualifiedItemId,
                intent.Quality,
                out destinationBeforeValue) &&
            TryMaterialNodeQuantity(
                after,
                intent.DestinationNodeId,
                intent.QualifiedItemId,
                intent.Quality,
                out destinationAfterValue);
        if (quantitiesResolved)
        {
            sourceBefore = sourceBeforeValue;
            sourceAfter = sourceAfterValue;
            destinationBefore = destinationBeforeValue;
            destinationAfter = destinationAfterValue;
            if (sourceBefore - sourceAfter != intent!.Quantity ||
                destinationAfter - destinationBefore != intent.Quantity)
            {
                reasons.Add(
                    "supporting_transition_material_transfer_delta_mismatch");
            }
        }
        else
        {
            reasons.Add(
                "supporting_transition_material_transfer_quantity_unavailable");
        }
        if (intent is not null &&
            (!TryMaterialSlotIdentityQuantity(
                 before,
                 intent.SourceNodeId,
                 intent.SourceSlotIndex,
                 intent.QualifiedItemId,
                 intent.Quality,
                 out var exactSourceBefore) ||
             !TryMaterialSlotIdentityQuantity(
                 after,
                 intent.SourceNodeId,
                 intent.SourceSlotIndex,
                 intent.QualifiedItemId,
                 intent.Quality,
                 out var exactSourceAfter) ||
             exactSourceBefore != intent.ExpectedSourceStack ||
             exactSourceAfter != projection?.SourceStackAfter))
        {
            reasons.Add(
                "supporting_transition_material_transfer_source_drifted");
        }
        if (intent is not null && destination is not null &&
            (!TryMaterialSlotIdentityQuantity(
                 before,
                 intent.DestinationNodeId,
                 destination.SlotIndex,
                 intent.QualifiedItemId,
                 intent.Quality,
                 out var exactDestinationBefore) ||
             !TryMaterialSlotIdentityQuantity(
                 after,
                 intent.DestinationNodeId,
                 destination.SlotIndex,
                 intent.QualifiedItemId,
                 intent.Quality,
                 out var exactDestinationAfter) ||
             exactDestinationBefore != destination.StackBefore ||
             exactDestinationAfter != destination.StackAfter))
        {
            reasons.Add(
                "supporting_transition_material_transfer_destination_drifted");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new AcquisitionMaterialTransferTransitionEvidence
        {
            ReservationId = relocation?.ReservationId ?? string.Empty,
            SourceNodeId = sourceNodeId,
            SourceSlotIndex = sourceSlot,
            DestinationNodeId = destinationNodeId,
            DestinationSlotIndex = destination?.SlotIndex,
            QualifiedItemId = qualifiedItemId,
            Quantity = quantity,
            SourceQuantityBefore = sourceBefore,
            SourceQuantityAfter = sourceAfter,
            DestinationQuantityBefore = destinationBefore,
            DestinationQuantityAfter = destinationAfter,
            Resolved = quantitiesResolved &&
                projection?.Status == "projected" &&
                destination is not null,
            Verified = blocking.Length == 0,
            BlockingReasons = blocking
        };
    }

    private static AcquisitionSupportMaterialRelocation[]
        ReadMaterialRelocations(
            SmallModelActionParameter[]? parameters,
            ICollection<string> reasons)
    {
        var json = UniqueParameter(
            parameters,
            "acquisition_support_material_relocations_json");
        try
        {
            var values = JsonSerializer.Deserialize<
                    AcquisitionSupportMaterialRelocation[]>(
                    json,
                    JsonDefaults.Options) ??
                Array.Empty<AcquisitionSupportMaterialRelocation>();
            if (values.Length != 1 ||
                values.Any(value =>
                    string.IsNullOrWhiteSpace(value.ReservationId) ||
                    string.IsNullOrWhiteSpace(value.SourceNodeId) ||
                    value.SourceSlotIndex < 0 ||
                    string.IsNullOrWhiteSpace(value.DestinationNodeId) ||
                    value.DestinationSlotIndex < 0 ||
                    string.IsNullOrWhiteSpace(value.QualifiedItemId) ||
                    value.Quantity <= 0))
            {
                reasons.Add(
                    "supporting_transition_material_relocation_plan_invalid");
                return Array.Empty<AcquisitionSupportMaterialRelocation>();
            }
            return values;
        }
        catch (JsonException)
        {
            reasons.Add(
                "supporting_transition_material_relocation_plan_invalid");
            return Array.Empty<AcquisitionSupportMaterialRelocation>();
        }
    }

    private static bool TryMaterialNodeQuantity(
        SnapshotEnvelope snapshot,
        string nodeId,
        string qualifiedItemId,
        int quality,
        out int quantity)
    {
        quantity = 0;
        if (!AcquisitionMachineInputMaterialStaging.TryReadGraph(
                snapshot,
                out var graph))
        {
            return false;
        }
        var nodes = graph!.InventoryNodes.Where(node =>
                node.NodeId == nodeId)
            .ToArray();
        if (nodes.Length != 1)
            return false;
        var total = nodes[0].Slots.Where(slot =>
                slot.QualifiedItemId == qualifiedItemId &&
                slot.Quality == quality)
            .Sum(slot => (long)slot.Stack);
        if (total > int.MaxValue)
            return false;
        quantity = (int)total;
        return true;
    }

    private static bool TryMaterialSlotIdentityQuantity(
        SnapshotEnvelope snapshot,
        string nodeId,
        int slotIndex,
        string qualifiedItemId,
        int quality,
        out int quantity)
    {
        quantity = 0;
        if (!AcquisitionMachineInputMaterialStaging.TryReadGraph(
                snapshot,
                out var graph))
        {
            return false;
        }
        var nodes = graph!.InventoryNodes.Where(node =>
                node.NodeId == nodeId)
            .ToArray();
        if (nodes.Length != 1)
            return false;
        var slots = nodes[0].Slots.Where(slot =>
                slot.SlotIndex == slotIndex)
            .ToArray();
        if (slots.Length == 0)
            return true;
        if (slots.Length != 1 ||
            slots[0].QualifiedItemId != qualifiedItemId ||
            slots[0].Quality != quality)
        {
            return false;
        }
        quantity = slots[0].Stack;
        return quantity >= 0;
    }
}
