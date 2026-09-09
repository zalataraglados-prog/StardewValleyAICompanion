using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentLoadedSchedulePresenceResolution
    {
        public string Status { get; set; } = "blocked";

        public string ProjectionStatus { get; set; } = "Blocked";

        public NpcFuturePresenceWindowResolution? Presence { get; set; }

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class NpcCurrentLoadedSchedulePresenceResolver
    {
        public NpcCurrentLoadedSchedulePresenceResolution Resolve(
            JsonElement snapshot,
            string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "time",
                    "total_days",
                    out var totalDaysNode) ||
                !totalDaysNode.TryGetInt32(out var totalDays) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "time",
                    "time",
                    out var gameTimeNode) ||
                !gameTimeNode.TryGetInt32(out var gameTime) ||
                !IsCurrentDayTime(gameTime) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "npcs",
                    "schedule_catalog",
                    out var catalog) ||
                !catalog.TryGetProperty(
                    "current_selection_context",
                    out var context) ||
                context.ValueKind != JsonValueKind.Object ||
                ReadInt(context, "capture_total_days") != totalDays ||
                ReadInt(context, "game_time") != gameTime ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "npcs",
                    "schedules",
                    out var schedules) ||
                schedules.ValueKind != JsonValueKind.Array)
            {
                return Blocked(
                    "current_loaded_schedule_snapshot_inputs_incomplete");
            }

            var realMillisecondsPerTenMinutes = ReadInt(
                context,
                "real_milliseconds_per_game_ten_minutes");
            if (!realMillisecondsPerTenMinutes.HasValue ||
                realMillisecondsPerTenMinutes.Value <= 0)
            {
                return Blocked(
                    "current_loaded_schedule_real_time_scale_invalid");
            }

            var matches = schedules.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(row, "name"),
                        npcName,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                return Blocked(matches.Length == 0
                    ? "current_loaded_schedule_npc_missing"
                    : "current_loaded_schedule_npc_ambiguous");
            }

            var schedule = matches[0];
            var scheduleKey = ReadString(schedule, "schedule_key");
            if (ReadBool(schedule, "follow_schedule") != true ||
                ReadBool(schedule, "ignore_schedule_today") != false ||
                ReadBool(schedule, "schedule_loaded") != true ||
                scheduleKey.Length == 0 ||
                !schedule.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array ||
                entries.GetArrayLength() == 0)
            {
                return Blocked("current_loaded_native_schedule_not_active");
            }

            var endpoints = new List<NpcFutureScheduleEndpoint>();
            var usedTimes = new HashSet<int>();
            var ordinal = 0;
            var previousMinutes = -1;
            foreach (var row in entries.EnumerateArray())
            {
                var time = ReadInt(row, "time");
                var location = ReadString(row, "target_location_name");
                var x = ReadInt(row, "target_tile_x");
                var y = ReadInt(row, "target_tile_y");
                var facing = ReadInt(row, "facing_direction");
                var routeCount = ReadInt(row, "route_count");
                var adjacentPixels = ReadInt(
                    row,
                    "adjacent_route_pixel_distance");
                if (!time.HasValue || !IsNativeScheduleTime(time.Value) ||
                    location.Length == 0 ||
                    !x.HasValue || x.Value < 0 ||
                    !y.HasValue || y.Value < 0 ||
                    !facing.HasValue ||
                    !routeCount.HasValue || routeCount.Value < 0 ||
                    !adjacentPixels.HasValue || adjacentPixels.Value < 0 ||
                    adjacentPixels.Value % 64 != 0 ||
                    !usedTimes.Add(time.Value) ||
                    ToMinutes(time.Value) <= previousMinutes)
                {
                    return Blocked(
                        "current_loaded_schedule_entry_invalid:" + ordinal);
                }

                var travelGameMinutes = ResolveTravelGameMinutes(
                    adjacentPixels.Value,
                    realMillisecondsPerTenMinutes.Value);
                var arrivalTime = AddGameMinutes(time.Value, travelGameMinutes);
                if (!IsNativeScheduleTime(arrivalTime))
                {
                    return Blocked(
                        "current_loaded_schedule_arrival_time_invalid:" +
                        ordinal);
                }

                var behavior = ReadString(row, "end_behavior");
                endpoints.Add(new NpcFutureScheduleEndpoint
                {
                    CommandIndex = ordinal,
                    ScheduledDepartureTime = time.Value,
                    ScheduleEntryOrdinal = ordinal,
                    NativeRoutePointCount = routeCount.Value,
                    NativeAdjacentRoutePixelDistance = adjacentPixels.Value,
                    NativeTravelGameMinutes = travelGameMinutes,
                    ScheduledArrivalTime = arrivalTime,
                    LocationName = location,
                    TileX = x.Value,
                    TileY = y.Value,
                    FacingDirection = facing.Value,
                    EndBehavior = behavior,
                    EndMessage = ReadString(row, "end_message"),
                    EndBehaviorComplete = !behavior.EndsWith(
                        "_sleep",
                        StringComparison.Ordinal)
                });
                previousMinutes = ToMinutes(time.Value);
                ordinal++;
            }

            var projection = new NpcFutureScheduleResolution
            {
                Status = NpcFutureScheduleResolutionStatus.Exact,
                NpcName = npcName,
                SelectedScheduleKey = scheduleKey,
                ResolvedScheduleKey = scheduleKey,
                ProjectionScope =
                    "current_loaded_native_schedule_endpoints_and_timing",
                NativePathRoutesResolved = false,
                ArrivalTimesResolved = true,
                TravelTimesResolved = true,
                EndpointBehaviorsResolved = true,
                ResolutionTrace = new[]
                {
                    "native_loaded_schedule:" + scheduleKey,
                    "capture_time:" + gameTime
                },
                Endpoints = endpoints.ToArray()
            };
            var presence = new NpcFuturePresenceWindowResolver().Resolve(
                projection,
                gameTime);
            if (presence.Status !=
                    NpcFuturePresenceWindowResolutionStatus.Exact)
            {
                return Blocked(presence.BlockingReasons);
            }

            return new NpcCurrentLoadedSchedulePresenceResolution
            {
                Status = "pass",
                ProjectionStatus = "ExactCurrentLoadedNativeSchedule",
                Presence = presence
            };
        }

        private static int ResolveTravelGameMinutes(
            int adjacentRoutePixelDistance,
            int realMillisecondsPerGameTenMinutes)
        {
            var nativeDistanceHalfPixels =
                adjacentRoutePixelDistance / 2;
            var nativeRealMinuteDenominator =
                realMillisecondsPerGameTenMinutes / 1000 * 60;
            if (nativeRealMinuteDenominator <= 0)
                return 0;
            return checked((int)Math.Round(
                (float)nativeDistanceHalfPixels /
                nativeRealMinuteDenominator) * 10);
        }

        private static NpcCurrentLoadedSchedulePresenceResolution Blocked(
            params string[] reasons) => new()
        {
            BlockingReasons = reasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

        private static bool IsCurrentDayTime(int value) =>
            value >= 600 && value <= 2600 && value % 100 < 60;

        private static bool IsNativeScheduleTime(int value) =>
            value >= 0 && value <= 2800 && value % 100 < 60;

        private static int ToMinutes(int time) =>
            time / 100 * 60 + time % 100;

        private static int AddGameMinutes(int time, int minutes)
        {
            var total = ToMinutes(time) + minutes;
            return total / 60 * 100 + total % 60;
        }

        private static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static int? ReadInt(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value)
                ? value
                : null;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;
    }
}
