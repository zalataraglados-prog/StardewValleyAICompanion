using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string CrabPotRuntimeType = "StardewValley.Objects.CrabPot";
    private const string CrabPotBaitNativeContract =
        "GameLocation.checkAction->CrabPot.performObjectDropInAction(Category=-21,probe:false,owner=current_player)->Farmer.reduceActiveItemByOne";

    private static CompiledActionStep[] CompileLoadCrabPotBaitStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || !x.HasValue || !y.HasValue || string.IsNullOrWhiteSpace(location))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "load_crab_pot_bait",
                location + "(" + x.Value + "," + y.Value + "):slot" + slot.Value + ":" + ReadParameter(action, "qualified_item_id"),
                "current_location.objects[" + x.Value + "," + y.Value + "].crab_pot_bait=" + ReadParameter(action, "qualified_item_id") + ";" +
                    "current_location.objects[" + x.Value + "," + y.Value + "].owner=current_player;" +
                    "player.inventory[" + slot.Value + "].stack_decreases=1",
                45)
        };
    }

    private static string[] ValidateLoadCrabPotBaitPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.load_crab_pot_bait")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var expectedStack = ReadIntParameter(action, "expected_stack_before");
        var baitQuality = ReadIntParameter(action, "bait_quality");
        var ownerBefore = ReadLongParameterExact(action, "expected_container_owner_player_id_before");
        var ownerAfter = ReadLongParameterExact(action, "expected_container_owner_player_id_after");
        var location = ReadParameter(action, "target_location");
        var baitId = ReadParameter(action, "qualified_item_id");
        var expectedContainerBaitId = ReadParameter(action, "expected_container_bait_qualified_item_id");
        var baitRuntimeType = ReadParameter(action, "bait_runtime_type");
        var baitUnitState = ReadParameter(action, "expected_container_bait_unit_state_sha256");
        if (!slot.HasValue || !targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !expectedStack.HasValue || expectedStack.Value < 1 || !baitQuality.HasValue ||
            !ownerBefore.HasValue || !ownerAfter.HasValue || string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(baitId) || string.IsNullOrWhiteSpace(expectedContainerBaitId) ||
            string.IsNullOrWhiteSpace(baitRuntimeType) ||
            string.IsNullOrWhiteSpace(baitUnitState))
        {
            return new[] { "load_crab_pot_bait_typed_projection_required" };
        }

        if (!string.Equals(ReadParameter(action, "target_runtime_type"), CrabPotRuntimeType, StringComparison.Ordinal))
        {
            reasons.Add("load_crab_pot_bait_exact_base_target_required");
        }
        if (!string.Equals(expectedContainerBaitId, baitId, StringComparison.Ordinal))
        {
            reasons.Add("load_crab_pot_bait_expected_identity_mismatch");
        }
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "crab_pot_bait_reason")))
        {
            reasons.Add("load_crab_pot_bait_reason_required");
        }
        if (!string.Equals(ReadParameter(action, "native_contract"), CrabPotBaitNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("load_crab_pot_bait_native_contract_mismatch");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("load_crab_pot_bait_menu_must_be_clear");
        }
        if (!TargetLocationMatchesCurrent(action, snapshot))
        {
            reasons.Add("load_crab_pot_bait_requires_loaded_target_location");
        }
        if (Math.Abs(standX.Value - targetX.Value) + Math.Abs(standY.Value - targetY.Value) != 1 ||
            PlacementCollisionGridBlocks(snapshot, standX.Value, standY.Value))
        {
            reasons.Add("load_crab_pot_bait_adjacent_stand_geometry_invalid");
        }

        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        var target = objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array
            ? objects.Value.EnumerateArray().FirstOrDefault(item =>
                ReadInt(item, "tile_x") == targetX.Value && ReadInt(item, "tile_y") == targetY.Value)
            : default;
        if (target.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(target, "type"), CrabPotRuntimeType, StringComparison.Ordinal) ||
            !string.Equals(ReadString(target, "crab_pot_bait_load_status"), "ready", StringComparison.Ordinal) ||
            ReadBool(target, "crab_pot_needs_bait") != true ||
            ReadBool(target, "crab_pot_ready_for_harvest") == true ||
            !string.IsNullOrEmpty(ReadString(target, "crab_pot_output_qualified_item_id")) ||
            !string.IsNullOrEmpty(ReadString(target, "crab_pot_bait_qualified_item_id")))
        {
            reasons.Add("load_crab_pot_bait_target_not_ready_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (ReadInt64(target, "crab_pot_owner_player_id_before_bait") != ownerBefore.Value ||
            ReadInt64(target, "crab_pot_expected_owner_player_id_after_bait") != ownerAfter.Value)
        {
            reasons.Add("load_crab_pot_bait_owner_projection_drifted");
        }
        if (!string.Equals(ReadString(target, "crab_pot_bait_load_native_contract"), CrabPotBaitNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("load_crab_pot_bait_projection_contract_drifted");
        }

        var inventoryRow = FindCrabPotBaitInventoryRow(target, slot.Value, baitId);
        if (!inventoryRow.HasValue || ReadInt(inventoryRow.Value, "stack") != expectedStack.Value ||
            ReadInt(inventoryRow.Value, "category") != -21 || ReadBool(inventoryRow.Value, "native_probe_accepts") != true ||
            !string.Equals(ReadString(inventoryRow.Value, "runtime_type"), baitRuntimeType, StringComparison.Ordinal) ||
            ReadInt(inventoryRow.Value, "quality") != baitQuality.Value ||
            !string.Equals(ReadString(inventoryRow.Value, "unit_state_sha256"), baitUnitState, StringComparison.Ordinal) ||
            !string.Equals(ReadString(inventoryRow.Value, "expected_container_bait_qualified_item_id"), expectedContainerBaitId, StringComparison.Ordinal) ||
            !string.Equals(ReadString(inventoryRow.Value, "expected_container_bait_runtime_type"), baitRuntimeType, StringComparison.Ordinal) ||
            ReadInt(inventoryRow.Value, "expected_container_bait_quality") != baitQuality.Value ||
            !string.Equals(ReadString(inventoryRow.Value, "expected_container_bait_unit_state_sha256"), baitUnitState, StringComparison.Ordinal) ||
            ReadInt(inventoryRow.Value, "expected_consumed_quantity") != 1)
        {
            reasons.Add("load_crab_pot_bait_inventory_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? FindCrabPotBaitInventoryRow(JsonElement target, int slot, string qualifiedItemId)
    {
        if (!target.TryGetProperty("crab_pot_bait_load_inventory_rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "inventory_slot_index", -1) == slot &&
                string.Equals(ReadString(row, "qualified_item_id"), qualifiedItemId, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }
}
