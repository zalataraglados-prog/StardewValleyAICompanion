using System;
using System.Collections.Generic;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure
{
    internal static class RouteGridPathfinder
    {
        internal static HashSet<string> ReadBlockedTiles(
            JsonElement collisionGrid)
        {
            var blocked = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty(
                    "notable_tiles",
                    out var notableTiles) ||
                notableTiles.ValueKind != JsonValueKind.Array)
            {
                return blocked;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object &&
                    ReadBool(tile, "collision_blocked") == true)
                {
                    blocked.Add(Key(
                        ReadInt(tile, "tile_x"),
                        ReadInt(tile, "tile_y")));
                }
            }
            return blocked;
        }

        internal static HashSet<string> ReadUnsupportedActionTiles(
            SnapshotEnvelope snapshot)
        {
            var unsupported = new HashSet<string>(StringComparer.Ordinal);
            var coverage = ReadStateFieldValue(
                snapshot,
                "locations",
                "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return unsupported;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty(
                        "route_training_blocked",
                        out var blocked) &&
                    blocked.ValueKind == JsonValueKind.True)
                {
                    unsupported.Add(Key(
                        ReadInt(row, "tile_x"),
                        ReadInt(row, "tile_y")));
                }
            }
            return unsupported;
        }

        internal static int[] DistancesFrom(
            int startX,
            int startY,
            int width,
            int height,
            ISet<string> blockedTiles,
            ISet<string> extraBlockedTiles)
            => BuildDistanceField(
                startX,
                startY,
                width,
                height,
                (x, y) => !blockedTiles.Contains(Key(x, y)),
                (x, y) =>
                    !blockedTiles.Contains(Key(x, y)) &&
                    !extraBlockedTiles.Contains(Key(x, y)));

        internal static int[] BuildDistanceField(
            int startX,
            int startY,
            int width,
            int height,
            Func<int, int, bool> canStart,
            Func<int, int, bool> canEnter)
        {
            if (width <= 0 || height <= 0)
                return Array.Empty<int>();

            var distances = new int[checked(width * height)];
            Array.Fill(distances, -1);
            if (!InBounds(startX, startY, width, height) ||
                !canStart(startX, startY))
            {
                return distances;
            }

            distances[startY * width + startX] = 0;
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((startX, startY));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var distance = distances[current.Y * width + current.X];
                foreach (var next in Neighbors(current.X, current.Y))
                {
                    if (!InBounds(next.X, next.Y, width, height) ||
                        distances[next.Y * width + next.X] >= 0 ||
                        !canEnter(next.X, next.Y))
                    {
                        continue;
                    }
                    distances[next.Y * width + next.X] = distance + 1;
                    queue.Enqueue(next);
                }
            }
            return distances;
        }

        internal static int? ShortestPathLength(
            int startX,
            int startY,
            int targetX,
            int targetY,
            int width,
            int height,
            ISet<string> blockedTiles,
            ISet<string> extraBlockedTiles)
        {
            var startKey = Key(startX, startY);
            var targetKey = Key(targetX, targetY);
            if (!InBounds(startX, startY, width, height) ||
                !InBounds(targetX, targetY, width, height) ||
                blockedTiles.Contains(startKey) ||
                blockedTiles.Contains(targetKey) ||
                extraBlockedTiles.Contains(targetKey))
            {
                return null;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal)
            {
                startKey
            };
            var queue = new Queue<(int X, int Y, int Distance)>();
            queue.Enqueue((startX, startY, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return current.Distance;
                }

                foreach (var next in Neighbors(current.X, current.Y))
                {
                    var key = Key(next.X, next.Y);
                    if (!InBounds(next.X, next.Y, width, height) ||
                        !visited.Add(key) ||
                        blockedTiles.Contains(key) ||
                        extraBlockedTiles.Contains(key))
                    {
                        continue;
                    }
                    queue.Enqueue((next.X, next.Y, current.Distance + 1));
                }
            }
            return null;
        }

        internal static bool InBounds(
            int x,
            int y,
            int width,
            int height) =>
            x >= 0 && y >= 0 && x < width && y < height;

        internal static string Key(int x, int y) => x + "," + y;

        internal static IEnumerable<(int X, int Y)> Neighbors(int x, int y)
        {
            yield return (x + 1, y);
            yield return (x - 1, y);
            yield return (x, y + 1);
            yield return (x, y - 1);
        }
    }
}
