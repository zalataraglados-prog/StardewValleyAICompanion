using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    private static AcquisitionMachineCapacityTransitionEvidence
        VerifyMachineCapacity(
            ActionQueueEnvelope queue,
            SnapshotEnvelope before,
            SnapshotEnvelope after)
    {
        var command = queue.Items.Last().NormalizedCommand;
        var parameters = command?.Parameters;
        var reasons = new List<string>();
        var stage = UniqueParameter(
            parameters,
            "acquisition_support_machine_intent_stage");
        var intentId = UniqueParameter(
            parameters,
            "acquisition_support_machine_intent_id");
        var machineQualifiedItemId = UniqueParameter(
            parameters,
            "acquisition_support_machine_qualified_item_id");
        var sourceJson = UniqueParameter(
            parameters,
            "acquisition_support_machine_sources_json");
        if (string.IsNullOrWhiteSpace(intentId) ||
            string.IsNullOrWhiteSpace(machineQualifiedItemId) ||
            !SupportSourceMatches(
                sourceJson,
                machineQualifiedItemId,
                intentId))
        {
            reasons.Add("supporting_transition_machine_capacity_lineage_invalid");
        }

        var beforeQuantity = PlayerInventoryQuantity(
            before,
            machineQualifiedItemId);
        var afterQuantity = PlayerInventoryQuantity(
            after,
            machineQualifiedItemId);
        int? inventoryDelta = beforeQuantity.HasValue && afterQuantity.HasValue
            ? afterQuantity.Value - beforeQuantity.Value
            : null;
        string location = string.Empty;
        int? x = null;
        int? y = null;
        bool? beforeMachinePresent = null;
        bool? afterMachinePresent = null;

        if (stage == MachineSupportIntentStages.CraftSelected)
        {
            var item = queue.Items.Single();
            var outputCount = UniqueIntParameter(parameters, "output_count");
            if (item.OptionId != "executor.craft_machine_item" ||
                command?.Steps is not { Length: 1 } ||
                command.Steps[0].StepType != "craft_machine_item" ||
                !outputCount.HasValue || outputCount <= 0 ||
                inventoryDelta != outputCount)
            {
                reasons.Add(
                    "supporting_transition_machine_craft_delta_mismatch");
            }
        }
        else if (stage == MachineSupportIntentStages.PlacementBound)
        {
            location = UniqueParameter(parameters, "target_location");
            x = UniqueIntParameter(parameters, "target_tile_x");
            y = UniqueIntParameter(parameters, "target_tile_y");
            var beforeMatches = MachinesAt(before, location, x, y,
                out var beforeResolved);
            var afterMatches = MachinesAt(after, location, x, y,
                out var afterResolved);
            beforeMachinePresent = beforeResolved
                ? beforeMatches.Length > 0
                : null;
            afterMachinePresent = afterResolved
                ? afterMatches.Length > 0
                : null;
            if (!beforeResolved || !afterResolved ||
                beforeMatches.Length != 0 ||
                afterMatches.Length != 1 ||
                ReadString(afterMatches[0], "qualified_item_id") !=
                    machineQualifiedItemId ||
                MachineCapacityState(afterMatches[0]) != "idle" ||
                inventoryDelta != -1)
            {
                reasons.Add(
                    "supporting_transition_machine_placement_delta_mismatch");
            }
        }
        else
        {
            reasons.Add("supporting_transition_machine_capacity_stage_invalid");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new AcquisitionMachineCapacityTransitionEvidence
        {
            Stage = stage,
            IntentId = intentId,
            MachineQualifiedItemId = machineQualifiedItemId,
            TargetLocationId = location,
            TargetTileX = x,
            TargetTileY = y,
            InventoryQuantityBefore = beforeQuantity,
            InventoryQuantityAfter = afterQuantity,
            ObservedInventoryDelta = inventoryDelta,
            BeforeTargetMachinePresent = beforeMachinePresent,
            AfterTargetMachinePresent = afterMachinePresent,
            Resolved = beforeQuantity.HasValue && afterQuantity.HasValue &&
                (stage == MachineSupportIntentStages.CraftSelected ||
                 beforeMachinePresent.HasValue &&
                 afterMachinePresent.HasValue),
            Verified = blocking.Length == 0,
            BlockingReasons = blocking
        };
    }

    private static JsonElement[] MachinesAt(
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
            !string.IsNullOrWhiteSpace(location) &&
            x.HasValue && y.HasValue;
        return !resolved
            ? Array.Empty<JsonElement>()
            : machines.EnumerateArray().Where(machine =>
                    machine.ValueKind == JsonValueKind.Object &&
                    ReadString(machine, "location_id") == location &&
                    ReadInt(machine, "tile_x") == x &&
                    ReadInt(machine, "tile_y") == y)
                .ToArray();
    }

    private static bool SupportSourceMatches(
        string json,
        string machineQualifiedItemId,
        string intentId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var source = document.RootElement;
            return source.ValueKind == JsonValueKind.Object &&
                !string.IsNullOrWhiteSpace(
                    ReadString(source, "route_occurrence_id")) &&
                !string.IsNullOrWhiteSpace(ReadString(source, "goal_id")) &&
                ReadString(source, "machine_qualified_item_id") ==
                    machineQualifiedItemId &&
                intentId.Contains(
                    ReadString(source, "route_occurrence_id"),
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
