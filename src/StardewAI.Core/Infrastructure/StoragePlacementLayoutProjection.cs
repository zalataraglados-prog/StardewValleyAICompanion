using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    public StoragePlacementLayoutResult SelectCurrentMapTile(
        SnapshotEnvelope snapshot,
        JsonElement placementLocation)
    {
        var reasons = new List<string>();
        var currentLocationId = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        if (!string.Equals(
                ReadString(placementLocation, "location_id"),
                currentLocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(
                "storage_placement_location_not_current");
        }
        if (!string.Equals(
                ReadString(
                    placementLocation,
                    "placement_probe_status"),
                "native_legal_tiles_available",
                StringComparison.Ordinal))
        {
            reasons.Add(
                "storage_placement_native_legal_tiles_unavailable");
        }

        var collisionGrid = ReadStateFieldValue(
            snapshot,
            "locations",
            "collision_grid");
        if (!collisionGrid.HasValue ||
            collisionGrid.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add(
                "storage_placement_collision_grid_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        var grid = collisionGrid.Value;
        var width = ReadInt(grid, "width");
        var height = ReadInt(grid, "height");
        if (width <= 0 ||
            height <= 0 ||
            !string.Equals(
                ReadString(grid, "location_id"),
                currentLocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(
                "storage_placement_collision_grid_identity_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var blocked = new HashSet<Tile>();
        var protectedTargets = new HashSet<Tile>();
        var protectedAccessGroups =
            new List<ProtectedAccessGroup>();
        if (!ReadCollisionFacts(
                grid,
                width,
                height,
                blocked,
                protectedTargets,
                protectedAccessGroups))
        {
            reasons.Add(
                "storage_placement_collision_grid_incomplete");
        }
        if (!ReadExistingStorageAccessGroups(
                snapshot,
                currentLocationId,
                width,
                height,
                blocked,
                protectedAccessGroups))
        {
            reasons.Add(
                "storage_placement_existing_access_projection_unavailable");
        }
        if (reasons.Count > 0)
        {
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var start = new Tile(
            ReadStateFieldInt(snapshot, "player", "tile_x", -1),
            ReadStateFieldInt(snapshot, "player", "tile_y", -1));
        if (!InBounds(start, width, height) ||
            blocked.Contains(start))
        {
            reasons.Add(
                "storage_placement_player_start_not_walkable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var baseline = Search(
            start,
            width,
            height,
            blocked,
            extraBlocked: null);
        if (baseline.Distances.Count == 0)
        {
            reasons.Add(
                "storage_placement_reachable_domain_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        foreach (var group in protectedAccessGroups)
        {
            group.BaselineReachable =
                group.CandidateStandTiles.Any(
                    baseline.Distances.ContainsKey);
        }

        var legalTiles = ReadLegalTiles(
                placementLocation,
                width,
                height)
            .Where(tile =>
                baseline.Distances.ContainsKey(tile))
            .Where(tile =>
                !protectedTargets.Contains(tile))
            .OrderBy(tile =>
                baseline.Distances[tile])
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArray();
        if (legalTiles.Length == 0)
        {
            reasons.Add(
                "storage_placement_no_reachable_unprotected_native_tile");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        foreach (var target in legalTiles)
        {
            var after = Search(
                start,
                width,
                height,
                blocked,
                target);
            if (after.Distances.Count !=
                baseline.Distances.Count - 1)
            {
                continue;
            }
            if (protectedAccessGroups.Any(group =>
                    group.BaselineReachable &&
                    !group.CandidateStandTiles.Any(
                        after.Distances.ContainsKey)))
            {
                continue;
            }

            var stands = CardinalNeighbors(target)
                .Where(after.Distances.ContainsKey)
                .OrderBy(tile => after.Distances[tile])
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X)
                .ToArray();
            if (stands.Length == 0)
            {
                continue;
            }
            var stand = stands[0];

            return new StoragePlacementLayoutResult
            {
                Status = "available",
                TargetTileX = target.X,
                TargetTileY = target.Y,
                StandTileX = stand.X,
                StandTileY = stand.Y,
                RouteDistanceTiles = after.Distances[stand],
                BaselineReachableTileCount =
                    baseline.Distances.Count,
                ReachableTileCountAfterPlacement =
                    after.Distances.Count,
                ProtectedAccessGroupCount =
                    protectedAccessGroups.Count,
                ProjectionBasis =
                    "native_legal_range+collision_grid_virtual_occupancy_bfs+protected_endpoint_and_storage_access",
                BlockingReasons = Array.Empty<string>()
            };
        }

        reasons.Add(
            "storage_placement_all_native_tiles_disconnect_routes_or_access");
        return StoragePlacementLayoutResult.Blocked(
            reasons,
            baseline.Distances.Count,
            protectedAccessGroups.Count);
    }
}
