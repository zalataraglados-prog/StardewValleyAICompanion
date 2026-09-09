using System;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure
{
    public sealed class MasterAnglerRouteTimingValidation
    {
        public string Status { get; init; } = string.Empty;

        public int GuaranteedArrivalByTime { get; init; }

        public int RemainingRouteGameMinutes { get; init; }

        public int PathEdgeCount { get; init; }

        public string TimingEvidenceId { get; init; } = string.Empty;

        public string TimingEvidenceSha256 { get; init; } = string.Empty;
    }

    public static partial class MasterAnglerRouteTimingValidator
    {
        public const string CalibrationPathParameter =
            "master_angler_route_timing_calibration_path";
        public const string CalibrationSha256Parameter =
            "master_angler_route_timing_calibration_sha256";
        public const string ValidationRequiredParameter =
            "master_angler_full_route_timing_validation_required";

        public static bool TryResolve(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] parameters,
            string targetLocation,
            out MasterAnglerRouteTimingValidation validation,
            out string rejectionReason)
        {
            validation = new MasterAnglerRouteTimingValidation();
            rejectionReason = string.Empty;
            if (!TryRead(parameters, CalibrationPathParameter, out var path) ||
                !TryRead(parameters, CalibrationSha256Parameter, out var expectedHash) ||
                !TryRead(parameters, ValidationRequiredParameter, out var required) ||
                required != "true" ||
                !IsSha256(expectedHash))
            {
                rejectionReason =
                    "master_angler_route_timing_contract_incomplete_or_invalid";
                return false;
            }

            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            if (string.IsNullOrWhiteSpace(currentLocation) ||
                string.IsNullOrWhiteSpace(targetLocation) ||
                !IsValidTime(currentTime))
            {
                rejectionReason =
                    "master_angler_route_timing_snapshot_position_invalid";
                return false;
            }
            if (string.Equals(
                    currentLocation,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                validation = new MasterAnglerRouteTimingValidation
                {
                    Status = "same_location_no_connector_route_required",
                    GuaranteedArrivalByTime = currentTime,
                    RemainingRouteGameMinutes = 0,
                    PathEdgeCount = 0,
                    TimingEvidenceId = "same_location_no_connector",
                    TimingEvidenceSha256 = expectedHash
                };
                return true;
            }

            if (!TryReadArtifact(
                    path,
                    expectedHash,
                    out var artifactJson,
                    out rejectionReason))
            {
                return false;
            }
            var movement = ReadStateFieldValue(
                snapshot,
                "player",
                "movement_timing_context");
            var routeGraph = ReadStateFieldValue(
                snapshot,
                "locations",
                "route_graph");
            var dateEvidence = ReadStateFieldValue(
                snapshot,
                "locations",
                "social_route_date_evidence");
            var totalDays = ReadStateFieldInt(snapshot, "time", "total_days");
            var tileX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var tileY = ReadStateFieldInt(snapshot, "player", "tile_y");
            if (!movement.HasValue ||
                !routeGraph.HasValue ||
                !dateEvidence.HasValue ||
                totalDays < 0 ||
                tileX < 0 ||
                tileY < 0 ||
                string.IsNullOrWhiteSpace(snapshot.GameVersion))
            {
                rejectionReason =
                    "master_angler_full_route_transparent_evidence_missing";
                return false;
            }

            var timingLoad = new FutureRouteTimingCalibrationLoader().Load(
                artifactJson,
                movement.Value,
                snapshot.GameVersion,
                totalDays);
            if (timingLoad.Status != FutureRouteTimingCalibrationLoadStatus.Loaded ||
                timingLoad.Calibration is null)
            {
                rejectionReason = AppendFirstReason(
                    "master_angler_route_timing_calibration_blocked",
                    timingLoad.BlockingReasons);
                return false;
            }

            var route = new FutureRouteDateEvidenceProducer()
                .ProduceLocationArrival(
                    routeGraph.Value,
                    dateEvidence.Value,
                    new FutureLocationRouteDateEvidenceRequest
                    {
                        TotalDays = totalDays,
                        StartLocation = currentLocation,
                        StartTileX = tileX,
                        StartTileY = tileY,
                        EarliestDepartureTime = currentTime,
                        TargetLocation = targetLocation
                    },
                    timingLoad.Calibration);
            if (route.Status != FutureRouteDateEvidenceProductionStatus.Produced ||
                !route.GuaranteedArrivalByTime.HasValue ||
                route.Path.Length == 0 ||
                route.TimingEvidenceKind !=
                    FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound ||
                !string.Equals(
                    route.TimingEvidenceId,
                    timingLoad.Calibration.EvidenceId,
                    StringComparison.Ordinal))
            {
                rejectionReason = AppendFirstReason(
                    "master_angler_full_route_timing_unavailable",
                    route.BlockingReasons);
                return false;
            }

            var remainingMinutes = GameClockBudgetPolicy.ClockMinutesBetween(
                currentTime,
                route.GuaranteedArrivalByTime.Value);
            if (remainingMinutes < 0)
            {
                rejectionReason =
                    "master_angler_full_route_timing_negative_duration";
                return false;
            }
            validation = new MasterAnglerRouteTimingValidation
            {
                Status = "conservative_current_date_full_connector_path",
                GuaranteedArrivalByTime = route.GuaranteedArrivalByTime.Value,
                RemainingRouteGameMinutes = remainingMinutes,
                PathEdgeCount = route.Path.Length,
                TimingEvidenceId = route.TimingEvidenceId,
                TimingEvidenceSha256 = timingLoad.Calibration.EvidenceSha256
            };
            return true;
        }

        private static bool IsValidTime(int value) =>
            value >= 600 && value <= 2600 && value % 100 < 60;
    }
}
