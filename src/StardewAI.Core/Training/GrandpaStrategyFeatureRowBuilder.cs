using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaStrategyFeatureRowBuilder
    {
        public TrainingFeatureRowEnvelope[] Build(WorldModelEnvelope worldModel, TrainingSampleEnvelope sample, int maxRows)
        {
            if (sample.PlannerState.Blocked || sample.Target.Complete)
            {
                return Array.Empty<TrainingFeatureRowEnvelope>();
            }

            return sample.CandidateDirections
                .Where(direction => direction.Known && !direction.Blocked && direction.PotentialPoints > 0)
                .OrderByDescending(direction => direction.PriorityScore)
                .ThenBy(direction => direction.DirectionId, StringComparer.Ordinal)
                .Take(Math.Max(1, maxRows))
                .Select(direction => BuildRow(worldModel, sample, direction))
                .ToArray();
        }

        private static TrainingFeatureRowEnvelope BuildRow(
            WorldModelEnvelope worldModel,
            TrainingSampleEnvelope sample,
            CandidateDirection direction)
        {
            var reward = Math.Round(direction.PriorityScore / 10.0, 4);
            return new TrainingFeatureRowEnvelope
            {
                RowId = "feature-row.grandpa." + Guid.NewGuid().ToString("N"),
                EpisodeId = "episode.grandpa.strategy." + Guid.NewGuid().ToString("N"),
                SourceStateHash = sample.SourceStateHash,
                QueueId = "grandpa.strategy." + direction.DirectionId,
                StateFeatures = BuildStateFeatures(worldModel, sample),
                ActionFeatures = BuildActionFeatures(direction),
                Labels = new TrainingLabelVector
                {
                    GoalProgressDelta = direction.PotentialPoints,
                    TotalReward = reward,
                    HardBlocked = false,
                    RequiredMinutes = EstimateRequiredMinutes(direction),
                    AvailableMinutes = 0,
                    RewardTermNames = new[]
                    {
                        "grandpa.potential_points",
                        "grandpa.direction_priority"
                    },
                    BlockReasons = Array.Empty<string>()
                },
                Audit = new TrainingFeatureRowAudit
                {
                    Exporter = "StardewAI.Core.Training.GrandpaStrategyFeatureRowBuilder",
                    Policy = "Grandpa strategy rows are generated from transparent evaluation factors and candidate directions; they are policy-ranker samples and never executor calibration rows."
                }
            };
        }

        private static ActionFeatureVector BuildActionFeatures(CandidateDirection direction)
        {
            return new ActionFeatureVector
            {
                OptionIds = new[] { "strategy.grandpa_progress" },
                TrainingRole = TrainingRoles.StrategyValue,
                LearningScope = "policy_ranker",
                ExcludeFromPolicyTraining = false,
                Features = new FeatureVector
                {
                    Numeric = new[]
                    {
                        Number("action.option_count", 1),
                        Number("action.grandpa_potential_points", direction.PotentialPoints),
                        Number("action.grandpa_priority_score", direction.PriorityScore),
                        Number("action.required_minutes", EstimateRequiredMinutes(direction)),
                        Number("action.optional_minutes", 0)
                    },
                    Categorical = new[]
                    {
                        Category("action.primary_option_id", "strategy.grandpa_progress"),
                        Category("action.intent_category", OptionBehaviorCategories.LongTermStrategic),
                        Category("action.behavior_category", OptionBehaviorCategories.LongTermStrategic),
                        Category("action.training_role", TrainingRoles.StrategyValue),
                        Category("action.learning_scope", "policy_ranker"),
                        Category("action.grandpa_direction_id", direction.DirectionId),
                        Category("action.grandpa_direction_domain", direction.Domain),
                        Category("action.feedback_key", direction.FeedbackKey),
                        Category("action.execution_profile", "strategy_sample_no_executor")
                    },
                    Boolean = new[]
                    {
                        Flag("action.hard_blocked", false),
                        Flag("action.exclude_from_policy_training", false)
                    }
                }
            };
        }

        private static FeatureVector BuildStateFeatures(WorldModelEnvelope worldModel, TrainingSampleEnvelope sample)
        {
            return new FeatureVector
            {
                Numeric = new[]
                {
                    Number("game.time", ReadDouble(worldModel.Facts.Game, "time")),
                    Number("game.day", ReadDouble(worldModel.Facts.Game, "day")),
                    Number("game.year", ReadDouble(worldModel.Facts.Game, "year")),
                    Number("player.money", ReadDouble(worldModel.Facts.Player, "money")),
                    Number("player.energy", ReadDouble(worldModel.Facts.Player, "energy")),
                    Number("player.level", ReadDouble(worldModel.Facts.Player, "level")),
                    Number("player.total_money_earned", ReadDouble(worldModel.Facts.Player, "total_money_earned")),
                    Number("grandpa.current_score", sample.Target.CurrentValue),
                    Number("grandpa.target_score", sample.Target.TargetValue),
                    Number("grandpa.points_needed", sample.Target.PointsNeeded),
                    Number("grandpa.candidate_direction_count", sample.CandidateDirections.Length),
                    Number("completeness.unavailable_count", worldModel.Completeness.UnavailableCount),
                    Number("completeness.required_readable_ratio", ReadableRatio(worldModel))
                },
                Categorical = new[]
                {
                    Category("game.season", ReadString(worldModel.Facts.Game, "season")),
                    Category("game.weather", ReadString(worldModel.Facts.Game, "weather")),
                    Category("player.location_id", ReadString(worldModel.Facts.Player, "location_id")),
                    Category("world.mode", worldModel.Mode),
                    Category("goal.id", sample.GoalId)
                },
                Boolean = new[]
                {
                    Flag("completeness.all_required_facts_readable", worldModel.Completeness.AllRequiredFactsReadable),
                    Flag("planner_inputs.blocked", worldModel.PlannerInputs.Blocked),
                    Flag("grandpa.target_met", sample.Target.Complete)
                }
            };
        }

        public static int EstimateRequiredMinutes(CandidateDirection direction)
        {
            switch (direction.Domain)
            {
                case "economy":
                    return 240;
                case "social":
                    return 180;
                case "skills":
                case "exploration":
                    return 360;
                case "world_progress":
                    return 480;
                case "farm":
                    return 120;
                default:
                    return 240;
            }
        }

        private static double ReadableRatio(WorldModelEnvelope worldModel)
        {
            return worldModel.Completeness.RequiredFactCount == 0
                ? 0
                : Math.Round((double)worldModel.Completeness.ReadableRequiredFactCount / worldModel.Completeness.RequiredFactCount, 4);
        }

        private static double ReadDouble(IReadOnlyDictionary<string, JsonElement> facts, string key)
        {
            return facts.TryGetValue(key, out var value) && value.TryGetDouble(out var result) ? result : 0;
        }

        private static string ReadString(IReadOnlyDictionary<string, JsonElement> facts, string key)
        {
            return facts.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "unknown"
                : "unknown";
        }

        private static NumericFeature Number(string name, double value)
        {
            return new NumericFeature { Name = name, Value = value };
        }

        private static CategoricalFeature Category(string name, string value)
        {
            return new CategoricalFeature { Name = name, Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value };
        }

        private static BooleanFeature Flag(string name, bool value)
        {
            return new BooleanFeature { Name = name, Value = value };
        }
    }
}
