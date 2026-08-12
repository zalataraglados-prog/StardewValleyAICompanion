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
    private static CompiledActionStep[] CompileChangeBuildingSkinStep(SmallModelAction action)
    {
        var identity = ReadParameter(action, "building_identity");
        var target = ReadParameter(action, "target_skin_key");
        return string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(target)
            ? Array.Empty<CompiledActionStep>()
            : new[]
            {
                Step("change_building_skin", identity + ":skin:" + target,
                    "building=" + identity + ";skin=" + target + ";paint_colors_default=true;fresh_snapshot_replan_required=true", 480)
            };
    }

    private static string[] ValidateChangeBuildingSkinPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.change_building_skin")
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("change_building_skin_menu_must_be_clear");
        var location = ReadParameter(action, "building_location_id") ?? string.Empty;
        var type = ReadParameter(action, "building_type") ?? string.Empty;
        var x = ReadIntParameter(action, "building_tile_x");
        var y = ReadIntParameter(action, "building_tile_y");
        var target = ReadParameter(action, "target_skin_key") ?? string.Empty;
        var row = x.HasValue && y.HasValue ? BuildingSkinRow(snapshot, location, type, x.Value, y.Value, target) : null;
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "appearance_reason")) || !row.HasValue)
        {
            reasons.Add("change_building_skin_exact_identity_and_reason_required");
            return reasons.ToArray();
        }
        var value = row.Value;
        var exact = ReadString(value, "action_status") == "ready_for_native_skin_change" &&
            ReadParameter(action, "building_identity") == ReadString(value, "building_identity") &&
            ReadParameter(action, "current_skin_key") == ReadString(value, "current_skin_key") &&
            ReadParameter(action, "current_skin_id") == ReadString(value, "current_skin_id") &&
            ReadIntParameter(action, "current_skin_index") == ReadInt(value, "current_skin_index") &&
            ReadParameter(action, "target_skin_id") == ReadString(value, "target_skin_id") &&
            ReadIntParameter(action, "target_skin_index") == ReadInt(value, "target_skin_index") &&
            ReadIntParameter(action, "available_skin_count") == ReadInt(value, "available_skin_count") &&
            ReadParameter(action, "available_skin_keys_json") == value.GetProperty("available_skin_keys").GetRawText() &&
            ReadParameter(action, "shortest_click_direction") == ReadString(value, "shortest_click_direction") &&
            ReadIntParameter(action, "shortest_click_count") == ReadInt(value, "shortest_click_count") &&
            ReadParameter(action, "entry_route") == ReadString(value, "entry_route") &&
            ReadParameter(action, "skin_change_resets_all_paint_colors_to_default") == "true" &&
            ReadParameter(action, "location_id") == ReadString(value, "service_location_id") &&
            ReadIntParameter(action, "target_tile_x") == NullableReadInt(value, "service_action_tile_x") &&
            ReadIntParameter(action, "target_tile_y") == NullableReadInt(value, "service_action_tile_y") &&
            ReadParameter(action, "builder_action_raw") == ReadString(value, "service_action_raw") &&
            ReadParameter(action, "native_contract") == ReadString(value, "native_contract");
        if (!exact)
            reasons.Add("change_building_skin_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? BuildingSkinRow(
        SnapshotEnvelope snapshot,
        string locationId,
        string buildingType,
        int tileX,
        int tileY,
        string targetSkinKey)
    {
        var catalog = ReadStateFieldValue(snapshot, "player", "building_skin_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Object ||
            !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "building_location_id") == locationId && ReadString(row, "building_type") == buildingType &&
                ReadInt(row, "building_tile_x") == tileX && ReadInt(row, "building_tile_y") == tileY &&
                ReadString(row, "target_skin_key") == targetSkinKey)
                return row;
        }
        return null;
    }
}
