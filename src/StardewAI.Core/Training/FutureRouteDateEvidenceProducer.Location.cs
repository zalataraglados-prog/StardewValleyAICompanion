using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public sealed class FutureLocationRouteDateEvidenceRequest
    {
        public int TotalDays { get; set; }

        public string StartLocation { get; set; } = string.Empty;

        public int StartTileX { get; set; }

        public int StartTileY { get; set; }

        public int EarliestDepartureTime { get; set; }

        public string TargetLocation { get; set; } = string.Empty;
    }

    public sealed class FutureLocationRouteDateEvidenceProduction
    {
        public FutureRouteDateEvidenceProductionStatus Status { get; set; }

        public int? GuaranteedArrivalByTime { get; set; }

        public FutureRouteTravelTimingEvidenceKind TimingEvidenceKind { get; set; } =
            FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;

        public string TimingEvidenceId { get; set; } = string.Empty;

        public TransparentRouteEdge[] Path { get; set; } =
            Array.Empty<TransparentRouteEdge>();

        public FutureRouteSegmentEvidence[] SegmentEvidence { get; set; } =
            Array.Empty<FutureRouteSegmentEvidence>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "current_date_full_connector_path_to_target_location_without_terminal_local_approach";
    }

    public sealed partial class FutureRouteDateEvidenceProducer
    {
        public FutureLocationRouteDateEvidenceProduction ProduceLocationArrival(
            JsonElement routeGraph,
            JsonElement socialRouteDateEvidence,
            FutureLocationRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing) =>
            ProduceLocationArrivals(
                routeGraph,
                socialRouteDateEvidence,
                new[] { request },
                timing).Single();

        public FutureLocationRouteDateEvidenceProduction[] ProduceLocationArrivals(
            JsonElement routeGraph,
            JsonElement socialRouteDateEvidence,
            IEnumerable<FutureLocationRouteDateEvidenceRequest> requests,
            FutureRouteTimingCalibration timing)
        {
            var materialized = requests?.ToArray() ??
                Array.Empty<FutureLocationRouteDateEvidenceRequest>();
            if (materialized.Length == 0)
                return Array.Empty<FutureLocationRouteDateEvidenceProduction>();

            var totalDays = materialized[0].TotalDays;
            var inputBlocks = materialized
                .Select(request => ValidateLocationInputs(request, timing))
                .ToArray();
            var hasDateMismatch = materialized.Any(request =>
                request.TotalDays != totalDays);
            if (inputBlocks.Any(reason => reason is not null) || hasDateMismatch)
            {
                return inputBlocks
                    .Select(reason => BlockedLocation(
                        reason ??
                        "future_location_route_batch_contains_invalid_request"))
                    .ToArray();
            }
            if (!TryCreateContext(
                    routeGraph,
                    socialRouteDateEvidence,
                    totalDays,
                    out var context,
                    out var contextBlocks))
            {
                return materialized
                    .Select(_ => BlockedLocation(contextBlocks))
                    .ToArray();
            }

            return materialized
                .Select(request => ProduceLocationArrival(
                    context,
                    request,
                    timing))
                .ToArray();
        }

        private static FutureLocationRouteDateEvidenceProduction
            ProduceLocationArrival(
                FutureRouteDateEvidenceContext context,
                FutureLocationRouteDateEvidenceRequest request,
                FutureRouteTimingCalibration timing)
        {
            var search = SearchFeasibleLocationRoute(
                context.Graph,
                context.DateIndex,
                context.ConnectorTiles,
                request,
                timing);
            if (!search.Success)
                return BlockedLocation(search.BlockingReasons);

            return new FutureLocationRouteDateEvidenceProduction
            {
                Status = FutureRouteDateEvidenceProductionStatus.Produced,
                GuaranteedArrivalByTime = FromMinutes(search.ArrivalMinutes),
                TimingEvidenceKind = timing.EvidenceKind,
                TimingEvidenceId = timing.EvidenceId,
                Path = search.Path,
                SegmentEvidence = search.Segments
            };
        }

        private static string? ValidateLocationInputs(
            FutureLocationRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing)
        {
            if (request is null || timing is null)
                return "future_location_route_evidence_input_missing";
            if (request.TotalDays < 0 ||
                request.TotalDays != timing.TotalDays ||
                string.IsNullOrWhiteSpace(request.StartLocation) ||
                string.IsNullOrWhiteSpace(request.TargetLocation) ||
                !IsValidTime(request.EarliestDepartureTime))
            {
                return "future_location_route_evidence_request_invalid";
            }
            if (!timing.StateComplete ||
                string.IsNullOrWhiteSpace(timing.EvidenceId) ||
                timing.GameMinuteNumeratorPerTile <= 0 ||
                timing.GameMinuteDenominatorPerTile <= 0 ||
                timing.ConnectorTransitionGameMinutes < 0)
            {
                return "future_route_timing_calibration_incomplete";
            }
            return null;
        }

        private static FutureLocationRouteDateEvidenceProduction
            BlockedLocation(string reason) =>
            BlockedLocation(new[] { reason });

        private static FutureLocationRouteDateEvidenceProduction
            BlockedLocation(string[] reasons) => new()
            {
                Status = FutureRouteDateEvidenceProductionStatus.Blocked,
                BlockingReasons = reasons
            };

        private static int FromMinutes(int minutes) =>
            minutes / 60 * 100 + minutes % 60;
    }
}
