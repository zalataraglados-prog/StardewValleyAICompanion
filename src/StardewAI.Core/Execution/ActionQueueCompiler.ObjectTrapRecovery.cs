using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileObjectTrapRecoverySteps(
        SmallModelAction action,
        SnapshotEnvelope snapshot) =>
        CompileRemoveMachineStep(BuildObjectTrapRecoveryRemovalAction(action, snapshot));

    private static SmallModelActionParameter[] BuildObjectTrapRecoveryParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot) =>
        BuildObjectTrapRecoveryRemovalAction(action, snapshot).Parameters;

    private static string[] ValidateObjectTrapRecoveryPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!string.Equals(action.OptionId, "recovery.escape_object_trap", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var context = ReadStateFieldValue(snapshot, "player", "object_trap_recovery");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            ReadBool(context.Value, "trapped_by_four_non_passable_objects") != true)
        {
            reasons.Add("object_trap_four_cardinal_non_passable_objects_not_observed");
            return reasons.ToArray();
        }
        if (ReadBool(context.Value, "active_menu_clear") != true ||
            ReadBool(context.Value, "active_object_clear") != true ||
            ReadBool(context.Value, "player_not_riding_horse") != true)
        {
            reasons.Add("object_trap_interaction_state_unsafe");
        }

        var effective = BuildObjectTrapRecoveryRemovalAction(action, snapshot);
        if (string.IsNullOrWhiteSpace(ReadParameter(effective, "qualified_item_id")))
        {
            reasons.Add("object_trap_no_selected_recoverable_adjacent_machine");
            return reasons.ToArray();
        }
        reasons.AddRange(ValidateRemoveMachinePlan(effective, snapshot, commitmentLedger: null));
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BuildObjectTrapRecoveryRemovalAction(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in action.Parameters)
        {
            parameters[parameter.Name] = parameter.Value;
        }
        var requestedTargetX = ReadIntParameter(action, "target_tile_x");
        var requestedTargetY = ReadIntParameter(action, "target_tile_y");
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var standX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var standY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var machine = requestedTargetX.HasValue || requestedTargetY.HasValue
            ? FindMachine(snapshot, locationId, requestedTargetX, requestedTargetY)
            : FindFirstSafeTrapMachine(snapshot, locationId);
        var targetX = machine.ValueKind == JsonValueKind.Object
            ? ReadInt(machine, "tile_x")
            : requestedTargetX;
        var targetY = machine.ValueKind == JsonValueKind.Object
            ? ReadInt(machine, "tile_y")
            : requestedTargetY;
        Set("execution_option_id", "executor.remove_machine");
        Set("target_location", locationId);
        Set("location_id", locationId);
        Set("target_tile_x", targetX?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        Set("target_tile_y", targetY?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        Set("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture));
        Set("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture));
        Set("relocation_intent_id", targetX.HasValue && targetY.HasValue
            ? "object_trap_recovery:" + locationId + ":" + standX + "," + standY + "->" + targetX + "," + targetY
            : string.Empty);
        if (machine.ValueKind == JsonValueKind.Object)
        {
            Set("qualified_item_id", ReadString(machine, "qualified_item_id"));
            Set("target_runtime_type", ReadString(machine, "runtime_type"));
            Set(
                "tool_slot_index",
                ReadInt(machine, "removal_tool_slot_index", -1)
                    .ToString(CultureInfo.InvariantCulture));
            Set("tool_qualified_item_id", ReadString(machine, "removal_tool_qualified_item_id"));
            Set("native_contract", ReadString(machine, "removal_native_contract"));
            Set("machine_removal_projection_fingerprint", ReadString(machine, "removal_projection_fingerprint"));
        }

        return new SmallModelAction
        {
            ActionId = action.ActionId,
            OptionId = "executor.remove_machine",
            Rationale = action.Rationale,
            Parameters = parameters.Select(pair => Parameter(pair.Key, pair.Value)).ToArray()
        };

        void Set(string name, string value) => parameters[name] = value;
    }

    private static JsonElement FindMachine(
        SnapshotEnvelope snapshot,
        string locationId,
        int? targetX,
        int? targetY)
    {
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!targetX.HasValue || !targetY.HasValue || !machines.HasValue ||
            machines.Value.ValueKind != JsonValueKind.Array)
        {
            return default;
        }
        return machines.Value.EnumerateArray().FirstOrDefault(row =>
            row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == targetX.Value &&
            ReadInt(row, "tile_y") == targetY.Value &&
            string.Equals(ReadString(row, "location_id"), locationId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonElement FindFirstSafeTrapMachine(
        SnapshotEnvelope snapshot,
        string locationId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "object_trap_recovery");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("adjacent_objects", out var adjacent) ||
            adjacent.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        foreach (var row in adjacent.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .OrderBy(row => ReadInt(row, "direction_from_player")))
        {
            var machine = FindMachine(
                snapshot,
                locationId,
                ReadInt(row, "tile_x"),
                ReadInt(row, "tile_y"));
            if (machine.ValueKind == JsonValueKind.Object &&
                ReadBool(machine, "removal_safe_now") == true)
            {
                return machine;
            }
        }
        return default;
    }
}
