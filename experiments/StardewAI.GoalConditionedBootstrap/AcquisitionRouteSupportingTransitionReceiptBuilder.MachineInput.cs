using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    private static AcquisitionMachineInputTransitionEvidence VerifyMachineInput(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var item = queue.Items.Single();
        var command = item.NormalizedCommand;
        var parameters = command?.Parameters;
        var reasons = new List<string>();
        if (item.OptionId != "executor.load_machine_input" ||
            command is null ||
            command.Steps is not { Length: 1 } ||
            command.Steps[0].StepType != "load_machine_input")
        {
            reasons.Add("supporting_transition_not_machine_input_load");
        }

        var location = UniqueParameter(parameters, "target_location");
        var x = UniqueIntParameter(parameters, "target_tile_x");
        var y = UniqueIntParameter(parameters, "target_tile_y");
        var machineQualifiedItemId = UniqueParameter(
            parameters,
            "acquisition_support_machine_qualified_item_id");
        var inputQualifiedItemId = UniqueParameter(
            parameters,
            "acquisition_support_input_qualified_item_id");
        var inputSlot = UniqueIntParameter(
            parameters,
            "acquisition_support_input_slot_index");
        var inputRequired = UniqueIntParameter(
            parameters,
            "acquisition_support_input_required_quantity");
        var predictedMinutes = UniqueIntParameter(
            parameters,
            "acquisition_support_predicted_processing_minutes");
        var routeKind = UniqueParameter(parameters, "acquisition_route_kind");
        var sourceId = UniqueParameter(parameters, "acquisition_source_id");
        var outputQualifiedItemId = UniqueParameter(
            parameters,
            "acquisition_qualified_item_id");
        var consumptions = ReadMaterialConsumptions(
            parameters,
            reasons);
        if (string.IsNullOrWhiteSpace(location) ||
            !x.HasValue || !y.HasValue ||
            string.IsNullOrWhiteSpace(machineQualifiedItemId) ||
            string.IsNullOrWhiteSpace(inputQualifiedItemId) ||
            !inputSlot.HasValue || !inputRequired.HasValue ||
            inputRequired <= 0 || !predictedMinutes.HasValue ||
            predictedMinutes < 0 ||
            string.IsNullOrWhiteSpace(routeKind) ||
            string.IsNullOrWhiteSpace(sourceId) ||
            string.IsNullOrWhiteSpace(outputQualifiedItemId))
        {
            reasons.Add("supporting_transition_machine_lineage_incomplete");
        }
        var primary = consumptions.Where(value =>
                value.InputRole == "primary_input" &&
                value.SlotIndex == inputSlot &&
                value.QualifiedItemId == inputQualifiedItemId &&
                value.ConsumedQuantity == inputRequired)
            .ToArray();
        if (primary.Length != 1)
        {
            reasons.Add(
                "supporting_transition_machine_primary_consumption_mismatch");
        }

        var materialEvidence = consumptions.Select(consumption =>
                VerifyMaterialConsumption(before, after, consumption))
            .ToArray();
        if (materialEvidence.Any(value => !value.Verified))
        {
            reasons.Add(
                "supporting_transition_machine_material_delta_mismatch");
        }

        var beforeMachine = MachineAt(
            before,
            location,
            x,
            y,
            out var beforeMachineResolved);
        var afterMachine = MachineAt(
            after,
            location,
            x,
            y,
            out var afterMachineResolved);
        if (!beforeMachineResolved || !afterMachineResolved ||
            !beforeMachine.HasValue || !afterMachine.HasValue)
        {
            reasons.Add("supporting_transition_machine_state_unavailable");
        }
        var beforeCapacity = beforeMachine.HasValue
            ? MachineCapacityState(beforeMachine.Value)
            : string.Empty;
        var afterCapacity = afterMachine.HasValue
            ? MachineCapacityState(afterMachine.Value)
            : string.Empty;
        var afterMinutes = afterMachine.HasValue
            ? ReadInt(afterMachine.Value, "minutes_until_ready")
            : (int?)null;
        if (!beforeMachine.HasValue ||
            ReadString(beforeMachine.Value, "qualified_item_id") !=
                machineQualifiedItemId ||
            beforeCapacity != "idle" ||
            !MachineHeldItemIsNull(beforeMachine.Value))
        {
            reasons.Add("supporting_transition_machine_before_state_not_idle");
        }
        if (!afterMachine.HasValue ||
            ReadString(afterMachine.Value, "qualified_item_id") !=
                machineQualifiedItemId ||
            !MachineItemMatches(
                afterMachine.Value,
                "last_input_item",
                inputQualifiedItemId) ||
            !MachineItemMatches(
                afterMachine.Value,
                "held_item",
                outputQualifiedItemId) ||
            !MachineRouteSourceMatches(
                afterMachine.Value,
                routeKind,
                sourceId,
                outputQualifiedItemId) ||
            !MachineTimerMatches(
                afterMachine.Value,
                predictedMinutes))
        {
            reasons.Add("supporting_transition_machine_after_state_mismatch");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new AcquisitionMachineInputTransitionEvidence
        {
            TargetLocationId = location,
            TargetTileX = x,
            TargetTileY = y,
            MachineQualifiedItemId = machineQualifiedItemId,
            InputQualifiedItemId = inputQualifiedItemId,
            OutputQualifiedItemId = outputQualifiedItemId,
            BeforeCapacityState = beforeCapacity,
            AfterCapacityState = afterCapacity,
            AfterMinutesUntilReady = afterMinutes,
            MaterialConsumptions = materialEvidence,
            Resolved = beforeMachineResolved && afterMachineResolved &&
                materialEvidence.All(value =>
                    value.BeforeQuantity.HasValue &&
                    value.AfterQuantity.HasValue),
            Verified = blocking.Length == 0,
            BlockingReasons = blocking
        };
    }

    private static AcquisitionSupportMaterialConsumption[]
        ReadMaterialConsumptions(
            SmallModelActionParameter[]? parameters,
            ICollection<string> reasons)
    {
        var json = UniqueParameter(
            parameters,
            "acquisition_support_material_consumptions_json");
        try
        {
            var values = JsonSerializer.Deserialize<
                    AcquisitionSupportMaterialConsumption[]>(
                    json,
                    JsonDefaults.Options) ??
                Array.Empty<AcquisitionSupportMaterialConsumption>();
            if (values.Length == 0 ||
                values.Any(value =>
                    string.IsNullOrWhiteSpace(value.ReservationId) ||
                    string.IsNullOrWhiteSpace(value.NodeId) ||
                    value.SlotIndex < 0 ||
                    string.IsNullOrWhiteSpace(value.QualifiedItemId) ||
                    value.ConsumedQuantity <= 0 ||
                    value.InputRole is not (
                        "primary_input" or "additional_input")) ||
                values.Select(value => value.ReservationId)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    values.Length ||
                values.Select(value =>
                        value.NodeId + ":" + value.SlotIndex)
                    .Distinct(StringComparer.Ordinal).Count() !=
                    values.Length)
            {
                reasons.Add(
                    "supporting_transition_material_consumption_plan_invalid");
                return Array.Empty<AcquisitionSupportMaterialConsumption>();
            }
            return values;
        }
        catch (JsonException)
        {
            reasons.Add(
                "supporting_transition_material_consumption_plan_invalid");
            return Array.Empty<AcquisitionSupportMaterialConsumption>();
        }
    }

    private static AcquisitionSupportMaterialConsumptionEvidence
        VerifyMaterialConsumption(
            SnapshotEnvelope before,
            SnapshotEnvelope after,
            AcquisitionSupportMaterialConsumption consumption)
    {
        var beforeResolved = TryMaterialSlotQuantity(
            before,
            consumption.NodeId,
            consumption.SlotIndex,
            consumption.QualifiedItemId,
            out var beforeQuantity);
        var afterResolved = TryMaterialSlotQuantity(
            after,
            consumption.NodeId,
            consumption.SlotIndex,
            consumption.QualifiedItemId,
            out var afterQuantity);
        var observed = beforeResolved && afterResolved
            ? beforeQuantity - afterQuantity
            : (int?)null;
        var reasons = new List<string>();
        if (!beforeResolved || !afterResolved)
            reasons.Add("supporting_transition_material_slot_unavailable");
        if (beforeResolved && beforeQuantity < consumption.ConsumedQuantity)
            reasons.Add("supporting_transition_material_before_quantity_short");
        if (observed != consumption.ConsumedQuantity)
            reasons.Add("supporting_transition_material_delta_not_exact");
        return new AcquisitionSupportMaterialConsumptionEvidence
        {
            ReservationId = consumption.ReservationId,
            NodeId = consumption.NodeId,
            SlotIndex = consumption.SlotIndex,
            QualifiedItemId = consumption.QualifiedItemId,
            ExpectedConsumedQuantity = consumption.ConsumedQuantity,
            BeforeQuantity = beforeResolved ? beforeQuantity : null,
            AfterQuantity = afterResolved ? afterQuantity : null,
            ObservedConsumedQuantity = observed,
            Verified = reasons.Count == 0,
            BlockingReasons = reasons.ToArray()
        };
    }

    private static bool TryMaterialSlotQuantity(
        SnapshotEnvelope snapshot,
        string nodeId,
        int slotIndex,
        string qualifiedItemId,
        out int quantity)
    {
        quantity = 0;
        if (!TryStateFieldValue(
                snapshot,
                "farm",
                "material_inventory_graph",
                out var graph) ||
            graph.ValueKind != JsonValueKind.Object ||
            !graph.TryGetProperty("inventory_nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var nodeMatches = nodes.EnumerateArray().Where(node =>
                node.ValueKind == JsonValueKind.Object &&
                ReadString(node, "node_id") == nodeId)
            .ToArray();
        if (nodeMatches.Length != 1 ||
            !nodeMatches[0].TryGetProperty("slots", out var slots) ||
            slots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var slotMatches = slots.EnumerateArray().Where(slot =>
                slot.ValueKind == JsonValueKind.Object &&
                ReadInt(slot, "slot_index") == slotIndex)
            .ToArray();
        if (slotMatches.Length == 0)
            return true;
        if (slotMatches.Length != 1 ||
            ReadString(slotMatches[0], "qualified_item_id") !=
                qualifiedItemId)
        {
            return false;
        }
        quantity = ReadInt(slotMatches[0], "stack");
        return quantity >= 0;
    }

    private static JsonElement? MachineAt(
        SnapshotEnvelope snapshot,
        string location,
        int? x,
        int? y,
        out bool resolved)
    {
        resolved = TryStateFieldValue(
            snapshot,
            "farm",
            "machines",
            out var machines) &&
            machines.ValueKind == JsonValueKind.Array &&
            !string.IsNullOrWhiteSpace(location) && x.HasValue && y.HasValue;
        if (!resolved)
            return null;
        var matches = machines.EnumerateArray().Where(machine =>
                machine.ValueKind == JsonValueKind.Object &&
                ReadString(machine, "location_id") == location &&
                ReadInt(machine, "tile_x") == x &&
                ReadInt(machine, "tile_y") == y)
            .ToArray();
        resolved = matches.Length == 1;
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string MachineCapacityState(JsonElement machine)
    {
        var readyResolved = TryReadBool(
            machine,
            "ready_for_harvest",
            out var ready);
        var minutes = ReadInt(machine, "minutes_until_ready");
        if (!readyResolved)
            return string.Empty;
        return ready
            ? "ready_output"
            : minutes > 0
                ? "processing"
                : "idle";
    }

    private static bool MachineHeldItemIsNull(JsonElement machine) =>
        machine.TryGetProperty("held_item", out var held) &&
        held.ValueKind == JsonValueKind.Null;

    private static bool MachineItemMatches(
        JsonElement machine,
        string property,
        string qualifiedItemId) =>
        machine.TryGetProperty(property, out var item) &&
        item.ValueKind == JsonValueKind.Object &&
        ReadString(item, "qualified_item_id") == qualifiedItemId;

    private static bool MachineRouteSourceMatches(
        JsonElement machine,
        string routeKind,
        string sourceId,
        string qualifiedItemId)
    {
        if (!machine.TryGetProperty(
                "active_output_authoritative_route_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var rows = sources.EnumerateArray().ToArray();
        return rows.Length == 1 &&
            ReadString(rows[0], "route_kind") == routeKind &&
            ReadString(rows[0], "source_id") == sourceId &&
            ReadString(rows[0], "qualified_item_id") == qualifiedItemId;
    }

    private static bool MachineTimerMatches(
        JsonElement machine,
        int? predictedMinutes)
    {
        if (!predictedMinutes.HasValue ||
            !TryReadBool(machine, "ready_for_harvest", out var ready))
        {
            return false;
        }
        var minutes = ReadInt(machine, "minutes_until_ready");
        return ready
            ? minutes == 0
            : minutes > 0 && minutes <= predictedMinutes.Value;
    }
}
