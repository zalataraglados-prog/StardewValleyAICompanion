using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static RelocationRouteEvidence?
            BuildRelocationRouteEvidence(
                SnapshotEnvelope snapshot,
                ResolvedRoutePlan routePlan,
                EventCandidate firstConnector)
        {
            if (routePlan.Path.Length == 0 ||
                firstConnector.EstimatedTicks < 60 ||
                firstConnector.EstimatedTicks % 60 != 0)
            {
                return null;
            }

            var segments =
                new List<MachineRelocationRouteSegment>();
            for (var index = 0;
                 index < routePlan.Path.Length;
                 index++)
            {
                var edge = routePlan.Path[index];
                if (!edge.FromX.HasValue ||
                    !edge.FromY.HasValue ||
                    !edge.TargetX.HasValue ||
                    !edge.TargetY.HasValue)
                {
                    return null;
                }

                int approachDistance;
                int estimatedTicks;
                if (index == 0)
                {
                    approachDistance =
                        firstConnector.EstimatedTicks / 60 - 1;
                    estimatedTicks =
                        firstConnector.EstimatedTicks;
                }
                else
                {
                    var previous = routePlan.Path[index - 1];
                    if (!previous.TargetX.HasValue ||
                        !previous.TargetY.HasValue)
                    {
                        return null;
                    }

                    var reachability =
                        MachineRelocationReachabilityProjectionReader
                            .Read(
                                snapshot,
                                edge.FromLocation,
                                previous.TargetX.Value,
                                previous.TargetY.Value);
                    if (reachability is null ||
                        !reachability.TryGetConnectorApproachDistance(
                            edge.FromX.Value,
                            edge.FromY.Value,
                            MachineRelocationReachabilityProjectionReader
                                .ReadResolvedConnectorTiles(
                                    snapshot,
                                    edge.FromLocation),
                            out approachDistance))
                    {
                        return null;
                    }
                    estimatedTicks =
                        checked((approachDistance + 1) * 60);
                }

                segments.Add(new MachineRelocationRouteSegment
                {
                    Index = index,
                    Kind = edge.Kind,
                    FromLocationId = edge.FromLocation,
                    FromTileX = edge.FromX.Value,
                    FromTileY = edge.FromY.Value,
                    TargetLocationId = edge.TargetLocation,
                    ArrivalTileX = edge.TargetX.Value,
                    ArrivalTileY = edge.TargetY.Value,
                    ApproachDistanceTiles = approachDistance,
                    EstimatedTicks = estimatedTicks
                });
            }

            var totalTicks = segments.Sum(segment =>
                checked(segment.EstimatedTicks));
            return new RelocationRouteEvidence(
                segments.ToArray(),
                totalTicks,
                segments[^1].ArrivalTileX,
                segments[^1].ArrivalTileY);
        }

        private sealed record RelocationRouteEvidence(
            MachineRelocationRouteSegment[] Segments,
            int EstimatedTicks,
            int FinalArrivalX,
            int FinalArrivalY);
    }
}
