using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static bool ArrivalMatches(IEnumerable<SmallModelActionParameter> parameters, int? targetX, int? targetY)
        {
            var arrivalX = ReadParameterInt(parameters, "expected_arrival_tile_x");
            var arrivalY = ReadParameterInt(parameters, "expected_arrival_tile_y");
            return targetX.HasValue && targetY.HasValue
                ? arrivalX == targetX && arrivalY == targetY
                : !arrivalX.HasValue && !arrivalY.HasValue;
        }

        private static ResolvedRoutePlan? FindResolvedRoutePlan(
            SnapshotEnvelope snapshot,
            string startLocation,
            string targetLocation,
            EventCandidate[] routeCandidates)
        {
            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object ||
                !graph.Value.TryGetProperty("edges", out var edgesElement) || edgesElement.ValueKind != JsonValueKind.Array ||
                string.IsNullOrWhiteSpace(startLocation) || string.IsNullOrWhiteSpace(targetLocation))
            {
                return null;
            }

            var edges = edgesElement.EnumerateArray()
                .Where(edge => edge.ValueKind == JsonValueKind.Object && ReadBool(edge, "resolved") == true)
                .Select(edge => new ResolvedRouteEdge(
                    ReadString(edge, "kind").ToLowerInvariant(), ReadString(edge, "from_location"), ReadString(edge, "target_location"),
                    ReadNullableInt(edge, "from_x"), ReadNullableInt(edge, "from_y"), ReadNullableInt(edge, "target_x"), ReadNullableInt(edge, "target_y")))
                .Where(edge => !string.IsNullOrWhiteSpace(edge.Kind) && !string.IsNullOrWhiteSpace(edge.FromLocation) &&
                    !string.IsNullOrWhiteSpace(edge.TargetLocation) && edge.FromX.HasValue && edge.FromY.HasValue)
                .ToArray();
            var adjacency = edges
                .GroupBy(edge => edge.FromLocation, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group
                    .OrderBy(edge => edge.TargetLocation, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                    .ThenBy(edge => edge.FromY)
                    .ThenBy(edge => edge.FromX)
                    .ToArray(), StringComparer.OrdinalIgnoreCase);
            if (!adjacency.TryGetValue(startLocation, out var firstEdges))
            {
                return null;
            }

            var plans = new List<ResolvedRoutePlan>();
            foreach (var firstEdge in firstEdges)
            {
                var tail = FindShortestResolvedRouteTail(adjacency, firstEdge.TargetLocation, targetLocation);
                if (tail is null)
                {
                    continue;
                }

                var firstConnectorCandidate = routeCandidates.FirstOrDefault(candidate =>
                    candidate.TileX == firstEdge.FromX && candidate.TileY == firstEdge.FromY &&
                    string.Equals(ReadParameter(candidate.Parameters, "connector_kind"), firstEdge.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadParameter(candidate.Parameters, "expected_target_location"), firstEdge.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                    ArrivalMatches(candidate.Parameters, firstEdge.TargetX, firstEdge.TargetY));
                plans.Add(new ResolvedRoutePlan(new[] { firstEdge }.Concat(tail).ToArray(), firstConnectorCandidate));
            }

            return plans
                .OrderByDescending(plan => plan.FirstConnectorCandidate is not null &&
                    (plan.FirstConnectorCandidate.Available || plan.FirstConnectorCandidate.AllowedToday == true))
                .ThenByDescending(plan => plan.FirstConnectorCandidate is not null)
                .ThenBy(plan => plan.Path.Length)
                .ThenByDescending(plan => plan.FirstConnectorCandidate?.Available == true)
                .ThenBy(plan => plan.Path[0].TargetLocation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(plan => plan.Path[0].Kind, StringComparer.Ordinal)
                .ThenBy(plan => plan.Path[0].FromY)
                .ThenBy(plan => plan.Path[0].FromX)
                .FirstOrDefault();
        }

        private static ResolvedRouteEdge[]? FindShortestResolvedRouteTail(
            IReadOnlyDictionary<string, ResolvedRouteEdge[]> adjacency,
            string startLocation,
            string targetLocation)
        {
            if (string.Equals(startLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<ResolvedRouteEdge>();
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startLocation };
            var queue = new Queue<(string Location, ResolvedRouteEdge[] Path)>();
            queue.Enqueue((startLocation, Array.Empty<ResolvedRouteEdge>()));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!adjacency.TryGetValue(current.Location, out var outgoing))
                {
                    continue;
                }

                foreach (var edge in outgoing)
                {
                    var path = current.Path.Concat(new[] { edge }).ToArray();
                    if (string.Equals(edge.TargetLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                    if (visited.Add(edge.TargetLocation))
                    {
                        queue.Enqueue((edge.TargetLocation, path));
                    }
                }
            }

            return null;
        }

        private sealed class ResolvedRoutePlan
        {
            public ResolvedRoutePlan(ResolvedRouteEdge[] path, EventCandidate? firstConnectorCandidate)
            {
                Path = path;
                FirstConnectorCandidate = firstConnectorCandidate;
            }

            public ResolvedRouteEdge[] Path { get; }
            public EventCandidate? FirstConnectorCandidate { get; }
        }

        private sealed class ResolvedRouteEdge
        {
            public ResolvedRouteEdge(string kind, string fromLocation, string targetLocation, int? fromX, int? fromY, int? targetX, int? targetY)
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
    }
}
