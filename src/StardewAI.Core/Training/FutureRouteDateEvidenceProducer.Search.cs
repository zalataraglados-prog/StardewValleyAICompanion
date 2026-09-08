using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public sealed partial class FutureRouteDateEvidenceProducer
    {
        private static RouteSearchResult SearchFeasibleRoute(
            TransparentRouteGraphResolver graph,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles,
            FutureRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing) =>
            SearchFeasibleRouteCore(
                graph,
                dateIndex,
                connectorTiles,
                request.TotalDays,
                request.StartLocation,
                request.StartTileX,
                request.StartTileY,
                request.EarliestDepartureTime,
                request.TargetLocation,
                timing,
                (current, failures) => BuildTargetResult(
                    current,
                    dateIndex,
                    connectorTiles,
                    request,
                    timing,
                    failures));

        private static RouteSearchResult SearchFeasibleLocationRoute(
            TransparentRouteGraphResolver graph,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles,
            FutureLocationRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing) =>
            SearchFeasibleRouteCore(
                graph,
                dateIndex,
                connectorTiles,
                request.TotalDays,
                request.StartLocation,
                request.StartTileX,
                request.StartTileY,
                request.EarliestDepartureTime,
                request.TargetLocation,
                timing,
                (current, failures) => BuildLocationTargetResult(
                    current,
                    dateIndex,
                    request.TargetLocation,
                    failures));

        private static RouteSearchResult SearchFeasibleRouteCore(
            TransparentRouteGraphResolver graph,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles,
            int totalDays,
            string startLocation,
            int startTileX,
            int startTileY,
            int earliestDepartureTime,
            string targetLocation,
            FutureRouteTimingCalibration timing,
            Func<RouteSearchState, ISet<string>, RouteSearchResult?> buildTarget)
        {
            var pending = new List<RouteSearchState>
            {
                new(
                    startLocation,
                    startTileX,
                    startTileY,
                    ToMinutes(earliestDepartureTime),
                    Array.Empty<TransparentRouteEdge>(),
                    Array.Empty<FutureRouteSegmentEvidence>())
            };
            var bestArrivalByState = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                [StateKey(startLocation, startTileX, startTileY)] =
                    ToMinutes(earliestDepartureTime)
            };
            var failures = new HashSet<string>(StringComparer.Ordinal);
            RouteSearchResult? bestResult = null;

            while (pending.Count > 0)
            {
                var current = pending
                    .OrderBy(state => state.ArrivalMinutes)
                    .ThenBy(state => state.Path.Length)
                    .ThenBy(state => PathKey(state.Path), StringComparer.Ordinal)
                    .First();
                pending.Remove(current);
                if (bestResult is not null &&
                    current.ArrivalMinutes >= bestResult.ArrivalMinutes)
                {
                    break;
                }
                if (bestArrivalByState.TryGetValue(
                        StateKey(current.Location, current.X, current.Y),
                        out var bestKnown) &&
                    current.ArrivalMinutes > bestKnown)
                {
                    continue;
                }

                if (string.Equals(
                        current.Location,
                        targetLocation,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = buildTarget(current, failures);
                    if (candidate is not null &&
                        (bestResult is null ||
                         candidate.ArrivalMinutes < bestResult.ArrivalMinutes ||
                         (candidate.ArrivalMinutes == bestResult.ArrivalMinutes &&
                          string.CompareOrdinal(
                              PathKey(candidate.Path),
                              PathKey(bestResult.Path)) < 0)))
                    {
                        bestResult = candidate;
                    }
                }

                foreach (var edge in graph.GetOutgoingEdges(current.Location))
                {
                    if (!TryAdvance(
                            current,
                            edge,
                            dateIndex,
                            connectorTiles,
                            totalDays,
                            timing,
                            out var next,
                            out var failure))
                    {
                        failures.Add(failure);
                        continue;
                    }

                    var key = StateKey(next.Location, next.X, next.Y);
                    if (bestArrivalByState.TryGetValue(key, out var prior) &&
                        prior <= next.ArrivalMinutes)
                    {
                        continue;
                    }
                    bestArrivalByState[key] = next.ArrivalMinutes;
                    pending.Add(next);
                }
            }

            if (bestResult is not null)
                return bestResult;
            if (!graph.HasTopologicalPath(
                    startLocation,
                    targetLocation))
            {
                failures.Add("future_route_graph_path_missing");
            }
            return RouteSearchResult.Blocked(
                failures.Count == 0
                    ? new[] { "future_route_date_evidence_path_unavailable" }
                    : failures
                        .Select(FailureClass)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray());
        }

        private static bool TryAdvance(
            RouteSearchState current,
            TransparentRouteEdge edge,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles,
            int totalDays,
            FutureRouteTimingCalibration timing,
            out RouteSearchState next,
            out string failure)
        {
            next = null!;
            failure = string.Empty;
            if (!edge.FromX.HasValue || !edge.FromY.HasValue ||
                !edge.TargetX.HasValue || !edge.TargetY.HasValue)
            {
                failure = "future_route_edge_coordinates_or_chain_incomplete";
                return false;
            }
            if (!dateIndex.TryGetMap(edge.FromLocation, out var map))
            {
                failure = "future_route_source_map_evidence_missing:" + edge.FromLocation;
                return false;
            }
            if (!map.AccessibleOnCaptureDate)
            {
                failure = "future_route_source_map_inaccessible_on_date:" +
                    edge.FromLocation;
                return false;
            }

            var stands = RouteConnectorStandTileResolver.ResolveCandidates(
                edge.Kind,
                edge.FromX.Value,
                edge.FromY.Value,
                map.Width,
                map.Height);
            var forbidden = connectorTiles.TryGetValue(edge.FromLocation, out var blocked)
                ? blocked
                : EmptyTiles;
            var selected = SelectReachableStand(
                map,
                current.X,
                current.Y,
                stands,
                forbidden);
            if (!selected.HasValue)
            {
                failure = "future_route_connector_approach_unreachable:" +
                    edge.FromLocation + ":" + edge.FromX + "," + edge.FromY;
                return false;
            }
            if (!TryResolveGate(
                    map,
                    edge,
                    out var gateComplete,
                    out var allowedOnDate,
                    out var openTime,
                    out var closeTime))
            {
                failure = "future_route_connector_gate_unresolved:" +
                    edge.FromLocation + ":" + edge.FromX + "," + edge.FromY;
                return false;
            }
            if (!allowedOnDate)
            {
                failure = "future_route_connector_not_allowed_on_date:" +
                    edge.FromLocation + ":" + edge.FromX + "," + edge.FromY;
                return false;
            }

            var approachMinutes = ToGameMinutes(selected.Value.Distance, timing);
            var nextArrival = current.ArrivalMinutes + approachMinutes;
            if (openTime.HasValue)
            {
                var openMinutes = ToMinutes(openTime.Value);
                var closeMinutes = ToMinutes(closeTime!.Value);
                nextArrival = Math.Max(nextArrival, openMinutes);
                if (nextArrival >= closeMinutes)
                {
                    failure = "future_route_connector_closed_before_arrival:" +
                        edge.FromLocation + ":" + edge.FromX + "," + edge.FromY;
                    return false;
                }
            }
            nextArrival += timing.ConnectorTransitionGameMinutes;
            if (nextArrival > ToMinutes(2800))
            {
                failure = "future_route_connector_arrival_outside_supported_day";
                return false;
            }

            var segment = new FutureRouteSegmentEvidence
            {
                TotalDays = totalDays,
                Kind = edge.Kind,
                FromLocation = edge.FromLocation,
                FromTileX = edge.FromX.Value,
                FromTileY = edge.FromY.Value,
                TargetLocation = edge.TargetLocation,
                TargetTileX = edge.TargetX.Value,
                TargetTileY = edge.TargetY.Value,
                ApproachFromTileX = current.X,
                ApproachFromTileY = current.Y,
                ApproachTravelGameMinutes = approachMinutes,
                ConnectorTransitionGameMinutes =
                    timing.ConnectorTransitionGameMinutes,
                TimingEvidenceKind = timing.EvidenceKind,
                TimingEvidenceId = timing.EvidenceId,
                TraversabilityComplete = true,
                GateStateComplete = gateComplete,
                AllowedOnDate = true,
                OpenTime = openTime,
                CloseTimeExclusive = closeTime
            };
            next = new RouteSearchState(
                edge.TargetLocation,
                edge.TargetX.Value,
                edge.TargetY.Value,
                nextArrival,
                current.Path.Concat(new[] { edge }).ToArray(),
                current.Segments.Concat(new[] { segment }).ToArray());
            return true;
        }

    }
}
