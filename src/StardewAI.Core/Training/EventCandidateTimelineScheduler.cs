using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class EventCandidateTimelineScheduler
    {
        private const int DefaultMaxWaitTicks = 21600;

        public PolicyEventCandidatePrediction[] Schedule(
            IEnumerable<PolicyEventCandidatePrediction> candidates,
            int currentTime,
            int maxWaitTicks = DefaultMaxWaitTicks)
        {
            return candidates
                .Select(candidate => ScheduleCandidate(candidate, currentTime, maxWaitTicks))
                .Where(candidate => candidate.TimelineStatus != "blocked")
                .OrderBy(candidate => candidate.TimelineStatus == "ready_now" ? 0 : 1)
                .ThenBy(candidate => candidate.ScheduledWaitCost ?? 0)
                .ThenByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select((candidate, index) =>
                {
                    candidate.Rank = index + 1;
                    return candidate;
                })
                .ToArray();
        }

        private static PolicyEventCandidatePrediction ScheduleCandidate(
            PolicyEventCandidatePrediction candidate,
            int currentTime,
            int maxWaitTicks)
        {
            var reasons = new List<string>();
            if (candidate.AllowedNow == false)
            {
                if (candidate.AllowedToday != true)
                {
                    reasons.Add("candidate_not_allowed_today");
                    return WithTimeline(candidate, "blocked", null, null, reasons);
                }

                var waitCost = candidate.WaitCost ?? 0;
                if (waitCost > maxWaitTicks)
                {
                    reasons.Add("candidate_wait_exceeds_time_budget");
                    return WithTimeline(candidate, "blocked", null, waitCost, reasons);
                }

                var startTime = candidate.NextOpenTime ?? candidate.EffectiveOpenTime;
                if (!startTime.HasValue)
                {
                    reasons.Add("candidate_missing_next_open_time");
                    return WithTimeline(candidate, "blocked", null, waitCost, reasons);
                }

                if (candidate.ClosesAt.HasValue &&
                    WouldFinishAfterClose(startTime.Value, candidate.EstimatedTicks, candidate.ClosesAt.Value))
                {
                    reasons.Add("candidate_would_finish_after_close");
                    return WithTimeline(candidate, "blocked", startTime, waitCost, reasons);
                }

                reasons.Add("candidate_deferred_until_open");
                return WithTimeline(candidate, "deferred", startTime, waitCost, reasons);
            }

            if (candidate.ClosesAt.HasValue &&
                WouldFinishAfterClose(currentTime, candidate.EstimatedTicks, candidate.ClosesAt.Value))
            {
                reasons.Add("candidate_would_finish_after_close");
                return WithTimeline(candidate, "blocked", currentTime, 0, reasons);
            }

            reasons.Add("candidate_ready_now");
            return WithTimeline(candidate, "ready_now", currentTime, 0, reasons);
        }

        private static PolicyEventCandidatePrediction WithTimeline(
            PolicyEventCandidatePrediction candidate,
            string status,
            int? scheduledStartTime,
            int? scheduledWaitCost,
            IEnumerable<string> reasons)
        {
            candidate.TimelineStatus = status;
            candidate.ScheduledStartTime = scheduledStartTime;
            candidate.ScheduledWaitCost = scheduledWaitCost;
            candidate.TimelineReasons = reasons
                .Concat(candidate.GateReasons ?? Array.Empty<string>())
                .Concat(candidate.BlockReasons ?? Array.Empty<string>())
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return candidate;
        }

        private static bool WouldFinishAfterClose(int startTime, int estimatedTicks, int closesAt)
        {
            var estimatedMinutes = (int)Math.Ceiling(Math.Max(0, estimatedTicks) / 60.0);
            return AddClockMinutes(startTime, estimatedMinutes) > closesAt;
        }

        private static int AddClockMinutes(int hhmm, int minutes)
        {
            var total = (hhmm / 100 * 60) + (hhmm % 100) + minutes;
            return total / 60 * 100 + total % 60;
        }
    }
}
