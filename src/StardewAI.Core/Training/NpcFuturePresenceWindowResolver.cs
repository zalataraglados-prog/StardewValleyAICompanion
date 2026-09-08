using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Training
{
    public enum NpcFuturePresenceWindowResolutionStatus
    {
        Exact,
        NoSchedule,
        Blocked
    }

    public sealed class NpcFuturePresenceWindow
    {
        public int ScheduleEntryOrdinal { get; set; }

        public bool IsInitialPosition { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int FacingDirection { get; set; }

        public int WindowStartTime { get; set; }

        public int WindowEndTimeExclusive { get; set; }

        public bool HasStableInterval { get; set; }

        public bool EndpointBehaviorComplete { get; set; }
    }

    public sealed class NpcFuturePresenceWindowResolution
    {
        public NpcFuturePresenceWindowResolutionStatus Status { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public string SelectedScheduleKey { get; set; } = string.Empty;

        public string ResolvedScheduleKey { get; set; } = string.Empty;

        public NpcFuturePresenceWindow[] Windows { get; set; } =
            Array.Empty<NpcFuturePresenceWindow>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "native_timed_stationary_endpoint_windows_only";
    }

    public sealed class NpcFuturePresenceWindowResolver
    {
        public NpcFuturePresenceWindowResolution Resolve(
            NpcFutureScheduleResolution schedule,
            int dayStartTime = 600,
            int dayEndTimeExclusive = 2600)
        {
            if (schedule is null)
                return Blocked(string.Empty, "future_presence_schedule_missing");
            if (!IsValidTime(dayStartTime) || !IsValidTime(dayEndTimeExclusive) ||
                ToMinutes(dayEndTimeExclusive) <= ToMinutes(dayStartTime))
            {
                return Blocked(schedule.NpcName, "future_presence_day_window_invalid", schedule);
            }
            if (schedule.Status == NpcFutureScheduleResolutionStatus.NoSchedule)
            {
                return new NpcFuturePresenceWindowResolution
                {
                    Status = NpcFuturePresenceWindowResolutionStatus.NoSchedule,
                    NpcName = schedule.NpcName,
                    SelectedScheduleKey = schedule.SelectedScheduleKey,
                    ResolvedScheduleKey = schedule.ResolvedScheduleKey
                };
            }
            if (schedule.Status != NpcFutureScheduleResolutionStatus.Exact)
                return Blocked(schedule.NpcName, "future_presence_schedule_not_exact", schedule);

            var initial = schedule.Endpoints
                .Where(endpoint => endpoint.IsInitialPosition)
                .ToArray();
            if (initial.Length > 1)
                return Blocked(schedule.NpcName, "future_presence_initial_position_ambiguous", schedule);

            var timed = schedule.Endpoints
                .Where(endpoint => !endpoint.IsInitialPosition)
                .OrderBy(endpoint => ToMinutes(endpoint.ScheduledDepartureTime))
                .ToArray();
            if (timed.Length == 0)
                return Blocked(schedule.NpcName, "future_presence_timed_endpoints_missing", schedule);
            if (!schedule.TravelTimesResolved || timed.Any(endpoint =>
                    !endpoint.ScheduledArrivalTime.HasValue ||
                    !IsValidTime(endpoint.ScheduledDepartureTime) ||
                    !IsValidTime(endpoint.ScheduledArrivalTime.Value)))
            {
                return Blocked(schedule.NpcName, "future_presence_travel_timing_incomplete", schedule);
            }

            var windows = new List<NpcFuturePresenceWindow>();
            if (initial.Length == 1)
            {
                windows.Add(CreateWindow(
                    initial[0],
                    dayStartTime,
                    EarlierTime(timed[0].ScheduledDepartureTime, dayEndTimeExclusive)));
            }

            for (var index = 0; index < timed.Length; index++)
            {
                var endpoint = timed[index];
                var end = index + 1 < timed.Length
                    ? EarlierTime(timed[index + 1].ScheduledDepartureTime, dayEndTimeExclusive)
                    : dayEndTimeExclusive;
                windows.Add(CreateWindow(
                    endpoint,
                    LaterTime(endpoint.ScheduledArrivalTime!.Value, dayStartTime),
                    end));
            }

            return new NpcFuturePresenceWindowResolution
            {
                Status = NpcFuturePresenceWindowResolutionStatus.Exact,
                NpcName = schedule.NpcName,
                SelectedScheduleKey = schedule.SelectedScheduleKey,
                ResolvedScheduleKey = schedule.ResolvedScheduleKey,
                Windows = windows.ToArray()
            };
        }

        private static NpcFuturePresenceWindow CreateWindow(
            NpcFutureScheduleEndpoint endpoint,
            int start,
            int end) => new()
        {
            ScheduleEntryOrdinal = endpoint.ScheduleEntryOrdinal,
            IsInitialPosition = endpoint.IsInitialPosition,
            LocationName = endpoint.LocationName,
            TileX = endpoint.TileX,
            TileY = endpoint.TileY,
            FacingDirection = endpoint.FacingDirection,
            WindowStartTime = start,
            WindowEndTimeExclusive = end,
            HasStableInterval = ToMinutes(start) < ToMinutes(end),
            EndpointBehaviorComplete = endpoint.EndBehaviorComplete
        };

        private static NpcFuturePresenceWindowResolution Blocked(
            string npcName,
            string reason,
            NpcFutureScheduleResolution? schedule = null) => new()
        {
            Status = NpcFuturePresenceWindowResolutionStatus.Blocked,
            NpcName = npcName,
            SelectedScheduleKey = schedule?.SelectedScheduleKey ?? string.Empty,
            ResolvedScheduleKey = schedule?.ResolvedScheduleKey ?? string.Empty,
            BlockingReasons = new[] { reason }
        };

        private static bool IsValidTime(int value) =>
            value >= 0 && value <= 2800 && value % 100 < 60;

        private static int EarlierTime(int left, int right) =>
            ToMinutes(left) <= ToMinutes(right) ? left : right;

        private static int LaterTime(int left, int right) =>
            ToMinutes(left) >= ToMinutes(right) ? left : right;

        private static int ToMinutes(int time) => time / 100 * 60 + time % 100;
    }
}
