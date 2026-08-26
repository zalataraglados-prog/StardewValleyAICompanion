using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    public StoragePlacementLayoutResult ValidateExactCurrentMapPassableRectanglePlacement(
        SnapshotEnvelope snapshot,
        JsonElement placementLocation,
        int targetX,
        int targetY,
        int standX,
        int standY,
        int rectangleX,
        int rectangleY,
        int rectangleWidth,
        int rectangleHeight,
        string reasonPrefix)
    {
        var reasons = new List<string>();
        var currentLocationId = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(ReadString(placementLocation, "location_id"), currentLocationId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(ReadString(placementLocation, "placement_probe_status"), "native_legal_directional_stands_available", StringComparison.Ordinal))
        {
            reasons.Add(reasonPrefix + "_location_or_native_projection_invalid");
        }

        var collision = ReadStateFieldValue(snapshot, "locations", "collision_grid");
        if (!collision.HasValue || collision.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add(reasonPrefix + "_collision_grid_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        var grid = collision.Value;
        var width = ReadInt(grid, "width");
        var height = ReadInt(grid, "height");
        if (width <= 0 || height <= 0 ||
            !string.Equals(ReadString(grid, "location_id"), currentLocationId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(reasonPrefix + "_collision_grid_identity_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var blocked = new HashSet<Tile>();
        var protectedTargets = new HashSet<Tile>();
        var protectedGroups = new List<ProtectedAccessGroup>();
        if (!ReadCollisionFacts(grid, width, height, blocked, protectedTargets, protectedGroups))
        {
            reasons.Add(reasonPrefix + "_collision_grid_incomplete");
        }
        if (rectangleWidth <= 0 || rectangleHeight <= 0)
        {
            reasons.Add(reasonPrefix + "_footprint_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var start = new Tile(
            ReadStateFieldInt(snapshot, "player", "tile_x", -1),
            ReadStateFieldInt(snapshot, "player", "tile_y", -1));
        var target = new Tile(targetX, targetY);
        var stand = new Tile(standX, standY);
        var footprint = Enumerable.Range(0, rectangleWidth)
            .SelectMany(dx => Enumerable.Range(0, rectangleHeight)
                .Select(dy => new Tile(rectangleX + dx, rectangleY + dy)))
            .ToArray();
        if (!InBounds(start, width, height) || blocked.Contains(start))
        {
            reasons.Add(reasonPrefix + "_player_start_not_walkable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        if (footprint.Any(tile => !InBounds(tile, width, height)) || footprint.Any(protectedTargets.Contains))
        {
            reasons.Add(reasonPrefix + "_footprint_out_of_bounds_or_covers_protected_endpoint");
        }
        if (Math.Abs(stand.X - target.X) + Math.Abs(stand.Y - target.Y) != 1)
        {
            reasons.Add(reasonPrefix + "_stand_not_cardinal_to_direction_probe");
        }

        var baseline = Search(start, width, height, blocked, extraBlocked: null);
        if (!baseline.Distances.ContainsKey(stand))
        {
            reasons.Add(reasonPrefix + "_stand_not_reachable");
        }
        if (reasons.Count > 0)
        {
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, protectedGroups.Count);
        }

        return new StoragePlacementLayoutResult
        {
            Status = "available",
            TargetTileX = targetX,
            TargetTileY = targetY,
            StandTileX = standX,
            StandTileY = standY,
            RouteDistanceTiles = baseline.Distances[stand],
            BaselineReachableTileCount = baseline.Distances.Count,
            ReachableTileCountAfterPlacement = baseline.Distances.Count,
            ProtectedAccessGroupCount = protectedGroups.Count,
            ProjectionBasis = "native_directional_3x2_area_clear+collision_grid_passable_rectangular_footprint_bfs+protected_endpoint_exclusion",
            BlockingReasons = Array.Empty<string>()
        };
    }
}
