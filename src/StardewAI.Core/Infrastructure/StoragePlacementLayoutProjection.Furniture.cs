using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    public StoragePlacementLayoutResult ValidateExactCurrentMapFurniturePlacement(
        SnapshotEnvelope snapshot,
        JsonElement rotationRow,
        JsonElement range,
        int targetX,
        int targetY,
        int standX,
        int standY,
        bool canFreePlace)
    {
        var reasons = new List<string>();
        var currentLocationId = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(ReadString(rotationRow, "location_id"), currentLocationId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(ReadString(rotationRow, "placement_probe_status"), "native_legal_tiles_available", StringComparison.Ordinal))
        {
            reasons.Add("furniture_placement_location_or_native_projection_invalid");
        }

        var collision = ReadStateFieldValue(snapshot, "locations", "collision_grid");
        if (!collision.HasValue || collision.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("furniture_placement_collision_grid_unavailable");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }
        var grid = collision.Value;
        var width = ReadInt(grid, "width");
        var height = ReadInt(grid, "height");
        if (width <= 0 || height <= 0 ||
            !string.Equals(ReadString(grid, "location_id"), currentLocationId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("furniture_placement_collision_grid_identity_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var blocked = new HashSet<Tile>();
        var protectedTargets = new HashSet<Tile>();
        var accessGroups = new List<ProtectedAccessGroup>();
        if (!ReadCollisionFacts(grid, width, height, blocked, protectedTargets, accessGroups))
        {
            reasons.Add("furniture_placement_collision_grid_incomplete");
        }
        if (!ReadExistingFurnitureStorageAccessGroups(snapshot, width, height, blocked, accessGroups))
        {
            reasons.Add("furniture_placement_existing_furniture_access_projection_unavailable");
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
        if (!InBounds(start, width, height) || blocked.Contains(start) ||
            !ReadLegalTiles(rotationRow, width, height).Contains(target))
        {
            reasons.Add("furniture_placement_start_or_exact_target_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons);
        }

        var baseline = Search(start, width, height, blocked, extraBlocked: null);
        var anchor = new Tile(
            targetX + ReadInt(range, "anchor_offset_x"),
            targetY + ReadInt(range, "anchor_offset_y"));
        var footprintWidth = ReadInt(range, "footprint_width");
        var footprintHeight = ReadInt(range, "footprint_height");
        if (footprintWidth <= 0 || footprintHeight <= 0)
        {
            reasons.Add("furniture_placement_footprint_invalid");
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, accessGroups.Count);
        }
        var footprint = Enumerable.Range(0, footprintWidth)
            .SelectMany(dx => Enumerable.Range(0, footprintHeight).Select(dy => new Tile(anchor.X + dx, anchor.Y + dy)))
            .Where(tile => InBounds(tile, width, height))
            .ToHashSet();
        if (footprint.Any(protectedTargets.Contains))
        {
            reasons.Add("furniture_placement_footprint_covers_protected_endpoint");
        }

        if (canFreePlace)
        {
            if (stand != start)
            {
                reasons.Add("furniture_placement_remote_stand_must_equal_current_player_tile");
            }
        }
        else if (!baseline.Distances.ContainsKey(stand) ||
            !footprint.SelectMany(CardinalNeighbors).Any(tile => tile == stand && !footprint.Contains(tile)))
        {
            reasons.Add("furniture_placement_cardinal_stand_invalid");
        }
        if (reasons.Count > 0)
        {
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, accessGroups.Count);
        }

        foreach (var group in accessGroups)
        {
            group.BaselineReachable = group.CandidateStandTiles.Any(baseline.Distances.ContainsKey);
        }
        var endpoint = ReadString(range, "placement_endpoint");
        var passable = ReadBool(range, "expected_passable");
        var extraBlocked = passable || string.Equals(endpoint, "table_held_object", StringComparison.Ordinal)
            ? new HashSet<Tile>()
            : footprint.Where(baseline.Distances.ContainsKey).ToHashSet();
        var after = SearchWithExtraBlocked(start, width, height, blocked, extraBlocked);
        var expectedReachable = baseline.Distances.Count - extraBlocked.Count;
        if (after.Distances.Count != expectedReachable || accessGroups.Any(group =>
                group.BaselineReachable && !group.CandidateStandTiles.Any(after.Distances.ContainsKey)))
        {
            reasons.Add("furniture_placement_disconnects_route_or_access");
            return StoragePlacementLayoutResult.Blocked(reasons, baseline.Distances.Count, accessGroups.Count);
        }

        return new StoragePlacementLayoutResult
        {
            Status = "available",
            TargetTileX = targetX,
            TargetTileY = targetY,
            StandTileX = standX,
            StandTileY = standY,
            RouteDistanceTiles = canFreePlace ? 0 : baseline.Distances[stand],
            BaselineReachableTileCount = baseline.Distances.Count,
            ReachableTileCountAfterPlacement = after.Distances.Count,
            ProtectedAccessGroupCount = accessGroups.Count,
            ProjectionBasis = "native_furniture_range+rotation_adjusted_rectangular_footprint+remote_or_cardinal_reach+protected_endpoint_and_furniture_storage_access",
            BlockingReasons = Array.Empty<string>()
        };
    }

    private static bool ReadExistingFurnitureStorageAccessGroups(
        SnapshotEnvelope snapshot,
        int width,
        int height,
        ISet<Tile> blocked,
        ICollection<ProtectedAccessGroup> groups)
    {
        var furniture = ReadStateFieldValue(snapshot, "current_location", "furniture");
        if (!furniture.HasValue || furniture.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var row in furniture.Value.EnumerateArray().Where(row =>
                     row.ValueKind == JsonValueKind.Object && ReadInt(row, "storage_capacity") > 0))
        {
            var widthTiles = Math.Max(1, ReadInt(row, "tiles_wide", 1));
            var heightTiles = Math.Max(1, ReadInt(row, "tiles_high", 1));
            var anchorX = ReadInt(row, "tile_x", -1);
            var anchorY = ReadInt(row, "tile_y", -1);
            var stands = Enumerable.Range(0, widthTiles)
                .SelectMany(dx => Enumerable.Range(0, heightTiles).Select(dy => new Tile(anchorX + dx, anchorY + dy)))
                .SelectMany(CardinalNeighbors)
                .Where(tile => InBounds(tile, width, height) && !blocked.Contains(tile))
                .Distinct()
                .ToArray();
            groups.Add(new ProtectedAccessGroup(stands));
        }
        return true;
    }
}
