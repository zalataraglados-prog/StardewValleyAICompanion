using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] BuildingPaintCandidates(SnapshotEnvelope snapshot, SmallModelActionParameter[] intent)
    {
        var location = IntentParameter(intent, "building_location_id");
        var type = IntentParameter(intent, "building_type");
        var x = ParseIntentInt(intent, "building_tile_x");
        var y = ParseIntentInt(intent, "building_tile_y");
        var regionId = IntentParameter(intent, "paint_region_id");
        var mode = IntentParameter(intent, "paint_target_mode");
        var reason = IntentParameter(intent, "appearance_reason");
        var hue = ParseIntentInt(intent, "target_hue");
        var saturation = ParseIntentInt(intent, "target_saturation");
        var lightness = ParseIntentInt(intent, "target_lightness");
        if (string.IsNullOrWhiteSpace(location) || string.IsNullOrWhiteSpace(type) || !x.HasValue || !y.HasValue ||
            string.IsNullOrWhiteSpace(regionId) || mode is not ("custom" or "default") || string.IsNullOrWhiteSpace(reason))
            return Array.Empty<EventCandidate>();
        var row = BuildingPaintRow(snapshot, location, type, x.Value, y.Value, regionId);
        if (!row.HasValue || !PaintTargetValid(row.Value, mode, hue, saturation, lightness))
            return Array.Empty<EventCandidate>();

        var serviceLocation = ReadString(row.Value, "service_location_id");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetSummary = mode == "default" ? "default" : hue + "," + saturation + "," + lightness;
        var continuation = new[]
        {
            Parameter("continuation.building_location_id", location), Parameter("continuation.building_type", type),
            Parameter("continuation.building_tile_x", x.Value.ToString(CultureInfo.InvariantCulture)), Parameter("continuation.building_tile_y", y.Value.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.paint_region_id", regionId), Parameter("continuation.paint_target_mode", mode),
            Parameter("continuation.target_hue", hue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("continuation.target_saturation", saturation?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("continuation.target_lightness", lightness?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Parameter("continuation.appearance_reason", reason)
        };
        if (!string.Equals(currentLocation, serviceLocation, StringComparison.OrdinalIgnoreCase))
        {
            if (ReadString(row.Value, "action_status") != "route_to_carpenter_service_required")
                return Array.Empty<EventCandidate>();
            var plan = FindResolvedRoutePlan(snapshot, currentLocation, serviceLocation,
                RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
            return plan?.FirstConnectorCandidate is null ? Array.Empty<EventCandidate>() : new[]
            {
                CloneCandidate(plan.FirstConnectorCandidate,
                    candidateId: "building-paint-route:" + ReadString(row.Value, "building_identity") + ":" + regionId + ":" + currentLocation,
                    expectedEffect: plan.FirstConnectorCandidate.ExpectedEffect + ";building_paint_service_location=" + serviceLocation,
                    parameters: plan.FirstConnectorCandidate.Parameters.Concat(continuation).ToArray(), availabilityClass: "building_paint_rolling_route")
            };
        }

        var actionX = NullableReadInt(row.Value, "service_action_tile_x");
        var actionY = NullableReadInt(row.Value, "service_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue ? FindBestStandTile(snapshot, actionX.Value, actionY.Value) : null;
        if (ReadString(row.Value, "action_status") != "ready_for_native_building_paint" || !actionX.HasValue || !actionY.HasValue || stand is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "building-paint:" + ReadString(row.Value, "building_identity") + ":" + regionId + ":" + targetSummary,
                Kind = "paint_building_region", Available = true, LocationId = serviceLocation, TileX = actionX, TileY = actionY,
                DisplayName = ReadString(row.Value, "paint_region_display_name"), Quantity = 1, EstimatedTicks = 540, EnergyCost = 0,
                AvailabilityClass = "transparent_purpose_bound_native_building_paint",
                ExpectedEffect = "building=" + ReadString(row.Value, "building_identity") + ";region=" + regionId + ";target=" + targetSummary,
                Parameters = BuildingPaintParameters(row.Value, mode, reason, hue, saturation, lightness, actionX.Value, actionY.Value, stand)
            }
        };
    }

    private static bool PaintTargetValid(JsonElement row, string mode, int? hue, int? saturation, int? lightness)
    {
        if (mode == "default")
            return ReadBool(row, "current_default") == false;
        if (!hue.HasValue || !saturation.HasValue || !lightness.HasValue ||
            !SliderValueIsMouseReachable(row, "hue_mouse_reachable_values", hue.Value) ||
            !SliderValueIsMouseReachable(row, "saturation_mouse_reachable_values", saturation.Value) ||
            !SliderValueIsMouseReachable(row, "lightness_mouse_reachable_values", lightness.Value) ||
            ReadBool(row, "current_default") == true && hue == ReadInt(row, "default_displayed_hue") &&
            saturation == ReadInt(row, "default_displayed_saturation") && lightness == ReadInt(row, "default_displayed_lightness"))
            return false;
        return ReadBool(row, "current_default") == true || hue != ReadInt(row, "current_hue") ||
            saturation != ReadInt(row, "current_saturation") || lightness != ReadInt(row, "current_lightness");
    }

    private static bool SliderValueIsMouseReachable(JsonElement row, string property, int target)
    {
        return row.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array &&
            values.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) && parsed == target);
    }

    private static SmallModelActionParameter[] BuildingPaintParameters(JsonElement row, string mode, string reason, int? hue, int? saturation, int? lightness, int actionX, int actionY, CandidateTile stand) => new[]
    {
        Parameter("appearance_reason", reason), Parameter("building_identity", ReadString(row, "building_identity")),
        Parameter("building_location_id", ReadString(row, "building_location_id")), Parameter("building_type", ReadString(row, "building_type")),
        Parameter("building_tile_x", ReadInt(row, "building_tile_x").ToString(CultureInfo.InvariantCulture)), Parameter("building_tile_y", ReadInt(row, "building_tile_y").ToString(CultureInfo.InvariantCulture)),
        Parameter("paint_data_key", ReadString(row, "paint_data_key")), Parameter("paint_region_count", ReadInt(row, "paint_region_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("paint_region_index", ReadInt(row, "paint_region_index").ToString(CultureInfo.InvariantCulture)), Parameter("paint_region_id", ReadString(row, "paint_region_id")),
        Parameter("current_paint_default", (ReadBool(row, "current_default") == true).ToString().ToLowerInvariant()), Parameter("current_hue", ReadInt(row, "current_hue").ToString(CultureInfo.InvariantCulture)),
        Parameter("current_saturation", ReadInt(row, "current_saturation").ToString(CultureInfo.InvariantCulture)), Parameter("current_lightness", ReadInt(row, "current_lightness").ToString(CultureInfo.InvariantCulture)),
        Parameter("hue_min", ReadInt(row, "hue_min").ToString(CultureInfo.InvariantCulture)), Parameter("hue_max", ReadInt(row, "hue_max").ToString(CultureInfo.InvariantCulture)),
        Parameter("saturation_min", ReadInt(row, "saturation_min").ToString(CultureInfo.InvariantCulture)), Parameter("saturation_max", ReadInt(row, "saturation_max").ToString(CultureInfo.InvariantCulture)),
        Parameter("lightness_min", ReadInt(row, "lightness_min").ToString(CultureInfo.InvariantCulture)), Parameter("lightness_max", ReadInt(row, "lightness_max").ToString(CultureInfo.InvariantCulture)),
        Parameter("native_slider_logical_width", ReadInt(row, "native_slider_logical_width").ToString(CultureInfo.InvariantCulture)),
        Parameter("paint_target_mode", mode), Parameter("target_hue", hue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
        Parameter("target_saturation", saturation?.ToString(CultureInfo.InvariantCulture) ?? string.Empty), Parameter("target_lightness", lightness?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
        Parameter("location_id", ReadString(row, "service_location_id")), Parameter("target_tile_x", actionX.ToString(CultureInfo.InvariantCulture)), Parameter("target_tile_y", actionY.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)), Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("builder_action_raw", ReadString(row, "service_action_raw")), Parameter("native_contract", ReadString(row, "native_contract"))
    };

    private static JsonElement? BuildingPaintRow(SnapshotEnvelope snapshot, string location, string type, int x, int y, string regionId)
    {
        var catalog = ReadStateFieldValue(snapshot, "player", "building_paint_catalog");
        if (!catalog.HasValue || !catalog.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in rows.EnumerateArray())
            if (ReadString(row, "building_location_id") == location && ReadString(row, "building_type") == type && ReadInt(row, "building_tile_x") == x &&
                ReadInt(row, "building_tile_y") == y && ReadString(row, "paint_region_id") == regionId)
                return row;
        return null;
    }
}
