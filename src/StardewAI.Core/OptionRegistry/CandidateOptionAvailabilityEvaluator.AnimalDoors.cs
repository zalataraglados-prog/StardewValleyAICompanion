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
    private EventCandidate[] OpenAnimalDoorCandidates(
        SnapshotEnvelope snapshot,
        string locationId,
        string buildingType,
        int buildingTileX,
        int buildingTileY)
    {
        var row = AnimalDoorBuildingRow(snapshot, buildingType, buildingTileX, buildingTileY);
        if (!row.HasValue ||
            !row.Value.TryGetProperty("animal_door", out var door) ||
            door.ValueKind != JsonValueKind.Object ||
            ReadBool(door, "present") != true)
        {
            return Array.Empty<EventCandidate>();
        }
        if (ReadBool(door, "is_open") == true)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, locationId, StringComparison.OrdinalIgnoreCase))
        {
            var route = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                locationId,
                RouteConnectorCandidates(snapshot, int.MaxValue))?.FirstActionCandidate;
            return route is null
                ? Array.Empty<EventCandidate>()
                : new[]
                {
                    CloneCandidate(
                        route,
                        candidateId: "animal-door-route:" + buildingType + ":" + buildingTileX + "," + buildingTileY + ":" + route.CandidateId,
                        expectedEffect: route.ExpectedEffect + ";animal_door_target=" + buildingType + ":" + buildingTileX + "," + buildingTileY + ";fresh_snapshot_replan_required=true",
                        availabilityClass: "native_animal_door_route")
                };
        }

        var targetX = NullableReadInt(door, "target_tile_x");
        var targetY = NullableReadInt(door, "target_tile_y");
        var stand = targetX.HasValue && targetY.HasValue
            ? FindBestStandTile(snapshot, targetX.Value, targetY.Value)
            : null;
        var safe = ReadStateFieldValue(snapshot, "player", "safe_item_context");
        var safeSlot = safe.HasValue ? NullableReadInt(safe.Value, "safe_slot_index") : null;
        var distance = stand?.Distance ?? int.MaxValue;
        var reasons = new List<string>();
        if (!string.Equals(ReadString(door, "interaction_status"), "ready_native_right_click_toggle", StringComparison.Ordinal))
            reasons.Add("animal_door_native_interaction_not_ready");
        if (!targetX.HasValue || !targetY.HasValue || stand is null || distance == int.MaxValue)
            reasons.Add("animal_door_target_or_stand_unavailable");
        if (!safeSlot.HasValue)
            reasons.Add("animal_door_safe_toolbar_slot_unavailable");

        return new[]
        {
            new EventCandidate
            {
                CandidateId = "animal-door-open:" + buildingType + ":" + buildingTileX + "," + buildingTileY,
                Kind = "toggle_animal_door",
                Available = reasons.Count == 0,
                LocationId = locationId,
                TileX = targetX,
                TileY = targetY,
                DisplayName = buildingType + " animal door",
                Quantity = 1,
                EstimatedTicks = distance == int.MaxValue ? -1 : distance * 12 + 30,
                EnergyCost = 0,
                AvailabilityClass = "transparent_exact_native_animal_door_open",
                ExpectedEffect = "farm.buildings[" + buildingType + ":" + buildingTileX + "," + buildingTileY + "].animal_door.is_open=false->true;fresh_snapshot_replan_required=true",
                BlockReasons = reasons.ToArray(),
                Parameters = targetX.HasValue && targetY.HasValue && stand is not null && safeSlot.HasValue
                    ? new[]
                    {
                        Parameter("interaction_kind", "animal_door"),
                        Parameter("expected_action_type", "AnimalDoor"),
                        Parameter("building_type", buildingType),
                        Parameter("building_tile_x", buildingTileX.ToString(CultureInfo.InvariantCulture)),
                        Parameter("building_tile_y", buildingTileY.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_x", targetX.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("target_tile_y", targetY.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
                        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
                        Parameter("route_distance_tiles", distance.ToString(CultureInfo.InvariantCulture)),
                        Parameter("safe_slot_index", safeSlot.Value.ToString(CultureInfo.InvariantCulture)),
                        Parameter("expected_animal_door_open_before", "false"),
                        Parameter("expected_animal_door_open_after", "true"),
                        Parameter("native_contract", ReadString(door, "native_contract"))
                    }
                    : Array.Empty<SmallModelActionParameter>()
            }
        };
    }

    private static JsonElement? AnimalDoorBuildingRow(
        SnapshotEnvelope snapshot,
        string buildingType,
        int buildingTileX,
        int buildingTileY)
    {
        var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
        if (!buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var row in buildings.Value.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "type"), buildingType, StringComparison.Ordinal) &&
                ReadInt(row, "tile_x") == buildingTileX &&
                ReadInt(row, "tile_y") == buildingTileY)
            {
                return row;
            }
        }
        return null;
    }
}
