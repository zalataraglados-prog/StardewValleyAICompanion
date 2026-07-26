using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileRemoveMachineStep(
            SmallModelAction action)
        {
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var locationId = ReadParameter(action, "location_id");
            var qualifiedItemId =
                ReadParameter(action, "qualified_item_id");
            var relocationIntentId =
                ReadParameter(action, "relocation_intent_id");
            if (!targetX.HasValue ||
                !targetY.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                string.IsNullOrWhiteSpace(relocationIntentId))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "remove_machine",
                    locationId + "(" + targetX.Value + "," +
                    targetY.Value + "):" + qualifiedItemId +
                    ":intent=" + relocationIntentId,
                    "farm.machines[" + locationId + ":" +
                    targetX.Value + "," + targetY.Value +
                    "]=missing;machine_recovery[" +
                    qualifiedItemId +
                    "]=debris_or_native_auto_collected_inventory",
                    45)
            };
        }

        private static string[] ValidateRemoveMachinePlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            StardewAI.Contracts.Strategy.StrategyCommitmentLedger?
                commitmentLedger)
        {
            if (action.OptionId != "executor.remove_machine")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            var standX = ReadIntParameter(action, "stand_tile_x");
            var standY = ReadIntParameter(action, "stand_tile_y");
            var toolSlot = ReadIntParameter(action, "tool_slot_index");
            var locationId = ReadParameter(action, "location_id");
            var qualifiedItemId =
                ReadParameter(action, "qualified_item_id");
            var relocationIntentId =
                ReadParameter(action, "relocation_intent_id");
            if (!targetX.HasValue ||
                !targetY.HasValue ||
                !standX.HasValue ||
                !standY.HasValue ||
                !toolSlot.HasValue ||
                string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                string.IsNullOrWhiteSpace(relocationIntentId))
            {
                reasons.Add(
                    "remove_machine_typed_target_and_intent_fields_required");
                return reasons.ToArray();
            }
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("remove_machine_menu_must_be_clear");
            }
            if (!string.Equals(
                    ReadStateFieldString(
                        snapshot,
                        "player",
                        "location_id"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ReadParameter(action, "target_location"),
                    locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(
                    "remove_machine_requires_loaded_target_location");
            }
            if (Math.Abs(standX.Value - targetX.Value) +
                    Math.Abs(standY.Value - targetY.Value) != 1 ||
                PlacementCollisionGridBlocks(
                    snapshot,
                    standX.Value,
                    standY.Value))
            {
                reasons.Add(
                    "remove_machine_adjacent_stand_geometry_invalid");
            }

            var machines = ReadStateFieldValue(
                snapshot,
                "farm",
                "machines");
            var machine = machines.HasValue &&
                machines.Value.ValueKind == JsonValueKind.Array
                    ? machines.Value.EnumerateArray()
                        .FirstOrDefault(row =>
                            row.ValueKind == JsonValueKind.Object &&
                            ReadInt(row, "tile_x") == targetX.Value &&
                            ReadInt(row, "tile_y") == targetY.Value &&
                            string.Equals(
                                ReadString(row, "location_id"),
                                locationId,
                                StringComparison.OrdinalIgnoreCase))
                    : default;
            if (machine.ValueKind != JsonValueKind.Object)
            {
                reasons.Add(
                    "remove_machine_target_not_found_in_transparent_state");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }
            if (!string.Equals(
                    ReadString(machine, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("remove_machine_identity_drifted");
            }
            if (ReadBool(machine, "removal_safe_now") != true ||
                !string.Equals(
                    ReadString(machine, "removal_status"),
                    "safe_idle_native_pickaxe",
                    StringComparison.Ordinal))
            {
                reasons.Add("remove_machine_safety_projection_blocked");
            }
            if (ReadInt(machine, "removal_tool_slot_index", -1) !=
                    toolSlot.Value ||
                !string.Equals(
                    ReadString(
                        machine,
                        "removal_tool_qualified_item_id"),
                    ReadParameter(
                        action,
                        "tool_qualified_item_id"),
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("remove_machine_tool_projection_drifted");
            }
            if (!string.Equals(
                    ReadString(machine, "removal_native_contract"),
                    ReadParameter(action, "native_contract"),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(
                        machine,
                        "removal_projection_fingerprint"),
                    ReadParameter(
                        action,
                        "machine_removal_projection_fingerprint"),
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "remove_machine_native_projection_drifted");
            }

            var relocationTargetLocation = ReadParameter(
                action,
                "relocation_target_location_id");
            var relocationTargetX = ReadIntParameter(
                action,
                "relocation_target_tile_x");
            var relocationTargetY = ReadIntParameter(
                action,
                "relocation_target_tile_y");
            if (!string.IsNullOrWhiteSpace(
                    relocationTargetLocation) ||
                relocationTargetX.HasValue ||
                relocationTargetY.HasValue)
            {
                var intent =
                    commitmentLedger?.MachineRelocationIntents
                        .FirstOrDefault(row =>
                            string.Equals(
                                row.Status,
                                StardewAI.Contracts.Strategy
                                    .StrategyCommitmentStatuses.Active,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                row.IntentId,
                                relocationIntentId,
                                StringComparison.Ordinal));
                if (intent is null ||
                    !string.Equals(
                        intent.SourceLocationId,
                        locationId,
                        StringComparison.OrdinalIgnoreCase) ||
                    intent.SourceTileX != targetX.Value ||
                    intent.SourceTileY != targetY.Value ||
                    !string.Equals(
                        intent.QualifiedItemId,
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        intent.TargetLocationId,
                        relocationTargetLocation,
                        StringComparison.OrdinalIgnoreCase) ||
                    intent.TargetTileX != relocationTargetX ||
                    intent.TargetTileY != relocationTargetY)
                {
                    reasons.Add(
                        "remove_machine_relocation_intent_drifted");
                }
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
