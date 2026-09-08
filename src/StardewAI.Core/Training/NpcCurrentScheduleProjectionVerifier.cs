using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentScheduleProjectionVerification
    {
        public string Status { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string ProjectedSelectedScheduleKey { get; set; } = string.Empty;

        public string ProjectedResolvedScheduleKey { get; set; } = string.Empty;

        public string NativeScheduleKey { get; set; } = string.Empty;

        public int VerifiedEntryCount { get; set; }

        public int VerifiedArrivalTimeEntryCount { get; set; }

        public int VerifiedTravelTimeEntryCount { get; set; }

        public int[] NativeRoutePointCounts { get; set; } = Array.Empty<int>();

        public int[] NativeAdjacentRoutePixelDistances { get; set; } = Array.Empty<int>();

        public string[] Issues { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } = "current_loaded_native_schedule_only";
    }

    public sealed class NpcCurrentScheduleProjectionVerifier
    {
        public NpcCurrentScheduleProjectionVerification Verify(
            NpcFutureScheduleResolution projection,
            JsonElement loadedSchedules)
        {
            if (projection is null)
                return Blocked(string.Empty, "schedule_projection_missing");
            if (projection.Status != NpcFutureScheduleResolutionStatus.Exact)
                return Blocked(projection.NpcName, "schedule_projection_not_exact", projection);
            if (projection.NativePathRoutesResolved)
                return Blocked(projection.NpcName, "schedule_projection_claims_unverified_native_routes", projection);
            if (projection.Endpoints.Any(endpoint => endpoint.ArrivalTimeRequested) &&
                !projection.ArrivalTimesResolved)
            {
                return Blocked(projection.NpcName, "schedule_projection_arrival_timing_unresolved", projection);
            }
            if (loadedSchedules.ValueKind != JsonValueKind.Array)
                return Blocked(projection.NpcName, "native_loaded_schedules_missing", projection);

            var matchingRows = loadedSchedules.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    ReadString(row, "name") == projection.NpcName)
                .ToArray();
            if (matchingRows.Length != 1)
            {
                return Blocked(
                    projection.NpcName,
                    matchingRows.Length == 0
                        ? "native_loaded_schedule_npc_missing"
                        : "native_loaded_schedule_npc_duplicate",
                    projection);
            }

            var native = matchingRows[0];
            if (ReadBool(native, "ignore_schedule_today") != false)
                return Blocked(projection.NpcName, "native_schedule_ignored_or_state_unknown", projection);
            if (ReadBool(native, "follow_schedule") != true ||
                ReadBool(native, "schedule_loaded") != true ||
                !native.TryGetProperty("entries", out var nativeEntries) ||
                nativeEntries.ValueKind != JsonValueKind.Array)
            {
                return Blocked(projection.NpcName, "native_schedule_not_loaded", projection);
            }

            var nativeKey = ReadString(native, "schedule_key");
            if (!string.Equals(nativeKey, projection.SelectedScheduleKey, StringComparison.Ordinal))
                return Mismatch(projection, nativeKey, "native_schedule_key_mismatch");

            var projectedEntries = projection.Endpoints
                .Where(endpoint => !endpoint.IsInitialPosition)
                .OrderBy(endpoint => endpoint.ScheduledDepartureTime)
                .ToArray();
            var nativeEntryRows = nativeEntries.EnumerateArray().ToArray();
            if (projectedEntries.Length != nativeEntryRows.Length)
                return Mismatch(projection, nativeKey, "native_schedule_entry_count_mismatch");

            var routeCounts = new int[nativeEntryRows.Length];
            var adjacentRoutePixels = new int[nativeEntryRows.Length];
            for (var index = 0; index < nativeEntryRows.Length; index++)
            {
                var expected = projectedEntries[index];
                var actual = nativeEntryRows[index];
                if (actual.ValueKind != JsonValueKind.Object ||
                    ReadInt(actual, "time") != expected.ScheduledDepartureTime ||
                    !string.Equals(ReadString(actual, "target_location_name"), expected.LocationName, StringComparison.Ordinal) ||
                    ReadInt(actual, "target_tile_x") != expected.TileX ||
                    ReadInt(actual, "target_tile_y") != expected.TileY ||
                    ReadInt(actual, "facing_direction") != expected.FacingDirection)
                {
                    return Mismatch(projection, nativeKey, "native_schedule_endpoint_mismatch:" + index);
                }
                var routeCount = ReadInt(actual, "route_count");
                if (!routeCount.HasValue || routeCount.Value < 0)
                    return Mismatch(projection, nativeKey, "native_schedule_route_count_invalid:" + index);
                routeCounts[index] = routeCount.Value;
                var adjacentPixels = ReadInt(actual, "adjacent_route_pixel_distance");
                adjacentRoutePixels[index] = adjacentPixels ?? -1;
                if (expected.ArrivalTimeRequested &&
                    (!adjacentPixels.HasValue || adjacentPixels.Value < 0 ||
                     expected.NativeRoutePointCount != routeCount.Value ||
                     expected.NativeAdjacentRoutePixelDistance != adjacentPixels.Value))
                {
                    return Mismatch(projection, nativeKey, "native_schedule_arrival_path_evidence_mismatch:" + index);
                }
                if (projection.TravelTimesResolved &&
                    (!adjacentPixels.HasValue || adjacentPixels.Value < 0 ||
                     expected.NativeRoutePointCount != routeCount.Value ||
                     expected.NativeAdjacentRoutePixelDistance != adjacentPixels.Value ||
                     !expected.NativeTravelGameMinutes.HasValue ||
                     !expected.ScheduledArrivalTime.HasValue ||
                     expected.NativeTravelGameMinutes.Value < 0 ||
                     AddGameMinutes(
                         expected.ScheduledDepartureTime,
                         expected.NativeTravelGameMinutes.Value) !=
                     expected.ScheduledArrivalTime.Value))
                {
                    return Mismatch(projection, nativeKey, "native_schedule_travel_path_evidence_mismatch:" + index);
                }
            }

            return new NpcCurrentScheduleProjectionVerification
            {
                Status = "pass",
                NpcName = projection.NpcName,
                ProjectedSelectedScheduleKey = projection.SelectedScheduleKey,
                ProjectedResolvedScheduleKey = projection.ResolvedScheduleKey,
                NativeScheduleKey = nativeKey,
                VerifiedEntryCount = nativeEntryRows.Length,
                VerifiedArrivalTimeEntryCount = projectedEntries.Count(endpoint => endpoint.ArrivalTimeRequested),
                VerifiedTravelTimeEntryCount = projection.TravelTimesResolved
                    ? projectedEntries.Length
                    : 0,
                NativeRoutePointCounts = routeCounts,
                NativeAdjacentRoutePixelDistances = adjacentRoutePixels,
                Scope = "current_loaded_native_schedule_key_endpoints_route_counts_and_travel_timing"
            };
        }

        private static NpcCurrentScheduleProjectionVerification Blocked(
            string npcName,
            string issue,
            NpcFutureScheduleResolution? projection = null)
        {
            return new NpcCurrentScheduleProjectionVerification
            {
                Status = "blocked",
                NpcName = npcName,
                ProjectedSelectedScheduleKey = projection?.SelectedScheduleKey ?? string.Empty,
                ProjectedResolvedScheduleKey = projection?.ResolvedScheduleKey ?? string.Empty,
                Issues = new[] { issue }
            };
        }

        private static NpcCurrentScheduleProjectionVerification Mismatch(
            NpcFutureScheduleResolution projection,
            string nativeKey,
            string issue)
        {
            return new NpcCurrentScheduleProjectionVerification
            {
                Status = "mismatch",
                NpcName = projection.NpcName,
                ProjectedSelectedScheduleKey = projection.SelectedScheduleKey,
                ProjectedResolvedScheduleKey = projection.ResolvedScheduleKey,
                NativeScheduleKey = nativeKey,
                Issues = new[] { issue }
            };
        }

        private static string ReadString(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int? ReadInt(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var value)
                    ? value
                    : null;
        }

        private static bool? ReadBool(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }
            return property.GetBoolean();
        }

        private static int AddGameMinutes(int time, int minutes)
        {
            var total = time / 100 * 60 + time % 100 + minutes;
            return total / 60 * 100 + total % 60;
        }
    }
}
