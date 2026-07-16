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
        private readonly ExecutionAssumptionRegistry assumptionRegistry;
        private readonly MiningPerfectExecutorModel miningModel;
        private readonly FishingPerfectExecutorModel fishingModel;
        private readonly NavigationPerfectExecutorModel navigationModel;

        public TimeBudgetValidator()
            : this(
                new ExecutionAssumptionRegistry(),
                new MiningPerfectExecutorModel(),
                new FishingPerfectExecutorModel(),
                new NavigationPerfectExecutorModel())
        {
        }

        public TimeBudgetValidator(
            ExecutionAssumptionRegistry assumptionRegistry,
            MiningPerfectExecutorModel miningModel,
            FishingPerfectExecutorModel fishingModel,
            NavigationPerfectExecutorModel navigationModel)
        {
            this.assumptionRegistry = assumptionRegistry;
            this.miningModel = miningModel;
            this.fishingModel = fishingModel;
            this.navigationModel = navigationModel;
        }

        public TimeBudgetReport Validate(WorldModelEnvelope model, ActionQueueEnvelope queue)
        {
            var currentTime = model.InGameTime ?? 600;
            var available = Math.Max(0, ClockMinutesBetween(currentTime, DefaultDeadlineTime) - DefaultSafetyBufferMinutes);
            var items = queue.Items
                .Where(item => item.Status == "pending")
                .SelectMany(EstimateItems)
                .ToArray();
            var required = items
                .Where(item => item.ScheduleRole != "optional")
                .Where(item => item.EstimatedMinutes >= 0)
                .Sum(item => item.EstimatedMinutes);
            var optional = items
                .Where(item => item.ScheduleRole == "optional")
                .Where(item => item.EstimatedMinutes >= 0)
                .Sum(item => item.EstimatedMinutes);
            var blockReasons = new List<string>();
            var hasUnknownRequiredDuration = items.Any(item => item.ScheduleRole != "optional" && item.EstimatedMinutes < 0);
            var hasUnknownOptionalDuration = items.Any(item => item.ScheduleRole == "optional" && item.EstimatedMinutes < 0);
            if (hasUnknownRequiredDuration)
            {
                blockReasons.Add("time_budget_contains_unknown_duration");
            }
            if (hasUnknownOptionalDuration)
            {
                blockReasons.Add("time_budget_contains_unknown_optional_duration");
            }
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
                FitsRequired = !hasUnknownRequiredDuration && required <= available,
                FitsRequiredPlusOptional = !hasUnknownRequiredDuration && !hasUnknownOptionalDuration && required + optional <= available,
                ExecutionProfile = PerfectHumanProfile,
                Items = items,
                BlockReasons = blockReasons.ToArray()
            };
        }

        private IEnumerable<TimeBudgetItem> EstimateItems(ActionQueueItem item)
        {
            if (item.NormalizedCommand.StrategyPlan.Length > 0)
            {
                foreach (var plan in item.NormalizedCommand.StrategyPlan)
                {
                    yield return StrategyItem(item, plan, "required", plan.RequiredMinutes);

                    if (plan.OptionalMinutes != 0)
                    {
                        yield return StrategyItem(item, plan, "optional", plan.OptionalMinutes);
                    }
                }

                yield break;
            }

            yield return EstimateItem(item);
        }

        private TimeBudgetItem StrategyItem(ActionQueueItem item, StrategyPlanStep plan, string role, int minutes)
        {
            var notes = new List<string>
            {
                "strategy_direction:" + plan.DirectionId,
                "strategy_domain:" + plan.Domain,
                "feedback_key:" + plan.FeedbackKey,
                "potential_points:" + plan.PotentialPoints,
                "priority_score:" + plan.PriorityScore
            };
            notes.AddRange(plan.HardPreconditions.Select(precondition => "hard_precondition:" + precondition));
            notes.AddRange(plan.ResourceBudget.Select(resource => "resource_budget:" + resource));
            if (!string.IsNullOrWhiteSpace(plan.ExecutorHandoffOption))
            {
                notes.Add("executor_handoff_option:" + plan.ExecutorHandoffOption);
            }

            return new TimeBudgetItem
            {
                QueueItemId = item.QueueItemId,
                OptionId = item.OptionId,
                ScheduleRole = role,
                EstimatedMinutes = minutes,
                Estimator = "strategy_plan_rule.v1",
                Notes = notes.ToArray()
            };
        }

        private TimeBudgetItem EstimateItem(ActionQueueItem item)
        {
            var role = Parameter(item, "schedule_role") == "optional" ? "optional" : "required";
            var optionId = item.OptionId;
            var estimate = EstimateDuration(item);
            return new TimeBudgetItem
            {
                QueueItemId = item.QueueItemId,
                OptionId = optionId,
                ScheduleRole = role,
                EstimatedMinutes = estimate.Minutes,
                Estimator = estimate.Estimator,
                Notes = NotesFor(item, estimate).ToArray()
            };
        }

        private DurationEstimate EstimateDuration(ActionQueueItem item)
        {
            switch (item.OptionId)
            {
                case "farm.maintain_crops":
                    return Fixed(30, "crop_farming_rule.v1");
                case "farm.process_machines":
                    return Fixed(20, "machine_processing_rule.v1");
                case "economy.buy_supplies":
                    return Fixed(90, "shop_menu_rule.v1");
                case "economy.sell_items":
                    return Fixed(30, "shop_menu_rule.v1");
                case "social.talk_npc":
                case "social.gift_npc":
                    return Unknown("social_duration_unknown_until_route_and_native_executor.v1");
                case "quest.advance":
                    return Unknown("quest_duration_unknown_until_route_and_native_executor_timing");
                case "recovery.stabilize_day":
                    return Fixed(30, "recovery_rule.v1");
                case "mining.reach_depth":
                    return miningModel.Estimate(item);
                case "mining.acquire_golden_scythe":
                    return Unknown("golden_scythe_duration_unknown_until_single_floor_runtime_calibration");
                case "volcano.reach_caldera":
                    return Unknown("volcano_duration_unknown_until_native_level_loop_calibration");
                case "exploration.visit_location":
                    return EstimateExploration(item);
                case "fishing.catch_fish":
                case "executor.catch_fish":
                    return fishingModel.Estimate(item);
                default:
                    return Fixed(120, "unknown_option_rule.v1");
            }
        }

        private DurationEstimate EstimateExploration(ActionQueueItem item)
        {
            if (Parameter(item, "target_activity") == "mining")
            {
                return miningModel.Estimate(item);
            }

            if (Parameter(item, "target_activity") == "fishing")
            {
                return fishingModel.Estimate(item);
            }

            return navigationModel.Estimate(item);
        }

        private static DurationEstimate Fixed(int minutes, string estimator)
        {
            return new DurationEstimate
            {
                Minutes = minutes,
                Estimator = estimator,
                Notes = Array.Empty<string>()
            };
        }

        private static DurationEstimate Unknown(string estimator)
        {
            return new DurationEstimate
            {
                Minutes = -1,
                Estimator = estimator,
                Notes = new[] { "duration_unknown_not_rankable_until_route_and_native_executor_timing" }
            };
        }

        private IEnumerable<string> NotesFor(ActionQueueItem item, DurationEstimate estimate)
        {
            foreach (var note in estimate.Notes)
            {
                yield return note;
            }

            var assumption = FindAssumption(item);
            if (assumption is not null)
            {
                yield return "assumption_domain:" + assumption.DomainId;
                yield return "preference_penalty_exclusions:" + string.Join(",", assumption.PreferencePenaltyExclusions);
            }

            yield return "decompile_evidence:Game1.timeOfDay advances in 10 minute steps and caps at 2600";
        }

        private ExecutionAssumption? FindAssumption(ActionQueueItem item)
        {
            var activity = Parameter(item, "target_activity");
            if (activity == "mining")
            {
                return assumptionRegistry.GetRequired("mining_and_combat");
            }

            if (activity == "fishing")
            {
                return assumptionRegistry.GetRequired("fishing");
            }

            if (item.OptionId is "fishing.catch_fish" or "executor.catch_fish")
            {
                return assumptionRegistry.GetRequired("fishing");
            }

            if (item.OptionId == "exploration.visit_location")
            {
                return assumptionRegistry.GetRequired("navigation");
            }

            return assumptionRegistry.All.FirstOrDefault(assumption =>
                assumption.AppliesToOptions.Contains(item.OptionId, StringComparer.Ordinal));
        }

        private static string? Parameter(ActionQueueItem item, string name)
        {
            return item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
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
