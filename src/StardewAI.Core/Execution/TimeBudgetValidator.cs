using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Execution
{
    public sealed class TimeBudgetValidator
    {
        private const int DefaultDeadlineTime = 2600;
        private const int DefaultSafetyBufferMinutes = 60;
        private const string PerfectHumanProfile = "perfect_human_player";

        public TimeBudgetReport Validate(WorldModelEnvelope model, ActionQueueEnvelope queue)
        {
            var currentTime = model.InGameTime ?? 600;
            var available = Math.Max(0, ClockMinutesBetween(currentTime, DefaultDeadlineTime) - DefaultSafetyBufferMinutes);
            var items = queue.Items
                .Where(item => item.Status == "pending")
                .Select(EstimateItem)
                .ToArray();
            var required = items
                .Where(item => item.ScheduleRole != "optional")
                .Sum(item => item.EstimatedMinutes);
            var optional = items
                .Where(item => item.ScheduleRole == "optional")
                .Sum(item => item.EstimatedMinutes);
            var blockReasons = new List<string>();
            if (required > available)
            {
                blockReasons.Add("required_work_exceeds_time_budget");
            }

            if (required + optional > available)
            {
                blockReasons.Add("required_plus_optional_exceeds_time_budget");
            }

            return new TimeBudgetReport
            {
                StateHash = model.StateHash,
                CurrentTime = currentTime,
                DeadlineTime = DefaultDeadlineTime,
                SafetyBufferMinutes = DefaultSafetyBufferMinutes,
                AvailableMinutes = available,
                RequiredMinutes = required,
                OptionalMinutes = optional,
                FitsRequired = required <= available,
                FitsRequiredPlusOptional = required + optional <= available,
                ExecutionProfile = PerfectHumanProfile,
                Items = items,
                BlockReasons = blockReasons.ToArray()
            };
        }

        private static TimeBudgetItem EstimateItem(ActionQueueItem item)
        {
            var role = Parameter(item, "schedule_role") == "optional" ? "optional" : "required";
            var optionId = item.OptionId;
            var minutes = EstimateMinutes(item);
            return new TimeBudgetItem
            {
                QueueItemId = item.QueueItemId,
                OptionId = optionId,
                ScheduleRole = role,
                EstimatedMinutes = minutes,
                Estimator = "decompile_seeded_rule.v1",
                Notes = NotesFor(item).ToArray()
            };
        }

        private static int EstimateMinutes(ActionQueueItem item)
        {
            switch (item.OptionId)
            {
                case "farm.maintain_crops":
                    return 30;
                case "farm.process_machines":
                    return 20;
                case "economy.buy_supplies":
                    return 90;
                case "economy.sell_items":
                    return 30;
                case "social.gift_npc":
                    return 90;
                case "quest.advance":
                    return 120;
                case "recovery.stabilize_day":
                    return 30;
                case "exploration.visit_location":
                    return EstimateExplorationMinutes(item);
                default:
                    return 120;
            }
        }

        private static int EstimateExplorationMinutes(ActionQueueItem item)
        {
            if (Parameter(item, "target_activity") == "mining")
            {
                var targetDepth = ParseInt(Parameter(item, "target_depth"));
                if (!targetDepth.HasValue)
                {
                    return 180;
                }

                var startingDepth = ParseInt(Parameter(item, "start_depth")) ?? 0;
                var levels = Math.Max(1, targetDepth.Value - startingDepth);
                var elevatorAdjustedLevels = Math.Min(levels, 5 + (levels % 5));
                return 60 + elevatorAdjustedLevels * 8;
            }

            return 90;
        }

        private static IEnumerable<string> NotesFor(ActionQueueItem item)
        {
            if (item.OptionId == "exploration.visit_location" && Parameter(item, "target_activity") == "mining")
            {
                yield return "execution_profile_assumes_perfect_human_player_inputs";
                yield return "random_mine_layout_affects_calibration_not_low_level_failure_penalty";
                yield return "decompile_evidence:MineShaft.mineLevel, MineShaft.mineRandom, MineShaft.findLadder";
            }

            yield return "decompile_evidence:Game1.timeOfDay advances in 10 minute steps and caps at 2600";
        }

        private static string? Parameter(ActionQueueItem item, string name)
        {
            return item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
        }

        private static int? ParseInt(string? value)
        {
            return int.TryParse(value, out var parsed) ? parsed : (int?)null;
        }

        private static int ClockMinutesBetween(int start, int end)
        {
            return ToAbsoluteMinutes(end) - ToAbsoluteMinutes(start);
        }

        private static int ToAbsoluteMinutes(int hhmm)
        {
            var hours = hhmm / 100;
            var minutes = hhmm % 100;
            return hours * 60 + minutes;
        }
    }
}
