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
    private const string TreasureTotemNativeContract =
        "Object.performUseAction((O)TreasureTotem)->outdoors_guard->Object.treasureTotem->TreasureTotemsUsed++->rounded_distance_3_ring->placement_occupancy_front_bush_diggable_or_winter_grass_gate->objects.Add((O)590)";

    private static CompiledActionStep[] CompileUseTreasureTotemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var location = ReadParameter(action, "target_location");
        var x = ReadIntParameter(action, "center_tile_x");
        var y = ReadIntParameter(action, "center_tile_y");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(location) || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("use_treasure_totem",
                location + ":" + x.Value + "," + y.Value + ":slot" + slot.Value + ":(O)TreasureTotem",
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                ";treasure_totems_used=" + ReadParameter(action, "treasure_totems_used_after") +
                ";artifact_spots_spawned=" + ReadParameter(action, "expected_spawn_count"), 30)
        };
    }

    private static string[] ValidateUseTreasureTotemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_treasure_totem")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        var centerX = ReadIntParameter(action, "center_tile_x");
        var centerY = ReadIntParameter(action, "center_tile_y");
        var spawnCount = ReadIntParameter(action, "expected_spawn_count");
        if (!slot.HasValue || !before.HasValue || before < 1 || !after.HasValue ||
            !centerX.HasValue || !centerY.HasValue || !spawnCount.HasValue ||
            !string.Equals(ReadParameter(action, "item_id"), "TreasureTotem", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(O)TreasureTotem", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal))
            return new[] { "use_treasure_totem_typed_fields_required" };
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_treasure_totem_menu_must_be_clear");
        if (!TargetLocationMatchesCurrent(action, snapshot))
            reasons.Add("use_treasure_totem_requires_loaded_target_location");

        var context = ReadStateFieldValue(snapshot, "player", "treasure_totem");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("use_treasure_totem_projection_unavailable").Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadParameter(action, "treasure_totem_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_treasure_totem_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_treasure_totem_native_effect_gate_blocked");

        JsonElement? row = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            row = rows.EnumerateArray().FirstOrDefault(value => ReadInt(value, "inventory_slot_index", -1) == slot);
        if (!row.HasValue || !string.Equals(ReadString(row.Value, "item_id"), "TreasureTotem", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "qualified_item_id"), "(O)TreasureTotem", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadBool(row.Value, "temporarily_invisible") == true || after != before - 1 ||
            ReadInt(row.Value, "stack_before", -1) != before || ReadInt(row.Value, "stack_after", -1) != after)
            reasons.Add("use_treasure_totem_inventory_identity_drifted");

        var center = context.Value.GetProperty("center_tile");
        if (centerX != ReadInt(center, "tile_x") || centerY != ReadInt(center, "tile_y") ||
            centerX != ReadStateFieldIntOptional(snapshot, "player", "tile_x") ||
            centerY != ReadStateFieldIntOptional(snapshot, "player", "tile_y"))
            reasons.Add("use_treasure_totem_center_tile_drifted");

        var spawn = context.Value.GetProperty("spawn_projection");
        if (ReadIntParameter(action, "ring_candidate_count") != ReadInt(spawn, "ring_candidate_count") ||
            spawnCount != ReadInt(spawn, "expected_spawn_count") || spawnCount <= 0 ||
            !string.Equals(ReadParameter(action, "expected_spawn_tiles_json"),
                ReadString(spawn, "expected_spawn_tiles_json"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "existing_artifact_spot_count_before") != ReadInt(spawn, "existing_artifact_spot_count_before") ||
            ReadIntParameter(action, "existing_artifact_spot_count_after") != ReadInt(spawn, "existing_artifact_spot_count_after") ||
            ReadInt(spawn, "existing_artifact_spot_count_after") !=
                ReadInt(spawn, "existing_artifact_spot_count_before") + spawnCount)
            reasons.Add("use_treasure_totem_spawn_projection_drifted");
        if (ReadIntParameter(action, "treasure_totems_used_before") != ReadInt(spawn, "treasure_totems_used_before") ||
            ReadIntParameter(action, "treasure_totems_used_after") != ReadInt(spawn, "treasure_totems_used_after") ||
            ReadInt(spawn, "treasure_totems_used_after") != ReadInt(spawn, "treasure_totems_used_before") + 1)
            reasons.Add("use_treasure_totem_counter_projection_drifted");

        var ring = context.Value.GetProperty("ring_contract");
        if (ReadIntParameter(action, "native_ring_scan_radius") != ReadInt(ring, "scan_radius") ||
            ReadIntParameter(action, "native_rounded_radius") != ReadInt(ring, "rounded_radius") ||
            !string.Equals(ReadParameter(action, "artifact_spot_qualified_item_id"),
                ReadString(ring, "artifact_spot_qualified_item_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_initial_sound"), ReadString(ring, "initial_sound"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_contract"), TreasureTotemNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), TreasureTotemNativeContract, StringComparison.Ordinal))
            reasons.Add("use_treasure_totem_native_contract_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
