using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    private static bool ReadCollisionFacts(
        JsonElement grid,
        int width,
        int height,
        ISet<Tile> blocked,
        ISet<Tile> protectedTargets,
        ICollection<ProtectedAccessGroup> accessGroups)
    {
        if (!grid.TryGetProperty(
                "notable_tiles",
                out var tiles) ||
            tiles.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var row in tiles.EnumerateArray()
                     .Where(row =>
                         row.ValueKind ==
                         JsonValueKind.Object))
        {
            var tile = new Tile(
                ReadInt(row, "tile_x", -1),
                ReadInt(row, "tile_y", -1));
            if (!InBounds(tile, width, height))
            {
                continue;
            }
            if (ReadBool(row, "collision_blocked"))
            {
                blocked.Add(tile);
            }

            var protectedEndpoint =
                !string.IsNullOrWhiteSpace(
                    ReadString(row, "action")) ||
                !string.IsNullOrWhiteSpace(
                    ReadString(row, "touch_action")) ||
                !string.IsNullOrWhiteSpace(
                    ReadString(row, "warp_target")) ||
                ReadBool(row, "door") ||
                ReadBool(row, "interior_door");
            if (!protectedEndpoint)
            {
                continue;
            }

            protectedTargets.Add(tile);
            accessGroups.Add(
                BuildAccessGroup(
                    tile,
                    width,
                    height,
                    blocked));
        }
        return true;
    }

    private static bool ReadExistingStorageAccessGroups(
        SnapshotEnvelope snapshot,
        string locationId,
        int width,
        int height,
        ISet<Tile> blocked,
        ICollection<ProtectedAccessGroup> accessGroups)
    {
        var storage = ReadStateFieldValue(
            snapshot,
            "current_location",
            "chests");
        if (!storage.HasValue ||
            storage.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(storage.Value, "schema_version"),
                "storage_infrastructure.v1",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(storage.Value, "status"),
                "available",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(storage.Value, "scope_location_id"),
                locationId,
                StringComparison.OrdinalIgnoreCase) ||
            !storage.Value.TryGetProperty(
                "access_points",
                out var accessPoints) ||
            accessPoints.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var row in accessPoints.EnumerateArray()
                     .Where(row =>
                         row.ValueKind ==
                         JsonValueKind.Object &&
                         string.Equals(
                             ReadString(row, "location_id"),
                             locationId,
                             StringComparison.OrdinalIgnoreCase)))
        {
            var tile = new Tile(
                ReadInt(row, "tile_x", -1),
                ReadInt(row, "tile_y", -1));
            if (!InBounds(tile, width, height))
            {
                continue;
            }
            accessGroups.Add(
                BuildAccessGroup(
                    tile,
                    width,
                    height,
                    blocked));
        }
        return true;
    }

    private static ProtectedAccessGroup BuildAccessGroup(
        Tile endpoint,
        int width,
        int height,
        ISet<Tile> blocked)
    {
        return new ProtectedAccessGroup(
            CardinalNeighbors(endpoint)
                .Where(tile => InBounds(tile, width, height))
                .Where(tile => !blocked.Contains(tile))
                .ToArray());
    }

    private static IEnumerable<Tile> ReadLegalTiles(
        JsonElement placementLocation,
        int width,
        int height)
    {
        if (!placementLocation.TryGetProperty(
                "static_legal_tile_ranges",
                out var ranges) ||
            ranges.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var range in ranges.EnumerateArray()
                     .Where(range =>
                         range.ValueKind ==
                         JsonValueKind.Object))
        {
            var y = ReadInt(range, "y", -1);
            var startX = ReadInt(range, "start_x");
            var endX = ReadInt(range, "end_x", -1);
            if (y < 0 ||
                y >= height ||
                startX < 0 ||
                endX < startX ||
                endX >= width)
            {
                continue;
            }
            for (var x = startX; x <= endX; x++)
            {
                yield return new Tile(x, y);
            }
        }
    }
}
