using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure
{
    public sealed class TransparentRouteEdge
    {
        public TransparentRouteEdge(
            string kind,
            string fromLocation,
            string targetLocation,
            int? fromX,
            int? fromY,
            int? targetX,
            int? targetY)
        {
            Kind = kind;
            FromLocation = fromLocation;
            TargetLocation = targetLocation;
            FromX = fromX;
            FromY = fromY;
            TargetX = targetX;
            TargetY = targetY;
        }

        public string Kind { get; }

        public string FromLocation { get; }

        public string TargetLocation { get; }

        public int? FromX { get; }

        public int? FromY { get; }

        public int? TargetX { get; }

        public int? TargetY { get; }
    }

    public sealed class TransparentRouteGraphResolver
    {
        private readonly IReadOnlyDictionary<string, TransparentRouteEdge[]> adjacency;

        private TransparentRouteGraphResolver(IReadOnlyDictionary<string, TransparentRouteEdge[]> adjacency)
        {
            this.adjacency = adjacency;
        }

        public static bool TryCreate(JsonElement graph, out TransparentRouteGraphResolver resolver)
        {
            resolver = new TransparentRouteGraphResolver(
                new Dictionary<string, TransparentRouteEdge[]>(StringComparer.OrdinalIgnoreCase));
            if (graph.ValueKind != JsonValueKind.Object ||
                !graph.TryGetProperty("edges", out var edgesElement) ||
                edgesElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var edges = edgesElement.EnumerateArray()
                .Where(edge => edge.ValueKind == JsonValueKind.Object && ReadBool(edge, "resolved") == true)
                .Select(edge => new TransparentRouteEdge(
                    ReadString(edge, "kind").ToLowerInvariant(),
                    ReadString(edge, "from_location"),
                    ReadString(edge, "target_location"),
                    ReadNullableInt(edge, "from_x"),
                    ReadNullableInt(edge, "from_y"),
                    ReadNullableInt(edge, "target_x"),
                    ReadNullableInt(edge, "target_y")))
                .Where(edge =>
                    !string.IsNullOrWhiteSpace(edge.Kind) &&
                    !string.IsNullOrWhiteSpace(edge.FromLocation) &&
                    !string.IsNullOrWhiteSpace(edge.TargetLocation) &&
                    edge.FromX.HasValue &&
                    edge.FromY.HasValue)
                .ToArray();
            var parsedAdjacency = edges
                .GroupBy(edge => edge.FromLocation, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(edge => edge.TargetLocation, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                        .ThenBy(edge => edge.FromY)
                        .ThenBy(edge => edge.FromX)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            resolver = new TransparentRouteGraphResolver(parsedAdjacency);
            return true;
        }

        public TransparentRouteEdge[][] FindShortestPathsByFirstEdge(
            string startLocation,
            string targetLocation)
        {
            if (string.IsNullOrWhiteSpace(startLocation) || string.IsNullOrWhiteSpace(targetLocation))
                return Array.Empty<TransparentRouteEdge[]>();
            if (string.Equals(startLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                return new[] { Array.Empty<TransparentRouteEdge>() };
            if (!adjacency.TryGetValue(startLocation, out var firstEdges))
                return Array.Empty<TransparentRouteEdge[]>();

            var paths = new List<TransparentRouteEdge[]>();
            foreach (var firstEdge in firstEdges)
            {
                var tail = FindShortestTail(firstEdge.TargetLocation, targetLocation, startLocation);
                if (tail is not null)
                    paths.Add(new[] { firstEdge }.Concat(tail).ToArray());
            }
            return paths.ToArray();
        }

        public IReadOnlyList<TransparentRouteEdge> GetOutgoingEdges(
            string location) =>
            adjacency.TryGetValue(location, out var edges)
                ? edges
                : Array.Empty<TransparentRouteEdge>();

        public bool ContainsEdge(TransparentRouteEdge candidate) =>
            adjacency.TryGetValue(candidate.FromLocation, out var edges) &&
            edges.Any(edge =>
                string.Equals(edge.Kind, candidate.Kind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(edge.TargetLocation, candidate.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                edge.FromX == candidate.FromX &&
                edge.FromY == candidate.FromY &&
                edge.TargetX == candidate.TargetX &&
                edge.TargetY == candidate.TargetY);

        public bool HasTopologicalPath(
            string startLocation,
            string targetLocation)
        {
            if (string.IsNullOrWhiteSpace(startLocation) ||
                string.IsNullOrWhiteSpace(targetLocation))
            {
                return false;
            }
            if (string.Equals(
                    startLocation,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                startLocation
            };
            var queue = new Queue<string>();
            queue.Enqueue(startLocation);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var edge in GetOutgoingEdges(current))
                {
                    if (string.Equals(
                            edge.TargetLocation,
                            targetLocation,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (visited.Add(edge.TargetLocation))
                        queue.Enqueue(edge.TargetLocation);
                }
            }
            return false;
        }

        private TransparentRouteEdge[]? FindShortestTail(
            string startLocation,
            string targetLocation,
            string routeOrigin)
        {
            if (string.Equals(startLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                return Array.Empty<TransparentRouteEdge>();

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                startLocation,
                routeOrigin
            };
            var queue = new Queue<(string Location, TransparentRouteEdge[] Path)>();
            queue.Enqueue((startLocation, Array.Empty<TransparentRouteEdge>()));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current.Location, out var outgoing))
                    continue;
                foreach (var edge in outgoing)
                {
                    var path = current.Path.Concat(new[] { edge }).ToArray();
                    if (string.Equals(edge.TargetLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                        return path;
                    if (visited.Add(edge.TargetLocation))
                        queue.Enqueue((edge.TargetLocation, path));
                }
            }
            return null;
        }

        private static string ReadString(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int? ReadNullableInt(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var value)
                    ? value
                    : null;
        }

        private static bool? ReadBool(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }
            return property.GetBoolean();
        }
    }
}
