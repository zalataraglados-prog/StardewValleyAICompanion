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
                case "farm.collect_animal_products":
                case "executor.collect_animal_product":
                    return EstimateCompiledSteps(item, "native_animal_tool_steps.v1");
                case "animals.purchase":
                case "executor.choose_animal_purchase_response":
                case "executor.purchase_animal":
                    return EstimateCompiledSteps(item, "native_animal_purchase_menu_steps.v1");
                case "animals.manage_animal":
                case "executor.manage_animal":
                    return EstimateCompiledSteps(item, "native_animal_query_menu_steps.v1");
                case "farm.care_for_pets":
                case "executor.pet_interact":
                case "executor.fill_pet_bowl":
                    return EstimateCompiledSteps(item, "native_pet_care_steps.v1");
                case "foraging.pan_ore_spot":
                case "executor.pan_ore_spot":
                    return EstimateCompiledSteps(item, "native_pan_steps.v1");
                case "foraging.harvest_ginger":
                case "executor.harvest_ginger":
                    return EstimateCompiledSteps(item, "native_ginger_hoe_steps.v1");
                case "foraging.harvest_bushes":
                case "executor.harvest_bush":
                    return EstimateCompiledSteps(item, "native_bush_shake_steps.v1");
                case "foraging.harvest_fruit_tree":
                case "executor.harvest_fruit_tree":
                    return EstimateCompiledSteps(item, "native_fruit_tree_shake_steps.v1");
                case "foraging.harvest_tree_product":
                case "executor.harvest_tree_product":
                    return EstimateCompiledSteps(item, "native_wild_tree_product_shake_steps.v1");
                case "foraging.rummage_garbage":
                case "executor.rummage_garbage":
                    return EstimateCompiledSteps(item, "native_garbage_can_rummage_steps.v1");
                case "housing.renovate":
                case "executor.renovate_home":
                    return EstimateCompiledSteps(item, "native_home_renovation_menu_steps.v1");
                case "foraging.clear_green_rain_bushes":
                case "executor.break_current_location_resource_clump":
                    return EstimateCompiledSteps(item, "native_green_rain_resource_clump_steps.v1");
                case "mining.claim_reward_chests":
                case "executor.claim_mine_reward_chest":
                    return EstimateCompiledSteps(item, "native_mineshaft_reward_chest_steps.v1");
                case "fishing.service_fish_ponds":
                case "executor.collect_fish_pond_output":
                case "executor.complete_fish_pond_request":
                    return EstimateCompiledSteps(item, "native_fish_pond_steps.v1");
                case "economy.buy_supplies":
                    return Fixed(90, "shop_menu_rule.v1");
                case "economy.sell_items":
                    return Fixed(30, "shop_menu_rule.v1");
                case "social.talk_npc":
                case "social.gift_npc":
                case "social.advance_partnership":
                    return Unknown("social_duration_unknown_until_route_and_native_executor.v1");
                case "quest.advance":
                    return Unknown("quest_duration_unknown_until_route_and_native_executor_timing");
                case "recovery.stabilize_day":
                    return EstimateRecovery(item);
                case "executor.wait_ticks":
                    return EstimateWait(item);
                case "executor.close_menu":
                    return Fixed(1, "native_close_menu_rule.v1");
                case "executor.traverse_connector":
                    return EstimateConnector(item);
                case "executor.sleep":
                    return EstimateCompiledSteps(item, "native_sleep_macro_steps.v1");
                case "recovery.sleep_in_tent":
                    return EstimateCompiledSteps(item, "native_tent_sleep_macro_steps.v1");
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

        private DurationEstimate EstimateRecovery(ActionQueueItem item)
        {
            return Parameter(item, "execution_option_id") switch
            {
                "executor.wait_ticks" => EstimateWait(item),
                "executor.close_menu" => Fixed(1, "recovery_close_menu_rule.v2"),
                "executor.traverse_connector" => EstimateConnector(item),
                "executor.sleep" => EstimateCompiledSteps(item, "recovery_sleep_macro_steps.v2"),
                _ => EstimateCompiledSteps(item, "recovery_compiled_steps.v2")
            };
        }

        private static DurationEstimate EstimateWait(ActionQueueItem item)
        {
            var waitTicks = ParameterInt(item, "wait_ticks");
            return waitTicks.HasValue && waitTicks.Value > 0
                ? Fixed(TicksToMinutes(waitTicks.Value), "bounded_wait_ticks.v1")
                : EstimateCompiledSteps(item, "bounded_wait_compiled_steps.v1");
        }

        private static DurationEstimate EstimateConnector(ActionQueueItem item)
        {
            var estimatedMinutes = ParameterInt(item, "estimated_minutes");
            if (estimatedMinutes.HasValue && estimatedMinutes.Value > 0)
            {
                return Fixed(estimatedMinutes.Value, "transparent_current_connector_path.v1");
            }

            var estimatedTicks = ParameterInt(item, "estimated_ticks");
            if (estimatedTicks.HasValue && estimatedTicks.Value > 0)
            {
                return Fixed(TicksToMinutes(estimatedTicks.Value), "transparent_current_connector_ticks.v1");
            }

            return EstimateCompiledSteps(item, "transparent_connector_compiled_steps.v1");
        }

        private static DurationEstimate EstimateCompiledSteps(ActionQueueItem item, string estimator)
        {
            if (item.NormalizedCommand.Steps.Length == 0 ||
                item.NormalizedCommand.Steps.Any(step => step.EstimatedTicks < 0))
            {
                return Unknown(estimator + ".duration_unknown");
            }

            return Fixed(
                TicksToMinutes(item.NormalizedCommand.Steps.Sum(step => Math.Max(0, step.EstimatedTicks))),
                estimator);
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
            if (item.OptionId is "fishing.collect_crab_pots" or "executor.collect_crab_pot")
            {
                return assumptionRegistry.GetRequired("crab_pot_collection");
            }

            if (item.OptionId is "fishing.service_fish_ponds" or "executor.collect_fish_pond_output" or "executor.complete_fish_pond_request")
            {
                return assumptionRegistry.GetRequired("fish_pond_service");
            }

            if (item.OptionId is "farm.collect_animal_products" or "executor.collect_animal_product" or
                "animals.purchase" or "executor.choose_animal_purchase_response" or "executor.purchase_animal" or
                "animals.manage_animal" or "executor.manage_animal" or
                "farm.care_for_pets" or "executor.pet_interact" or "executor.fill_pet_bowl")
            {
                return assumptionRegistry.GetRequired("animals");
            }

            if (item.OptionId is "foraging.pan_ore_spot" or "executor.pan_ore_spot")
            {
                return assumptionRegistry.GetRequired("panning");
            }

            if (item.OptionId is "foraging.harvest_ginger" or "executor.harvest_ginger")
            {
                return assumptionRegistry.GetRequired("ginger_harvest");
            }

            if (item.OptionId is "foraging.harvest_bushes" or "executor.harvest_bush")
            {
                return assumptionRegistry.GetRequired("bush_harvest");
            }

            if (item.OptionId is "foraging.harvest_fruit_tree" or "executor.harvest_fruit_tree")
            {
                return assumptionRegistry.GetRequired("fruit_tree_harvest");
            }

            if (item.OptionId is "foraging.harvest_tree_product" or "executor.harvest_tree_product")
            {
                return assumptionRegistry.GetRequired("wild_tree_product_harvest");
            }

            if (item.OptionId is "foraging.rummage_garbage" or "executor.rummage_garbage")
            {
                return assumptionRegistry.GetRequired("garbage_can_rummage");
            }

            if (item.OptionId is "foraging.clear_green_rain_bushes" or "executor.break_current_location_resource_clump")
            {
                return assumptionRegistry.GetRequired("green_rain_resource_clump");
            }

            if (item.OptionId is "mining.claim_reward_chests" or "executor.claim_mine_reward_chest")
            {
                return assumptionRegistry.GetRequired("mining_and_combat");
            }

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

        private static int? ParameterInt(ActionQueueItem item, string name)
        {
            return int.TryParse(Parameter(item, name), out var value) ? value : null;
        }

        private static int TicksToMinutes(int ticks)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, ticks) / 60d));
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
