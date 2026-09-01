using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
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
                var tail = FindShortestResolvedRouteTail(
                    adjacency,
                    firstEdge.TargetLocation,
                    targetLocation,
                    startLocation);
                if (tail is null)
                {
                    continue;
                }

                var firstConnectorCandidate = routeCandidates.FirstOrDefault(candidate =>
                    candidate.TileX == firstEdge.FromX && candidate.TileY == firstEdge.FromY &&
                    string.Equals(ReadParameter(candidate.Parameters, "connector_kind"), firstEdge.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadParameter(candidate.Parameters, "expected_target_location"), firstEdge.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                    ArrivalMatches(candidate.Parameters, firstEdge.TargetX, firstEdge.TargetY));
                plans.Add(new ResolvedRoutePlan(
                    new[] { firstEdge }.Concat(tail).ToArray(),
                    firstConnectorCandidate,
                    FirstRouteActionCandidate(routeCandidates, firstConnectorCandidate)));
            }

            return plans
                .OrderByDescending(plan => plan.FirstActionCandidate is not null)
                .ThenBy(plan => plan.Path.Length)
                .ThenByDescending(plan => plan.FirstActionCandidate?.Available == true)
                .ThenByDescending(plan => plan.FirstActionCandidate?.AllowedToday == true)
                .ThenByDescending(plan => plan.FirstActionCandidate is not null)
                .ThenBy(plan => plan.Path[0].TargetLocation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(plan => plan.Path[0].Kind, StringComparer.Ordinal)
                .ThenBy(plan => plan.Path[0].FromY)
                .ThenBy(plan => plan.Path[0].FromX)
                .FirstOrDefault();
        }

        private static EventCandidate? FirstRouteActionCandidate(
            IEnumerable<EventCandidate> routeCandidates,
            EventCandidate? firstConnectorCandidate)
        {
            if (firstConnectorCandidate is null ||
                firstConnectorCandidate.Available ||
                firstConnectorCandidate.AllowedToday == true)
            {
                return firstConnectorCandidate;
            }

            return routeCandidates
                .Where(candidate =>
                    candidate.Available &&
                    string.Equals(candidate.Kind, "clear_obstacle_tile", StringComparison.Ordinal) &&
                    string.Equals(
                        ReadParameter(candidate.Parameters, "route_repair.candidate_id"),
                        firstConnectorCandidate.CandidateId,
                        StringComparison.Ordinal))
                .OrderBy(candidate => candidate.EstimatedTicks)
                .ThenBy(candidate => candidate.EnergyCost)
                .ThenBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .FirstOrDefault() ?? firstConnectorCandidate;
        }

        private static ResolvedRouteEdge[]? FindShortestResolvedRouteTail(
            IReadOnlyDictionary<string, ResolvedRouteEdge[]> adjacency,
            string startLocation,
            string targetLocation,
            string routeOrigin)
        {
            if (string.Equals(startLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<ResolvedRouteEdge>();
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                startLocation,
                routeOrigin
            };
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

        private static ResolvedRoutePlan?
            FindCommittedRelocationRoutePlan(
                SnapshotEnvelope snapshot,
                string startLocation,
                string targetLocation,
                EventCandidate[] routeCandidates,
                MachineRelocationIntent intent)
        {
            var segments = intent.RouteSegments ?? [];
            var startIndexes = segments
                .Select((segment, index) => (segment, index))
                .Where(entry => string.Equals(
                    entry.segment.FromLocationId,
                    startLocation,
                    StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.index)
                .ToArray();
            if (startIndexes.Length != 1)
            {
                return null;
            }

            var suffix = segments[startIndexes[0]..];
            if (suffix.Length == 0 ||
                !string.Equals(
                    suffix[^1].TargetLocationId,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var graph = ReadStateFieldValue(
                snapshot,
                "locations",
                "route_graph");
            if (!graph.HasValue ||
                graph.Value.ValueKind != JsonValueKind.Object ||
                !graph.Value.TryGetProperty(
                    "edges",
                    out var graphEdges) ||
                graphEdges.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var path = new List<ResolvedRouteEdge>();
            for (var index = 0; index < suffix.Length; index++)
            {
                var segment = suffix[index];
                if ((index > 0 &&
                     !string.Equals(
                         suffix[index - 1].TargetLocationId,
                         segment.FromLocationId,
                         StringComparison.OrdinalIgnoreCase)) ||
                    !graphEdges.EnumerateArray().Any(edge =>
                        edge.ValueKind == JsonValueKind.Object &&
                        ReadBool(edge, "resolved") == true &&
                        string.Equals(
                            ReadString(edge, "kind"),
                            segment.Kind,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            ReadString(edge, "from_location"),
                            segment.FromLocationId,
                            StringComparison.OrdinalIgnoreCase) &&
                        ReadNullableInt(edge, "from_x") ==
                            segment.FromTileX &&
                        ReadNullableInt(edge, "from_y") ==
                            segment.FromTileY &&
                        string.Equals(
                            ReadString(edge, "target_location"),
                            segment.TargetLocationId,
                            StringComparison.OrdinalIgnoreCase) &&
                        ReadNullableInt(edge, "target_x") ==
                            segment.ArrivalTileX &&
                        ReadNullableInt(edge, "target_y") ==
                            segment.ArrivalTileY))
                {
                    return null;
                }

                path.Add(new ResolvedRouteEdge(
                    segment.Kind,
                    segment.FromLocationId,
                    segment.TargetLocationId,
                    segment.FromTileX,
                    segment.FromTileY,
                    segment.ArrivalTileX,
                    segment.ArrivalTileY));
            }

            var first = path[0];
            var firstConnectorCandidate =
                routeCandidates.FirstOrDefault(candidate =>
                    candidate.TileX == first.FromX &&
                    candidate.TileY == first.FromY &&
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "connector_kind"),
                        first.Kind,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "expected_target_location"),
                        first.TargetLocation,
                        StringComparison.OrdinalIgnoreCase) &&
                    ArrivalMatches(
                        candidate.Parameters,
                        first.TargetX,
                        first.TargetY));
            return new ResolvedRoutePlan(
                path.ToArray(),
                firstConnectorCandidate,
                FirstRouteActionCandidate(routeCandidates, firstConnectorCandidate));
        }

        private sealed class ResolvedRoutePlan
        {
            public ResolvedRoutePlan(
                ResolvedRouteEdge[] path,
                EventCandidate? firstConnectorCandidate,
                EventCandidate? firstActionCandidate)
            {
                Path = path;
                FirstConnectorCandidate = firstConnectorCandidate;
                FirstActionCandidate = firstActionCandidate;
            }

            public ResolvedRouteEdge[] Path { get; }
            public EventCandidate? FirstConnectorCandidate { get; }
            public EventCandidate? FirstActionCandidate { get; }
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
