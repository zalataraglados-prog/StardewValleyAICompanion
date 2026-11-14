using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        public SmallModelPlanEnvelope CompileWithJointCalendar(
            IEnumerable<PolicyEventCandidatePrediction> candidates,
            string stateHash,
            int currentTime,
            string goalId = "daily.closed_loop",
            string executionMode = "training_singleplayer",
            int maxCandidates = 4,
            int? availableMinutes = null,
            int? energyBudget = null)
            => CompileCore(candidates, stateHash, goalId, executionMode, maxCandidates, availableMinutes, energyBudget,
                new JointCalendarBudget(currentTime));

        public SmallModelPlanEnvelope CompileWithJointCalendar(
            IEnumerable<PolicyEventCandidatePrediction> candidates,
            string stateHash,
            int currentTime,
            IReadOnlyCollection<string> requiredCandidateIds,
            string goalId = "daily.closed_loop",
            string executionMode = "training_singleplayer",
            int maxCandidates = 4,
            int? availableMinutes = null,
            int? energyBudget = null)
        {
            var rows = candidates.ToArray();
            var required = new HashSet<string>(requiredCandidateIds, StringComparer.Ordinal);
            if (required.Count != requiredCandidateIds.Count || required.Any(string.IsNullOrWhiteSpace) ||
                required.Any(id => rows.Count(row => row.CandidateId == id) != 1 ||
                    rows.Single(row => row.CandidateId == id).TimelineStatus == "blocked"))
                throw new ArgumentException("Required candidate IDs must name distinct, uniquely materialized unblocked candidates.");
            return CompileCore(rows, stateHash, goalId, executionMode, maxCandidates, availableMinutes, energyBudget,
                new JointCalendarBudget(currentTime), required);
        }

        private sealed class JointCalendarBudget
        {
            private readonly int sourceTime;
            private int elapsedMinutes;

            internal JointCalendarBudget(int sourceTime)
            {
                if (!ValidClock(sourceTime) || sourceTime < 600)
                    throw new ArgumentOutOfRangeException(nameof(sourceTime));
                this.sourceTime = sourceTime;
            }

            internal string? BlockReason(PolicyEventCandidatePrediction candidate, int minutes, bool rollingWait)
            {
                var start = GameClockBudgetPolicy.AddClockMinutes(sourceTime, elapsedMinutes);
                var finish = GameClockBudgetPolicy.AddClockMinutes(start, minutes);
                if (finish > GameClockBudgetPolicy.DayEndTime) return "aggregate_calendar_day_end_exceeded";
                // A bounded wait observes a new source; it does not execute the deferred interaction.
                if (rollingWait) return null;
                if (candidate.EffectiveOpenTime.HasValue && !ValidClock(candidate.EffectiveOpenTime.Value) ||
                    candidate.ClosesAt.HasValue && !ValidClock(candidate.ClosesAt.Value))
                    return "aggregate_calendar_window_invalid";
                if (candidate.EffectiveOpenTime.HasValue && start < candidate.EffectiveOpenTime.Value)
                    return "aggregate_calendar_window_not_open";
                return candidate.ClosesAt.HasValue && finish > candidate.ClosesAt.Value
                    ? "aggregate_calendar_window_missed" : null;
            }

            internal SmallModelPlanStep Annotate(SmallModelPlanStep step, int candidateMinutes)
            {
                var start = GameClockBudgetPolicy.AddClockMinutes(sourceTime, elapsedMinutes);
                step.Parameters = step.Parameters.Concat(new[]
                {
                    Parameter("budget.candidate_start_time", start.ToString()),
                    Parameter("budget.candidate_end_time", GameClockBudgetPolicy.AddClockMinutes(start, candidateMinutes).ToString())
                }).ToArray();
                step.SafetyConstraints = step.SafetyConstraints.Concat(new[] { "daily_plan_joint_calendar_checked" }).ToArray();
                return step;
            }

            internal void Accept(int minutes) => elapsedMinutes = checked(elapsedMinutes + minutes);
            private static bool ValidClock(int time) => time >= 0 && time <= GameClockBudgetPolicy.DayEndTime && time % 100 < 60;
        }
    }
}
