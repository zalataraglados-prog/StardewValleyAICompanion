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
    private static CompiledActionStep[] CompilePaintBuildingRegionStep(SmallModelAction action)
    {
        var identity = ReadParameter(action, "building_identity");
        var region = ReadParameter(action, "paint_region_id");
        var mode = ReadParameter(action, "paint_target_mode");
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(mode))
            return Array.Empty<CompiledActionStep>();
        var target = mode == "default"
            ? "default"
            : ReadParameter(action, "target_hue") + "," + ReadParameter(action, "target_saturation") + "," + ReadParameter(action, "target_lightness");
        return new[]
        {
            Step("paint_building_region", identity + ":paint:" + region,
                "building=" + identity + ";region=" + region + ";target=" + target + ";fresh_snapshot_replan_required=true", 540)
        };
    }

    private static string[] ValidatePaintBuildingRegionPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.change_building_skin" || string.IsNullOrWhiteSpace(ReadParameter(action, "paint_target_mode")))
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("paint_building_region_menu_must_be_clear");
        var location = ReadParameter(action, "building_location_id") ?? string.Empty;
        var type = ReadParameter(action, "building_type") ?? string.Empty;
        var x = ReadIntParameter(action, "building_tile_x");
        var y = ReadIntParameter(action, "building_tile_y");
        var region = ReadParameter(action, "paint_region_id") ?? string.Empty;
        var mode = ReadParameter(action, "paint_target_mode") ?? string.Empty;
        var row = x.HasValue && y.HasValue ? BuildingPaintProjectionRow(snapshot, location, type, x.Value, y.Value, region) : null;
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "appearance_reason")) || !row.HasValue || mode is not ("custom" or "default"))
        {
            reasons.Add("paint_building_region_exact_identity_region_mode_and_reason_required");
            return reasons.ToArray();
        }
        var value = row.Value;
        var targetHue = ReadIntParameter(action, "target_hue");
        var targetSaturation = ReadIntParameter(action, "target_saturation");
        var targetLightness = ReadIntParameter(action, "target_lightness");
        var targetValid = mode == "default"
            ? ReadBool(value, "current_default") == false
            : targetHue.HasValue && targetSaturation.HasValue && targetLightness.HasValue &&
              BuildingPaintSliderValueReachable(value, "hue_mouse_reachable_values", targetHue.Value) &&
              BuildingPaintSliderValueReachable(value, "saturation_mouse_reachable_values", targetSaturation.Value) &&
              BuildingPaintSliderValueReachable(value, "lightness_mouse_reachable_values", targetLightness.Value) &&
              !(ReadBool(value, "current_default") && targetHue == ReadInt(value, "default_displayed_hue") &&
                targetSaturation == ReadInt(value, "default_displayed_saturation") && targetLightness == ReadInt(value, "default_displayed_lightness"));
        var exact = targetValid && ReadString(value, "action_status") == "ready_for_native_building_paint" &&
            ReadParameter(action, "building_identity") == ReadString(value, "building_identity") &&
            ReadParameter(action, "paint_data_key") == ReadString(value, "paint_data_key") &&
            ReadIntParameter(action, "paint_region_count") == ReadInt(value, "paint_region_count") &&
            ReadIntParameter(action, "paint_region_index") == ReadInt(value, "paint_region_index") &&
            ReadParameter(action, "current_paint_default") == ReadBool(value, "current_default").ToString().ToLowerInvariant() &&
            ReadIntParameter(action, "current_hue") == ReadInt(value, "current_hue") &&
            ReadIntParameter(action, "current_saturation") == ReadInt(value, "current_saturation") &&
            ReadIntParameter(action, "current_lightness") == ReadInt(value, "current_lightness") &&
            ReadIntParameter(action, "hue_min") == ReadInt(value, "hue_min") && ReadIntParameter(action, "hue_max") == ReadInt(value, "hue_max") &&
            ReadIntParameter(action, "saturation_min") == ReadInt(value, "saturation_min") && ReadIntParameter(action, "saturation_max") == ReadInt(value, "saturation_max") &&
            ReadIntParameter(action, "lightness_min") == ReadInt(value, "lightness_min") && ReadIntParameter(action, "lightness_max") == ReadInt(value, "lightness_max") &&
            ReadIntParameter(action, "native_slider_logical_width") == ReadInt(value, "native_slider_logical_width") &&
            ReadParameter(action, "location_id") == ReadString(value, "service_location_id") &&
            ReadIntParameter(action, "target_tile_x") == NullableReadInt(value, "service_action_tile_x") &&
            ReadIntParameter(action, "target_tile_y") == NullableReadInt(value, "service_action_tile_y") &&
            ReadParameter(action, "builder_action_raw") == ReadString(value, "service_action_raw") &&
            ReadParameter(action, "native_contract") == ReadString(value, "native_contract");
        if (!exact)
            reasons.Add("paint_building_region_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool BuildingPaintSliderValueReachable(JsonElement row, string property, int target) =>
        row.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array &&
        values.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Number && value.GetInt32() == target);

    private static JsonElement? BuildingPaintProjectionRow(SnapshotEnvelope snapshot, string location, string type, int x, int y, string region)
    {
        var catalog = ReadStateFieldValue(snapshot, "player", "building_paint_catalog");
        if (!catalog.HasValue || !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in rows.EnumerateArray())
            if (ReadString(row, "building_location_id") == location && ReadString(row, "building_type") == type &&
                ReadInt(row, "building_tile_x") == x && ReadInt(row, "building_tile_y") == y && ReadString(row, "paint_region_id") == region)
                return row;
        return null;
    }
}
