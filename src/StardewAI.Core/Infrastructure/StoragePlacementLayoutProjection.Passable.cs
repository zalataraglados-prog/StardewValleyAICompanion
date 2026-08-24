using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    public StoragePlacementLayoutResult ValidateExactCurrentMapPassablePlacement(
        SnapshotEnvelope snapshot,
        JsonElement placementLocation,
        int targetX,
        int targetY,
        int standX,
        int standY,
        string reasonPrefix)
    {
        var reasons = new List<string>();
        var currentLocationId = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(ReadString(placementLocation, "location_id"), currentLocationId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(reasonPrefix + "_location_not_current");
        }
        if (!string.Equals(ReadString(placementLocation, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal))
        {
            reasons.Add(reasonPrefix + "_native_legal_tiles_unavailable");
        }

        var collisionGrid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
        if (!collisionGrid.HasValue || collisionGrid.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add(reasonPrefix + "_collision_grid_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        var grid = collisionGrid.Value;
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
        var ignoredAccessGroups = new List<ProtectedAccessGroup>();
        if (!ReadCollisionFacts(grid, width, height, blocked, protectedTargets, ignoredAccessGroups))
        {
            reasons.Add(reasonPrefix + "_collision_grid_incomplete");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var start = new Tile(
            ReadStateFieldInt(snapshot, "player", "tile_x", -1),
            ReadStateFieldInt(snapshot, "player", "tile_y", -1));
        var target = new Tile(targetX, targetY);
        var stand = new Tile(standX, standY);
        if (!InBounds(start, width, height) || blocked.Contains(start))
        {
            reasons.Add(reasonPrefix + "_player_start_not_walkable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var baseline = Search(start, width, height, blocked, extraBlocked: null);
        if (!ReadLegalTiles(placementLocation, width, height).Contains(target) ||
            !baseline.Distances.ContainsKey(target))
        {
            reasons.Add(reasonPrefix + "_target_not_reachable_native_tile");
        }
        if (Math.Abs(stand.X - target.X) + Math.Abs(stand.Y - target.Y) != 1 ||
            !baseline.Distances.ContainsKey(stand))
        {
            reasons.Add(reasonPrefix + "_stand_not_reachable_cardinal_tile");
        }
        if (reasons.Count > 0)
        {
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count);
        }

        return new StoragePlacementLayoutResult
        {
            Status = "available",
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StandTileX = stand.X,
            StandTileY = stand.Y,
            RouteDistanceTiles = baseline.Distances[stand],
            BaselineReachableTileCount = baseline.Distances.Count,
            ReachableTileCountAfterPlacement = baseline.Distances.Count,
            ProtectedAccessGroupCount = 0,
            ProjectionBasis = "native_legal_range+collision_grid_passable_target_bfs",
            BlockingReasons = Array.Empty<string>()
        };
    }
}
