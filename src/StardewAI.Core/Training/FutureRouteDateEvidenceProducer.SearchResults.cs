using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public sealed partial class FutureRouteDateEvidenceProducer
    {
        private static RouteSearchResult? BuildTargetResult(
            RouteSearchState current,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles,
            FutureRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing,
            ISet<string> failures)
        {
            if (!dateIndex.TryGetMap(request.TargetLocation, out var targetMap))
            {
                failures.Add(
                    "future_route_target_map_evidence_missing:" + request.TargetLocation);
                return null;
            }
            if (!targetMap.AccessibleOnCaptureDate)
            {
                failures.Add(
                    "future_route_target_map_inaccessible_on_date:" +
                        request.TargetLocation);
                return null;
            }
            var targetStands = new[]
            {
                new RouteTileCoordinate(request.TargetTileX + 1, request.TargetTileY),
                new RouteTileCoordinate(request.TargetTileX - 1, request.TargetTileY),
                new RouteTileCoordinate(request.TargetTileX, request.TargetTileY + 1),
                new RouteTileCoordinate(request.TargetTileX, request.TargetTileY - 1)
            };
            var forbidden = connectorTiles.TryGetValue(
                    request.TargetLocation,
                    out var targetBlocked)
                ? targetBlocked
                : EmptyTiles;
            var stand = SelectReachableStand(
                targetMap,
                current.X,
                current.Y,
                targetStands,
                forbidden);
            if (!stand.HasValue)
            {
                failures.Add("future_route_target_adjacent_stand_unreachable");
                return null;
            }

            var travelMinutes = ToGameMinutes(stand.Value.Distance, timing);
            return RouteSearchResult.Produced(
                current.ArrivalMinutes + travelMinutes,
                current.Path,
                current.Segments,
                new FutureRouteApproachEvidence
                {
                    TotalDays = request.TotalDays,
                    LocationName = request.TargetLocation,
                    FromTileX = current.X,
                    FromTileY = current.Y,
                    TargetTileX = request.TargetTileX,
                    TargetTileY = request.TargetTileY,
                    StandTileX = stand.Value.Tile.X,
                    StandTileY = stand.Value.Tile.Y,
                    TravelGameMinutes = travelMinutes,
                    TimingEvidenceKind = timing.EvidenceKind,
                    TimingEvidenceId = timing.EvidenceId,
                    TraversabilityComplete = true
                });
        }

        private static RouteSearchResult? BuildLocationTargetResult(
            RouteSearchState current,
            SocialRouteDateEvidenceIndex dateIndex,
            string targetLocation,
            ISet<string> failures)
        {
            if (!dateIndex.TryGetMap(targetLocation, out var targetMap))
            {
                failures.Add(
                    "future_route_target_map_evidence_missing:" +
                    targetLocation);
                return null;
            }
            if (!targetMap.AccessibleOnCaptureDate)
            {
                failures.Add(
                    "future_route_target_map_inaccessible_on_date:" +
                    targetLocation);
                return null;
            }
            return RouteSearchResult.Produced(
                current.ArrivalMinutes,
                current.Path,
                current.Segments);
        }

        private static string StateKey(string location, int x, int y) =>
            location + "|" + x + "|" + y;

        private static string FailureClass(string failure)
        {
            var separator = failure.IndexOf(':');
            return separator < 0 ? failure : failure.Substring(0, separator);
        }

        private static string PathKey(IEnumerable<TransparentRouteEdge> path) =>
            string.Join(">", path.Select(edge =>
                edge.FromLocation + ":" + edge.FromX + "," + edge.FromY +
                "->" + edge.TargetLocation + ":" + edge.TargetX + "," + edge.TargetY));

        private static int ToMinutes(int time) => time / 100 * 60 + time % 100;

        private sealed class RouteSearchState
        {
            public RouteSearchState(
                string location,
                int x,
                int y,
                int arrivalMinutes,
                TransparentRouteEdge[] path,
                FutureRouteSegmentEvidence[] segments)
            {
                Location = location;
                X = x;
                Y = y;
                ArrivalMinutes = arrivalMinutes;
                Path = path;
                Segments = segments;
            }

            public string Location { get; }

            public int X { get; }

            public int Y { get; }

            public int ArrivalMinutes { get; }

            public TransparentRouteEdge[] Path { get; }

            public FutureRouteSegmentEvidence[] Segments { get; }
        }

        private sealed class RouteSearchResult
        {
            private RouteSearchResult()
            {
            }

            public bool Success { get; private init; }

            public int ArrivalMinutes { get; private init; }

            public TransparentRouteEdge[] Path { get; private init; } =
                Array.Empty<TransparentRouteEdge>();

            public FutureRouteSegmentEvidence[] Segments { get; private init; } =
                Array.Empty<FutureRouteSegmentEvidence>();

            public FutureRouteApproachEvidence? FinalApproach { get; private init; }

            public string[] BlockingReasons { get; private init; } =
                Array.Empty<string>();

            public static RouteSearchResult Produced(
                int arrivalMinutes,
                TransparentRouteEdge[] path,
                FutureRouteSegmentEvidence[] segments,
                FutureRouteApproachEvidence? finalApproach = null) => new()
            {
                Success = true,
                ArrivalMinutes = arrivalMinutes,
                Path = path,
                Segments = segments,
                FinalApproach = finalApproach
            };

            public static RouteSearchResult Blocked(string[] reasons) => new()
            {
                BlockingReasons = reasons
            };
        }
    }
}
