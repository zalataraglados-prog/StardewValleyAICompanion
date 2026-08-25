using System.Collections.Generic;

namespace StardewAI.Core.Infrastructure;

internal sealed partial class StoragePlacementLayoutProjection
{
    private static SearchResult Search(
        Tile start,
        int width,
        int height,
        ISet<Tile> blocked,
        Tile? extraBlocked)
    {
        var distances = new Dictionary<Tile, int>();
        if (blocked.Contains(start) ||
            extraBlocked == start)
        {
            return new SearchResult(distances);
        }

        var queue = new Queue<Tile>();
        distances[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in CardinalNeighbors(current))
            {
                if (!InBounds(next, width, height) ||
                    blocked.Contains(next) ||
                    extraBlocked == next ||
                    distances.ContainsKey(next))
                {
                    continue;
                }
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }
        return new SearchResult(distances);
    }

    private static SearchResult SearchWithExtraBlocked(
        Tile start,
        int width,
        int height,
        ISet<Tile> blocked,
        ISet<Tile> extraBlocked)
    {
        var distances = new Dictionary<Tile, int>();
        if (blocked.Contains(start) || extraBlocked.Contains(start))
        {
            return new SearchResult(distances);
        }

        var queue = new Queue<Tile>();
        distances[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in CardinalNeighbors(current))
            {
                if (!InBounds(next, width, height) || blocked.Contains(next) ||
                    extraBlocked.Contains(next) || distances.ContainsKey(next))
                {
                    continue;
                }
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }
        return new SearchResult(distances);
    }

    private static IEnumerable<Tile> CardinalNeighbors(
        Tile tile)
    {
        yield return new Tile(tile.X + 1, tile.Y);
        yield return new Tile(tile.X - 1, tile.Y);
        yield return new Tile(tile.X, tile.Y + 1);
        yield return new Tile(tile.X, tile.Y - 1);
    }

    private static bool InBounds(
        Tile tile,
        int width,
        int height)
    {
        return tile.X >= 0 &&
            tile.Y >= 0 &&
            tile.X < width &&
            tile.Y < height;
    }

    private readonly record struct Tile(int X, int Y);

    private sealed class ProtectedAccessGroup
    {
        public ProtectedAccessGroup(
            Tile[] candidateStandTiles)
        {
            CandidateStandTiles = candidateStandTiles;
        }

        public Tile[] CandidateStandTiles { get; }

        public bool BaselineReachable { get; set; }
    }

    private sealed record SearchResult(
        Dictionary<Tile, int> Distances);
}
