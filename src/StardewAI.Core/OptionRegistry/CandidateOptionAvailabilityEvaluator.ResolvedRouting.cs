using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
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
            if (!graph.HasValue ||
                string.IsNullOrWhiteSpace(startLocation) || string.IsNullOrWhiteSpace(targetLocation))
            {
                return null;
            }
            if (!TransparentRouteGraphResolver.TryCreate(graph.Value, out var resolver))
                return null;

            var plans = new List<ResolvedRoutePlan>();
            foreach (var path in resolver.FindShortestPathsByFirstEdge(startLocation, targetLocation))
            {
                if (path.Length == 0)
                    continue;
                var firstEdge = path[0];

                var firstConnectorCandidate = routeCandidates.FirstOrDefault(candidate =>
                    candidate.TileX == firstEdge.FromX && candidate.TileY == firstEdge.FromY &&
                    string.Equals(ReadParameter(candidate.Parameters, "connector_kind"), firstEdge.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(ReadParameter(candidate.Parameters, "expected_target_location"), firstEdge.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                    ArrivalMatches(candidate.Parameters, firstEdge.TargetX, firstEdge.TargetY));
                plans.Add(new ResolvedRoutePlan(
                    path,
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

            var path = new List<TransparentRouteEdge>();
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

                path.Add(new TransparentRouteEdge(
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
                TransparentRouteEdge[] path,
                EventCandidate? firstConnectorCandidate,
                EventCandidate? firstActionCandidate)
            {
                Path = path;
                FirstConnectorCandidate = firstConnectorCandidate;
                FirstActionCandidate = firstActionCandidate;
            }

            public TransparentRouteEdge[] Path { get; }
            public EventCandidate? FirstConnectorCandidate { get; }
            public EventCandidate? FirstActionCandidate { get; }
        }

    }
}
