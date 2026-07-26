using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed class MachineRelocationReachabilityProjection
{
    private readonly IReadOnlyDictionary<(int X, int Y), int> distances;
    private readonly IReadOnlyDictionary<
        (int X, int Y),
        (int X, int Y)?> parents;

    public MachineRelocationReachabilityProjection(
        IReadOnlyDictionary<(int X, int Y), int> distances,
        IReadOnlyDictionary<
            (int X, int Y),
            (int X, int Y)?> parents)
    {
        this.distances = distances;
        this.parents = parents;
    }

    public bool Contains(int x, int y) =>
        distances.ContainsKey((x, y));

    public bool TryGetProvenDistanceAvoiding(
        int standX,
        int standY,
        int excludedX,
        int excludedY,
        out int distance)
    {
        var standKey = (X: standX, Y: standY);
        if (!distances.TryGetValue(standKey, out distance))
        {
            return false;
        }

        var excludedKey = (X: excludedX, Y: excludedY);
        (int X, int Y)? cursor = standKey;
        while (cursor.HasValue)
        {
            if (cursor.Value == excludedKey)
            {
                return false;
            }
            if (!parents.TryGetValue(
                    cursor.Value,
                    out var parent))
            {
                return false;
            }
            cursor = parent;
        }
        return true;
    }
}

internal static class MachineRelocationReachabilityProjectionReader
{
    public static MachineRelocationReachabilityProjection? Read(
        SnapshotEnvelope snapshot,
        string locationId,
        int arrivalX,
        int arrivalY)
    {
        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "machine_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            !placement.Value.TryGetProperty(
                "relocation_route_reachability",
                out var projection) ||
            projection.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(projection, "projection_status"),
                "complete_static_native_walkability_for_relocation_scope",
                StringComparison.Ordinal) ||
            !projection.TryGetProperty(
                "locations",
                out var locations) ||
            locations.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var row = locations.EnumerateArray().FirstOrDefault(candidate =>
            candidate.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(candidate, "location_id"),
                locationId,
                StringComparison.OrdinalIgnoreCase));
        if (row.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(row, "projection_status"),
                "native_static_walkable_tiles_available",
                StringComparison.Ordinal) ||
            !row.TryGetProperty(
                "static_walkable_tile_ranges",
                out var ranges) ||
            ranges.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var width = ReadInt(row, "map_width");
        var height = ReadInt(row, "map_height");
        if (!InBounds(arrivalX, arrivalY, width, height))
        {
            return null;
        }

        var walkable = new HashSet<(int X, int Y)>();
        foreach (var range in ranges.EnumerateArray().Where(candidate =>
            candidate.ValueKind == JsonValueKind.Object))
        {
            var y = ReadInt(range, "y");
            var startX = ReadInt(range, "start_x");
            var endX = ReadInt(range, "end_x", startX - 1);
            if (y < 0 || y >= height ||
                startX < 0 || endX >= width ||
                endX < startX)
            {
                return null;
            }
            for (var x = startX; x <= endX; x++)
            {
                if (!walkable.Add((x, y)))
                {
                    return null;
                }
            }
        }
        if (ReadInt(row, "static_walkable_tile_count", -1) !=
            walkable.Count)
        {
            return null;
        }

        var arrivalKey = (X: arrivalX, Y: arrivalY);
        if (!walkable.Contains(arrivalKey))
        {
            return null;
        }

        var distances =
            new Dictionary<(int X, int Y), int>
        {
            [arrivalKey] = 0
        };
        var parents =
            new Dictionary<(int X, int Y), (int X, int Y)?>
        {
            [arrivalKey] = null
        };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((arrivalX, arrivalY));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentKey = (current.X, current.Y);
            foreach (var next in CardinalNeighbors(
                current.X,
                current.Y))
            {
                var nextKey = (next.X, next.Y);
                if (!walkable.Contains(nextKey) ||
                    distances.ContainsKey(nextKey))
                {
                    continue;
                }
                distances[nextKey] = distances[currentKey] + 1;
                parents[nextKey] = currentKey;
                queue.Enqueue(next);
            }
        }

        return new MachineRelocationReachabilityProjection(
            distances,
            parents);
    }

    private static IEnumerable<(int X, int Y)> CardinalNeighbors(
        int x,
        int y)
    {
        yield return (x, y - 1);
        yield return (x - 1, y);
        yield return (x + 1, y);
        yield return (x, y + 1);
    }

    private static bool InBounds(
        int x,
        int y,
        int width,
        int height) =>
        width > 0 &&
        height > 0 &&
        x >= 0 &&
        x < width &&
        y >= 0 &&
        y < height;
}
