using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    public StoragePlacementLayoutResult ValidateExactCurrentMapBlockingPlacement(
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
        var protectedAccessGroups = new List<ProtectedAccessGroup>();
        if (!ReadCollisionFacts(grid, width, height, blocked, protectedTargets, protectedAccessGroups))
        {
            reasons.Add(reasonPrefix + "_collision_grid_incomplete");
        }
        if (!ReadExistingStorageAccessGroups(snapshot, currentLocationId, width, height, blocked, protectedAccessGroups))
        {
            reasons.Add(reasonPrefix + "_existing_access_projection_unavailable");
        }
        if (reasons.Count > 0)
        {
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
        if (baseline.Distances.Count == 0)
        {
            reasons.Add(reasonPrefix + "_reachable_domain_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        foreach (var group in protectedAccessGroups)
        {
            group.BaselineReachable = group.CandidateStandTiles.Any(baseline.Distances.ContainsKey);
        }

        var exactNativeLegal = ReadLegalTiles(placementLocation, width, height).Contains(target);
        if (!exactNativeLegal || !baseline.Distances.ContainsKey(target) || protectedTargets.Contains(target))
        {
            reasons.Add(reasonPrefix + "_target_not_reachable_unprotected_native_tile");
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, protectedAccessGroups.Count);
        }
        if (Math.Abs(stand.X - target.X) + Math.Abs(stand.Y - target.Y) != 1)
        {
            reasons.Add(reasonPrefix + "_stand_not_cardinal_to_target");
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, protectedAccessGroups.Count);
        }

        var after = Search(start, width, height, blocked, target);
        if (after.Distances.Count != baseline.Distances.Count - 1)
        {
            reasons.Add(reasonPrefix + "_target_disconnects_reachable_domain");
        }
        if (protectedAccessGroups.Any(group => group.BaselineReachable &&
            !group.CandidateStandTiles.Any(after.Distances.ContainsKey)))
        {
            reasons.Add(reasonPrefix + "_target_disconnects_protected_access");
        }
        if (!after.Distances.ContainsKey(stand))
        {
            reasons.Add(reasonPrefix + "_stand_unreachable_after_virtual_placement");
        }
        if (reasons.Count > 0)
        {
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, protectedAccessGroups.Count);
        }

        return new StoragePlacementLayoutResult
        {
            Status = "available",
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StandTileX = stand.X,
            StandTileY = stand.Y,
            RouteDistanceTiles = after.Distances[stand],
            BaselineReachableTileCount = baseline.Distances.Count,
            ReachableTileCountAfterPlacement = after.Distances.Count,
            ProtectedAccessGroupCount = protectedAccessGroups.Count,
            ProjectionBasis = "native_legal_range+collision_grid_virtual_occupancy_bfs+protected_endpoint_and_storage_access",
            BlockingReasons = Array.Empty<string>()
        };
    }
}
