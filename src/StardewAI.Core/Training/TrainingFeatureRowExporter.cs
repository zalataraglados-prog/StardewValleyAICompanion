using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training
{
    public sealed class TrainingFeatureRowExporter
    {
        public TrainingFeatureRowEnvelope Build(WorldModelEnvelope worldModel, TrainingEpisodeEnvelope episode)
        {
            return new TrainingFeatureRowEnvelope
            {
                RowId = "feature-row." + Guid.NewGuid().ToString("N"),
                EpisodeId = episode.EpisodeId,
                SourceStateHash = episode.SourceStateHash,
                QueueId = episode.QueueId,
                StateFeatures = BuildStateFeatures(worldModel),
                ActionFeatures = BuildActionFeatures(episode),
                Labels = BuildLabels(episode)
            };
        }

        private static FeatureVector BuildStateFeatures(WorldModelEnvelope worldModel)
        {
            var numeric = new List<NumericFeature>
            {
                Number("game.time", ReadDouble(worldModel.Facts.Game, "time")),
                Number("game.day", ReadDouble(worldModel.Facts.Game, "day")),
                Number("game.year", ReadDouble(worldModel.Facts.Game, "year")),
                Number("player.money", ReadDouble(worldModel.Facts.Player, "money")),
                Number("player.energy", ReadDouble(worldModel.Facts.Player, "energy")),
                Number("player.health", ReadDouble(worldModel.Facts.Player, "health")),
                Number("player.level", ReadDouble(worldModel.Facts.Player, "level")),
                Number("player.total_money_earned", ReadDouble(worldModel.Facts.Player, "total_money_earned")),
                Number("current_location.crops_needing_watering", CountCropsNeedingWater(worldModel)),
                Number("completeness.unavailable_count", worldModel.Completeness.UnavailableCount),
                Number("completeness.required_readable_ratio", ReadableRatio(worldModel))
            };

            var categorical = new List<CategoricalFeature>
            {
                Category("game.season", ReadString(worldModel.Facts.Game, "season")),
                Category("game.weather", ReadString(worldModel.Facts.Game, "weather")),
                Category("player.location_id", ReadString(worldModel.Facts.Player, "location_id")),
                Category("world.mode", worldModel.Mode)
            };

            var boolean = new List<BooleanFeature>
            {
                Flag("completeness.all_required_facts_readable", worldModel.Completeness.AllRequiredFactsReadable),
                Flag("planner_inputs.blocked", worldModel.PlannerInputs.Blocked)
            };

            return new FeatureVector
            {
                Numeric = numeric.ToArray(),
                Categorical = categorical.ToArray(),
                Boolean = boolean.ToArray()
            };
        }

        private static ActionFeatureVector BuildActionFeatures(TrainingEpisodeEnvelope episode)
        {
            var optionIds = episode.ActionSummary.OptionIds
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var firstOption = optionIds.FirstOrDefault() ?? "none";
            var behaviorCategory = BehaviorCategory(firstOption);
            var trainingRole = TrainingRole(behaviorCategory);
            var excludeFromPolicyTraining = trainingRole == TrainingRoles.ExecutorCalibration;
            var candidateAudit = episode.CandidateAudit ?? Array.Empty<StardewAI.Contracts.Execution.SmallModelPlanCandidateAudit>();
            var skippedAudits = candidateAudit
                .Where(item => string.Equals(item.Decision, "skipped", StringComparison.Ordinal))
                .ToArray();
            var acceptedAudits = candidateAudit
                .Where(item => string.Equals(item.Decision, "accepted", StringComparison.Ordinal))
                .ToArray();
            var skipReasons = skippedAudits
                .SelectMany(item => item.Reasons)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var hasTimeBudgetSkip = skipReasons.Contains("aggregate_time_budget_exceeded", StringComparer.Ordinal);
            var hasEnergyBudgetSkip = skipReasons.Contains("aggregate_energy_budget_exceeded", StringComparer.Ordinal);

            return new ActionFeatureVector
            {
                OptionIds = optionIds,
                TrainingRole = trainingRole,
                LearningScope = excludeFromPolicyTraining ? "calibration_only" : "policy_ranker",
                ExcludeFromPolicyTraining = excludeFromPolicyTraining,
                Features = new FeatureVector
                {
                    Numeric = new[]
                    {
                        Number("action.option_count", optionIds.Length),
                        Number("action.required_minutes", episode.HardFeasibility.TimeBudget.RequiredMinutes),
                        Number("action.optional_minutes", episode.HardFeasibility.TimeBudget.OptionalMinutes),
                        Number("candidate_audit.accepted_count", acceptedAudits.Length),
                        Number("candidate_audit.skipped_count", skippedAudits.Length),
                        Number("candidate_audit.skipped_time_budget_count", CountSkipReason(skippedAudits, "aggregate_time_budget_exceeded")),
                        Number("candidate_audit.skipped_energy_budget_count", CountSkipReason(skippedAudits, "aggregate_energy_budget_exceeded")),
                        Number("candidate_audit.skipped_max_candidates_count", CountSkipReason(skippedAudits, "max_candidates_reached")),
                        Number("candidate_audit.skipped_unsupported_count", CountSkipReason(skippedAudits, "unsupported_candidate_kind_or_missing_required_candidate_fields")),
                        Number("candidate_audit.skipped_candidate_minutes_sum", skippedAudits.Sum(item => item.CandidateMinutes)),
                        Number("candidate_audit.skipped_candidate_energy_sum", skippedAudits.Sum(item => item.CandidateEnergyCost))
                    },
                    Categorical = new[]
                    {
                        Category("action.primary_option_id", firstOption),
                        Category("action.intent_category", behaviorCategory),
                        Category("action.behavior_category", behaviorCategory),
                        Category("action.training_role", trainingRole),
                        Category("action.learning_scope", excludeFromPolicyTraining ? "calibration_only" : "policy_ranker"),
                        Category("action.execution_mode", episode.ActionSummary.ExecutionMode),
                        Category("action.actor_type", episode.ActionSummary.Actor.ActorType),
                        Category("action.execution_profile", episode.ExecutorCalibration.ExecutionProfile),
                        Category("candidate_audit.primary_skip_reason", skipReasons.FirstOrDefault() ?? "none"),
                        Category("candidate_audit.skip_reasons", skipReasons.Length == 0 ? "none" : string.Join("|", skipReasons))
                    },
                    Boolean = new[]
                    {
                        Flag("action.hard_blocked", episode.HardFeasibility.Blocked),
                        Flag("action.exclude_from_policy_training", excludeFromPolicyTraining),
                        Flag("candidate_audit.present", candidateAudit.Length > 0),
                        Flag("candidate_audit.has_budget_skip", hasTimeBudgetSkip || hasEnergyBudgetSkip),
                        Flag("candidate_audit.has_time_budget_skip", hasTimeBudgetSkip),
                        Flag("candidate_audit.has_energy_budget_skip", hasEnergyBudgetSkip)
                    }
                }
            };
        }

        private static int CountSkipReason(IEnumerable<StardewAI.Contracts.Execution.SmallModelPlanCandidateAudit> audits, string reason)
        {
            return audits.Count(item => item.Reasons.Contains(reason, StringComparer.Ordinal));
        }

        private static TrainingLabelVector BuildLabels(TrainingEpisodeEnvelope episode)
        {
            return new TrainingLabelVector
            {
                GoalProgressDelta = episode.StrategyValue.GoalProgressDelta,
                TotalReward = episode.StrategyValue.RewardTerms.Sum(item => item.Value),
                HardBlocked = episode.HardFeasibility.Blocked,
                RequiredMinutes = episode.HardFeasibility.TimeBudget.RequiredMinutes,
                AvailableMinutes = episode.HardFeasibility.TimeBudget.AvailableMinutes,
                RewardTermNames = episode.StrategyValue.RewardTerms
                    .Select(item => item.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToArray(),
                BlockReasons = episode.HardFeasibility.BlockReasons
            };
        }

        private static double ReadableRatio(WorldModelEnvelope worldModel)
        {
            return worldModel.Completeness.RequiredFactCount == 0
                ? 0
                : Math.Round((double)worldModel.Completeness.ReadableRequiredFactCount / worldModel.Completeness.RequiredFactCount, 4);
        }

        private static double CountCropsNeedingWater(WorldModelEnvelope worldModel)
        {
            if (!worldModel.Facts.CurrentLocation.TryGetValue("crops", out var crops) || crops.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            var count = 0;
            foreach (var crop in crops.EnumerateArray())
            {
                if (crop.ValueKind == JsonValueKind.Object &&
                    crop.TryGetProperty("needs_watering", out var value) &&
                    value.ValueKind == JsonValueKind.True)
                {
                    count++;
                }
            }

            return count;
        }

        private static double ReadDouble(IReadOnlyDictionary<string, JsonElement> facts, string key)
        {
            if (!facts.TryGetValue(key, out var value))
            {
                return 0;
            }

            if (value.TryGetDouble(out var result))
            {
                return result;
            }

            return 0;
        }

        private static string ReadString(IReadOnlyDictionary<string, JsonElement> facts, string key)
        {
            return facts.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "unknown"
                : "unknown";
        }

        private static string BehaviorCategory(string optionId)
        {
            switch (optionId)
            {
                case "farm.maintain_crops":
                case "farm.process_machines":
                case "executor.water_crop":
                case "executor.catch_fish":
                case "executor.collect_crab_pot":
                case "executor.collect_fish_pond_output":
                case "executor.complete_fish_pond_request":
                case "executor.collect_animal_product":
                case "executor.pet_interact":
                case "executor.fill_pet_bowl":
                case "executor.pan_ore_spot":
                case "executor.harvest_ginger":
                case "executor.harvest_bush":
                case "executor.break_current_location_resource_clump":
                case "executor.claim_mine_reward_chest":
                case "rewards.claim_adventure_guild_reward":
                case "executor.claim_adventure_guild_reward":
                case "rewards.claim_pot_of_gold":
                    return OptionBehaviorCategories.Mechanical;
                case "exploration.visit_location":
                case "fishing.catch_fish":
                case "fishing.collect_crab_pots":
                case "fishing.service_fish_ponds":
                case "farm.collect_animal_products":
                case "farm.care_for_pets":
                case "foraging.pan_ore_spot":
                case "foraging.harvest_ginger":
                case "foraging.harvest_bushes":
                case "foraging.clear_green_rain_bushes":
                case "mining.claim_reward_chests":
                    return OptionBehaviorCategories.ParameterizedMechanical;
                case "economy.buy_supplies":
                case "economy.sell_items":
                case "economy.ship_items":
                case "quest.advance":
                    return OptionBehaviorCategories.EconomicStrategic;
                case "social.talk_npc":
                case "social.gift_npc":
                case "social.advance_partnership":
                    return OptionBehaviorCategories.SocialStrategic;
                case "strategy.grandpa_progress":
                    return OptionBehaviorCategories.LongTermStrategic;
                case "recovery.stabilize_day":
                    return OptionBehaviorCategories.Recovery;
                default:
                    return OptionBehaviorCategories.Unknown;
            }
        }

        private static string TrainingRole(string behaviorCategory)
        {
            return behaviorCategory == OptionBehaviorCategories.Mechanical ||
                behaviorCategory == OptionBehaviorCategories.Recovery
                ? TrainingRoles.ExecutorCalibration
                : TrainingRoles.StrategyValue;
        }

        private static NumericFeature Number(string name, double value)
        {
            return new NumericFeature { Name = name, Value = value };
        }

        private static CategoricalFeature Category(string name, string value)
        {
            return new CategoricalFeature
            {
                Name = name,
                Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value
            };
        }

        private static BooleanFeature Flag(string name, bool value)
        {
            return new BooleanFeature { Name = name, Value = value };
        }
    }
}
