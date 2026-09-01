using System;
using System.Collections.Generic;
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
    private EventCandidate[] BuildingSkinCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var locationId = IntentParameter(intent, "building_location_id");
        var buildingType = IntentParameter(intent, "building_type");
        var tileX = ParseIntentInt(intent, "building_tile_x");
        var tileY = ParseIntentInt(intent, "building_tile_y");
        var targetSkinKey = IntentParameter(intent, "target_skin_key");
        var appearanceReason = IntentParameter(intent, "appearance_reason");
        if (string.IsNullOrWhiteSpace(locationId) || string.IsNullOrWhiteSpace(buildingType) ||
            !tileX.HasValue || !tileY.HasValue || string.IsNullOrWhiteSpace(targetSkinKey) ||
            string.IsNullOrWhiteSpace(appearanceReason))
            return Array.Empty<EventCandidate>();

        var row = BuildingSkinRow(snapshot, locationId, buildingType, tileX.Value, tileY.Value, targetSkinKey);
        if (!row.HasValue)
            return Array.Empty<EventCandidate>();

        var serviceLocation = ReadString(row.Value, "service_location_id");
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, serviceLocation, StringComparison.OrdinalIgnoreCase))
        {
            if (ReadString(row.Value, "action_status") != "route_to_carpenter_service_required")
                return Array.Empty<EventCandidate>();
            return BuildingSkinRouteCandidate(snapshot, row.Value, appearanceReason, currentLocation, serviceLocation);
        }

        var actionX = NullableReadInt(row.Value, "service_action_tile_x");
        var actionY = NullableReadInt(row.Value, "service_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue ? FindBestStandTile(snapshot, actionX.Value, actionY.Value) : null;
        var reasons = new List<string>();
        if (ReadString(row.Value, "action_status") != "ready_for_native_skin_change")
            reasons.Add("building_skin_not_ready:" + ReadString(row.Value, "action_status"));
        if (!actionX.HasValue || !actionY.HasValue || stand is null)
            reasons.Add("building_skin_service_stand_unavailable");

        var parameters = actionX.HasValue && actionY.HasValue && stand is not null
            ? BuildingSkinParameters(row.Value, appearanceReason, actionX.Value, actionY.Value, stand)
            : Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "building-skin:" + locationId + ":" + buildingType + ":" + tileX + "," + tileY + ":" + targetSkinKey,
                Kind = "change_building_skin",
                Available = reasons.Count == 0,
                LocationId = serviceLocation,
                TileX = actionX,
                TileY = actionY,
                DisplayName = ReadString(row.Value, "target_skin_name"),
                Quantity = 1,
                EstimatedTicks = 480,
                EnergyCost = 0,
                AvailabilityClass = "transparent_purpose_bound_native_building_skin",
                ExpectedEffect = "building=" + ReadString(row.Value, "building_identity") +
                    ";target_skin_key=" + targetSkinKey + ";paint_colors_reset_to_default=true;fresh_snapshot_replan_required=true",
                BlockReasons = reasons.ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] BuildingSkinRouteCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        string reason,
        string currentLocation,
        string serviceLocation)
    {
        var plan = FindResolvedRoutePlan(snapshot, currentLocation, serviceLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
        if (plan?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = new[]
        {
            Parameter("continuation.building_location_id", ReadString(row, "building_location_id")),
            Parameter("continuation.building_type", ReadString(row, "building_type")),
            Parameter("continuation.building_tile_x", ReadInt(row, "building_tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.building_tile_y", ReadInt(row, "building_tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.target_skin_key", ReadString(row, "target_skin_key")),
            Parameter("continuation.appearance_reason", reason)
        };
        return new[]
        {
            CloneCandidate(plan.FirstActionCandidate,
                candidateId: "building-skin-route:" + ReadString(row, "building_identity") + ":" + currentLocation,
                expectedEffect: plan.FirstActionCandidate.ExpectedEffect + ";building_skin_service_location=" + serviceLocation,
                parameters: plan.FirstActionCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "building_skin_rolling_route")
        };
    }

    private static SmallModelActionParameter[] BuildingSkinParameters(
        JsonElement row,
        string reason,
        int actionX,
        int actionY,
        CandidateTile stand) => new[]
    {
        Parameter("appearance_reason", reason),
        Parameter("building_identity", ReadString(row, "building_identity")),
        Parameter("building_location_id", ReadString(row, "building_location_id")),
        Parameter("building_type", ReadString(row, "building_type")),
        Parameter("building_tile_x", ReadInt(row, "building_tile_x").ToString(CultureInfo.InvariantCulture)),
        Parameter("building_tile_y", ReadInt(row, "building_tile_y").ToString(CultureInfo.InvariantCulture)),
        Parameter("current_skin_key", ReadString(row, "current_skin_key")),
        Parameter("current_skin_id", ReadString(row, "current_skin_id")),
        Parameter("current_skin_index", ReadInt(row, "current_skin_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("target_skin_key", ReadString(row, "target_skin_key")),
        Parameter("target_skin_id", ReadString(row, "target_skin_id")),
        Parameter("target_skin_index", ReadInt(row, "target_skin_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("available_skin_count", ReadInt(row, "available_skin_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("available_skin_keys_json", row.GetProperty("available_skin_keys").GetRawText()),
        Parameter("shortest_click_direction", ReadString(row, "shortest_click_direction")),
        Parameter("shortest_click_count", ReadInt(row, "shortest_click_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("entry_route", ReadString(row, "entry_route")),
        Parameter("skin_change_resets_all_paint_colors_to_default", "true"),
        Parameter("location_id", ReadString(row, "service_location_id")),
        Parameter("target_tile_x", actionX.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", actionY.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("builder_action_raw", ReadString(row, "service_action_raw")),
        Parameter("native_contract", ReadString(row, "native_contract"))
    };

    private static int? ParseIntentInt(SmallModelActionParameter[] parameters, string name)
    {
        var value = IntentParameter(parameters, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
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
