using System;
using System.Linq;

namespace StardewAI.Core.Training
{
    public sealed partial class NpcFutureScheduleResolver
    {
        private static bool TryResolvePathTiming(
            NpcFutureScheduleScenario scenario,
            string selectedScheduleKey,
            int scheduleEntryOrdinal,
            string targetLocation,
            int targetTileX,
            int targetTileY,
            int facingDirection,
            out int travelGameMinutes,
            out NpcSchedulePathTimingEvidence? timing,
            out string reason)
        {
            travelGameMinutes = 0;
            timing = null;
            reason = string.Empty;
            if (!scenario.RealMillisecondsPerGameTenMinutes.HasValue ||
                scenario.RealMillisecondsPerGameTenMinutes.Value <= 0 ||
                scenario.NativePathTimings is null)
            {
                reason = "future_schedule_arrival_time_requires_native_path_length";
                return false;
            }

            var matches = scenario.NativePathTimings.Where(candidate =>
                    candidate is not null &&
                    string.Equals(candidate.ScheduleKey, selectedScheduleKey, StringComparison.Ordinal) &&
                    candidate.ScheduleEntryOrdinal == scheduleEntryOrdinal &&
                    string.Equals(candidate.TargetLocationName, targetLocation, StringComparison.Ordinal) &&
                    candidate.TargetTileX == targetTileX &&
                    candidate.TargetTileY == targetTileY &&
                    candidate.FacingDirection == facingDirection)
                .ToArray();
            if (matches.Length == 0)
            {
                reason = "future_schedule_arrival_path_timing_evidence_missing";
                return false;
            }
            if (matches.Length != 1)
            {
                reason = "future_schedule_arrival_path_timing_evidence_ambiguous";
                return false;
            }

            timing = matches[0];
            if (timing.NativeRoutePointCount < 0 ||
                timing.AdjacentRoutePixelDistance < 0 ||
                timing.AdjacentRoutePixelDistance % 64 != 0)
            {
                reason = "future_schedule_arrival_path_timing_evidence_invalid";
                timing = null;
                return false;
            }

            var nativeDistanceHalfPixels = timing.AdjacentRoutePixelDistance / 2;
            var nativeRealMinuteDenominator =
                scenario.RealMillisecondsPerGameTenMinutes.Value / 1000 * 60;
            if (nativeRealMinuteDenominator <= 0)
            {
                reason = "future_schedule_real_time_scale_invalid";
                timing = null;
                return false;
            }
            travelGameMinutes = checked((int)Math.Round(
                (float)nativeDistanceHalfPixels / nativeRealMinuteDenominator) * 10);
            return true;
        }

        private static int ResolveArrivalDepartureTime(
            int requestedArrivalTime,
            int previousDepartureTime,
            int travelGameMinutes)
        {
            var arrivalMinutes = ConvertTimeToMinutes(requestedArrivalTime);
            return Math.Max(
                ConvertMinutesToTime(arrivalMinutes - travelGameMinutes),
                previousDepartureTime);
        }

        private static int AddGameMinutes(int time, int minutes) =>
            ConvertMinutesToTime(ConvertTimeToMinutes(time) + minutes);

        private static int ConvertTimeToMinutes(int time) => time / 100 * 60 + time % 100;

        private static int ConvertMinutesToTime(int minutes) => minutes / 60 * 100 + minutes % 60;
    }
}
