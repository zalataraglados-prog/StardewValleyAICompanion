using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public enum FutureRouteAccessResolutionStatus
    {
        Exact,
        ConservativeUpperBound,
        Blocked
    }

    public enum FutureRouteTravelTimingEvidenceKind
    {
        ExactDuration,
        ConservativeUpperBound
    }

    public sealed class FutureRouteSegmentEvidence
    {
        public int TotalDays { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string FromLocation { get; set; } = string.Empty;

        public int FromTileX { get; set; }

        public int FromTileY { get; set; }

        public string TargetLocation { get; set; } = string.Empty;

        public int TargetTileX { get; set; }

        public int TargetTileY { get; set; }

        public int ApproachFromTileX { get; set; }

        public int ApproachFromTileY { get; set; }

        public int ApproachTravelGameMinutes { get; set; }

        public int ConnectorTransitionGameMinutes { get; set; }

        public FutureRouteTravelTimingEvidenceKind TimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ExactDuration;

        public string TimingEvidenceId { get; set; } =
            "caller_supplied_exact_duration";

        public bool TraversabilityComplete { get; set; }

        public bool GateStateComplete { get; set; }

        public bool AllowedOnDate { get; set; }

        public int? OpenTime { get; set; }

        public int? CloseTimeExclusive { get; set; }
    }

    public sealed class FutureRouteApproachEvidence
    {
        public int TotalDays { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int FromTileX { get; set; }

        public int FromTileY { get; set; }

        public int TargetTileX { get; set; }

        public int TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int TravelGameMinutes { get; set; }

        public FutureRouteTravelTimingEvidenceKind TimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ExactDuration;

        public string TimingEvidenceId { get; set; } =
            "caller_supplied_exact_duration";

        public bool TraversabilityComplete { get; set; }
    }

    public sealed class FutureRouteAccessScenario
    {
        public int TotalDays { get; set; }

        public string StartLocation { get; set; } = string.Empty;

        public int StartTileX { get; set; }

        public int StartTileY { get; set; }

        public int EarliestDepartureTime { get; set; }

        public FutureRouteSegmentEvidence[]? SegmentEvidence { get; set; }

        public FutureRouteApproachEvidence[]? ApproachEvidence { get; set; }

        public TransparentRouteEdge[][]? ProducedPaths { get; set; }
    }

    public sealed class FutureRouteAccessResolution
    {
        public FutureRouteAccessResolutionStatus Status { get; set; }

        public string TargetLocation { get; set; } = string.Empty;

        public int TargetTileX { get; set; }

        public int TargetTileY { get; set; }

        public int? StandTileX { get; set; }

        public int? StandTileY { get; set; }

        public int? EarliestArrivalTime { get; set; }

        public int? GuaranteedArrivalByTime { get; set; }

        public FutureRouteTravelTimingEvidenceKind TimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ExactDuration;

        public int WaitGameMinutes { get; set; }

        public TransparentRouteEdge[] Path { get; set; } =
            Array.Empty<TransparentRouteEdge>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "future_date_bound_route_topology_gate_traversability_and_timing_evidence";
    }

    public sealed class FutureRouteAccessWindowResolver
    {
        public FutureRouteAccessResolution Resolve(
            JsonElement routeGraph,
            FutureRouteAccessScenario scenario,
            string targetLocation,
            int targetTileX,
            int targetTileY)
        {
            if (scenario is null)
                return Blocked(targetLocation, targetTileX, targetTileY, "future_route_scenario_missing");
            if (scenario.TotalDays < 0 ||
                string.IsNullOrWhiteSpace(scenario.StartLocation) ||
                string.IsNullOrWhiteSpace(targetLocation) ||
                !IsValidTime(scenario.EarliestDepartureTime) ||
                scenario.SegmentEvidence is null ||
                scenario.ApproachEvidence is null)
            {
                return Blocked(targetLocation, targetTileX, targetTileY, "future_route_scenario_incomplete");
            }
            if (!TransparentRouteGraphResolver.TryCreate(routeGraph, out var graph))
                return Blocked(targetLocation, targetTileX, targetTileY, "future_route_graph_invalid");

            var paths = scenario.ProducedPaths is not null
                ? scenario.ProducedPaths
                    .Where(path => path is not null &&
                        path.All(graph.ContainsEdge))
                    .ToArray()
                : graph.FindShortestPathsByFirstEdge(
                    scenario.StartLocation,
                    targetLocation);
            if (paths.Length == 0)
            {
                return Blocked(
                    targetLocation,
                    targetTileX,
                    targetTileY,
                    scenario.ProducedPaths is null
                        ? "future_route_graph_path_missing"
                        : "future_route_produced_path_invalid");
            }

            var successful = new List<FutureRouteAccessResolution>();
            var failures = new List<string>();
            foreach (var path in paths)
            {
                var resolved = ResolvePath(
                    scenario,
                    path,
                    targetLocation,
                    targetTileX,
                    targetTileY);
                if (resolved.Status != FutureRouteAccessResolutionStatus.Blocked)
                    successful.Add(resolved);
                else
                    failures.AddRange(resolved.BlockingReasons);
            }

            return successful
                .OrderBy(result => ToMinutes(result.GuaranteedArrivalByTime!.Value))
                .ThenBy(result => result.Status == FutureRouteAccessResolutionStatus.Exact ? 0 : 1)
                .ThenBy(result => result.WaitGameMinutes)
                .ThenBy(result => result.Path.Length)
                .FirstOrDefault() ?? Blocked(
                    targetLocation,
                    targetTileX,
                    targetTileY,
                    failures.Count == 0
                        ? new[] { "future_route_evidence_missing" }
                        : failures.Distinct(StringComparer.Ordinal).ToArray());
        }

        private static FutureRouteAccessResolution ResolvePath(
            FutureRouteAccessScenario scenario,
            TransparentRouteEdge[] path,
            string targetLocation,
            int targetTileX,
            int targetTileY)
        {
            var currentLocation = scenario.StartLocation;
            var currentX = scenario.StartTileX;
            var currentY = scenario.StartTileY;
            var currentMinutes = ToMinutes(scenario.EarliestDepartureTime);
            var waitMinutes = 0;
            var timingKind = FutureRouteTravelTimingEvidenceKind.ExactDuration;

            foreach (var edge in path)
            {
                if (!edge.FromX.HasValue || !edge.FromY.HasValue ||
                    !edge.TargetX.HasValue || !edge.TargetY.HasValue)
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        "future_route_edge_coordinates_incomplete");
                }
                var matches = scenario.SegmentEvidence!.Where(evidence =>
                    evidence.TotalDays == scenario.TotalDays &&
                    string.Equals(evidence.Kind, edge.Kind, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(evidence.FromLocation, edge.FromLocation, StringComparison.OrdinalIgnoreCase) &&
                    evidence.FromTileX == edge.FromX.Value &&
                    evidence.FromTileY == edge.FromY.Value &&
                    string.Equals(evidence.TargetLocation, edge.TargetLocation, StringComparison.OrdinalIgnoreCase) &&
                    evidence.TargetTileX == edge.TargetX.Value &&
                    evidence.TargetTileY == edge.TargetY.Value &&
                    evidence.ApproachFromTileX == currentX &&
                    evidence.ApproachFromTileY == currentY)
                    .ToArray();
                if (matches.Length != 1)
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        matches.Length == 0
                            ? "future_route_segment_evidence_missing"
                            : "future_route_segment_evidence_ambiguous");
                }

                var evidence = matches[0];
                if (!string.Equals(currentLocation, edge.FromLocation, StringComparison.OrdinalIgnoreCase))
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        "future_route_segment_chain_disconnected");
                }
                if (!evidence.TraversabilityComplete || !evidence.GateStateComplete)
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        "future_route_segment_state_incomplete");
                }
                if (!evidence.AllowedOnDate)
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        "future_route_segment_not_allowed_on_date");
                }
                if (evidence.ApproachTravelGameMinutes < 0 ||
                    evidence.ConnectorTransitionGameMinutes < 0 ||
                    string.IsNullOrWhiteSpace(evidence.TimingEvidenceId) ||
                    evidence.OpenTime.HasValue != evidence.CloseTimeExclusive.HasValue ||
                    (evidence.OpenTime.HasValue &&
                     (!IsValidTime(evidence.OpenTime.Value) ||
                      !IsValidTime(evidence.CloseTimeExclusive!.Value) ||
                      ToMinutes(evidence.OpenTime.Value) >=
                      ToMinutes(evidence.CloseTimeExclusive.Value))))
                {
                    return Blocked(targetLocation, targetTileX, targetTileY,
                        "future_route_segment_timing_invalid");
                }

                if (evidence.TimingEvidenceKind ==
                    FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound)
                {
                    timingKind = FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;
                }

                currentMinutes += evidence.ApproachTravelGameMinutes;
                if (evidence.OpenTime.HasValue)
                {
                    var openMinutes = ToMinutes(evidence.OpenTime.Value);
                    var closeMinutes = ToMinutes(evidence.CloseTimeExclusive!.Value);
                    if (currentMinutes < openMinutes)
                    {
                        waitMinutes += openMinutes - currentMinutes;
                        currentMinutes = openMinutes;
                    }
                    if (currentMinutes >= closeMinutes)
                    {
                        return Blocked(targetLocation, targetTileX, targetTileY,
                            "future_route_segment_gate_closed_before_arrival");
                    }
                }

                currentMinutes += evidence.ConnectorTransitionGameMinutes;
                currentLocation = edge.TargetLocation;
                currentX = edge.TargetX.Value;
                currentY = edge.TargetY.Value;
            }

            var approaches = scenario.ApproachEvidence!.Where(evidence =>
                evidence.TotalDays == scenario.TotalDays &&
                string.Equals(evidence.LocationName, targetLocation, StringComparison.OrdinalIgnoreCase) &&
                evidence.FromTileX == currentX &&
                evidence.FromTileY == currentY &&
                evidence.TargetTileX == targetTileX &&
                evidence.TargetTileY == targetTileY)
                .ToArray();
            if (approaches.Length != 1)
            {
                return Blocked(targetLocation, targetTileX, targetTileY,
                    approaches.Length == 0
                        ? "future_route_target_approach_evidence_missing"
                        : "future_route_target_approach_evidence_ambiguous");
            }
            var approach = approaches[0];
            if (!approach.StandTileX.HasValue ||
                !approach.StandTileY.HasValue ||
                Math.Abs(approach.StandTileX.Value - targetTileX) +
                    Math.Abs(approach.StandTileY.Value - targetTileY) != 1)
            {
                return Blocked(targetLocation, targetTileX, targetTileY,
                    "future_route_target_stand_evidence_invalid");
            }
            if (!approach.TraversabilityComplete ||
                approach.TravelGameMinutes < 0 ||
                string.IsNullOrWhiteSpace(approach.TimingEvidenceId))
            {
                return Blocked(targetLocation, targetTileX, targetTileY,
                    "future_route_target_approach_incomplete");
            }

            if (approach.TimingEvidenceKind ==
                FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound)
            {
                timingKind = FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;
            }

            currentMinutes += approach.TravelGameMinutes;
            if (currentMinutes > ToMinutes(2800))
            {
                return Blocked(targetLocation, targetTileX, targetTileY,
                    "future_route_arrival_outside_supported_day");
            }
            var arrivalTime = FromMinutes(currentMinutes);
            return new FutureRouteAccessResolution
            {
                Status = timingKind == FutureRouteTravelTimingEvidenceKind.ExactDuration
                    ? FutureRouteAccessResolutionStatus.Exact
                    : FutureRouteAccessResolutionStatus.ConservativeUpperBound,
                TargetLocation = targetLocation,
                TargetTileX = targetTileX,
                TargetTileY = targetTileY,
                StandTileX = approach.StandTileX,
                StandTileY = approach.StandTileY,
                EarliestArrivalTime = timingKind == FutureRouteTravelTimingEvidenceKind.ExactDuration
                    ? arrivalTime
                    : null,
                GuaranteedArrivalByTime = arrivalTime,
                TimingEvidenceKind = timingKind,
                WaitGameMinutes = waitMinutes,
                Path = path
            };
        }

        private static FutureRouteAccessResolution Blocked(
            string location,
            int tileX,
            int tileY,
            string reason) => Blocked(location, tileX, tileY, new[] { reason });

        private static FutureRouteAccessResolution Blocked(
            string location,
            int tileX,
            int tileY,
            string[] reasons) => new()
        {
            Status = FutureRouteAccessResolutionStatus.Blocked,
            TargetLocation = location,
            TargetTileX = tileX,
            TargetTileY = tileY,
            BlockingReasons = reasons
        };

        private static bool IsValidTime(int value) =>
            value >= 0 && value <= 2800 && value % 100 < 60;

        private static int ToMinutes(int time) => time / 100 * 60 + time % 100;

        private static int FromMinutes(int minutes) => minutes / 60 * 100 + minutes % 60;
    }
}
