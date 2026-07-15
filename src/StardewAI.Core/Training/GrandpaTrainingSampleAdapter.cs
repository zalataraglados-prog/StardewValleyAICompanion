using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaTrainingSampleAdapter
    {
        public TrainingSampleEnvelope Build(WorldModelEnvelope worldModel, GrandpaEvaluationGoalReport goalReport)
        {
            var blockingMissingFacts = goalReport.MissingFactPaths
                .Where(path => !IsNonBlockingEvaluationContextFact(path))
                .ToArray();
            var blocked = blockingMissingFacts.Length > 0;
            var directions = BuildDirections(goalReport, blocked);

            return new TrainingSampleEnvelope
            {
                SampleId = "training.grandpa." + Guid.NewGuid().ToString("N"),
                SourceStateHash = worldModel.StateHash,
                SourceWorldModelSchema = worldModel.SchemaVersion,
                GoalId = goalReport.GoalId,
                Target = new TrainingTarget
                {
                    TargetValue = goalReport.TargetScore,
                    CurrentValue = goalReport.CurrentScore,
                    PointsNeeded = goalReport.PointsNeeded,
                    Complete = goalReport.TargetMet
                },
                PlannerState = new PlannerGoalState
                {
                    Blocked = blocked,
                    BlockReasons = blocked ? new[] { "missing_required_transparent_facts" } : Array.Empty<string>(),
                    MissingFactPaths = goalReport.MissingFactPaths,
                    EvaluationContext = DescribeContext(goalReport)
                },
                CandidateDirections = directions,
                Feedback = new TrainingFeedback
                {
                    ExecutorRequired = false,
                    AvailableNow = false,
                    ObservedDelta = new ObservedStateDelta
                    {
                        BeforeStateHash = worldModel.StateHash
                    }
                }
            };
        }

        private static CandidateDirection[] BuildDirections(GrandpaEvaluationGoalReport report, bool globalBlocked)
        {
            if (report.TargetMet)
            {
                return Array.Empty<CandidateDirection>();
            }

            var factorMap = report.Factors.ToDictionary(factor => factor.Id, StringComparer.Ordinal);
            var specs = new[]
            {
                Spec("earn_money", "economy", "Increase total money earned", "grandpa.money", new[] { "money_50000", "money_100000", "money_200000", "money_300000", "money_500000", "money_1000000" }),
                Spec("complete_museum_collection", "world_progress", "Complete museum collection achievement", "grandpa.achievement.5", new[] { "achievement_complete_collection" }),
                Spec("obtain_skull_key", "exploration", "Obtain Skull Key", "grandpa.skull_key", new[] { "skull_key" }),
                Spec("complete_community_center", "world_progress", "Complete Community Center", "grandpa.community_center", new[] { "community_center_access_or_completion", "community_center_accessible_bonus" }),
                Spec("complete_joja_development", "world_progress", "Complete Joja development", "grandpa.joja_development", new[] { "joja_development_completed" }),
                Spec("marriage_and_house_upgrade", "social", "Marry or get roommate and upgrade farmhouse", "grandpa.marriage_house", new[] { "married_or_roommate_house_2" }),
                Spec("obtain_rusty_key", "world_progress", "Obtain Rusty Key", "grandpa.rusty_key", new[] { "rusty_key" }),
                Spec("complete_master_angler", "world_progress", "Complete Master Angler achievement", "grandpa.achievement.26", new[] { "achievement_master_angler" }),
                Spec("complete_full_shipment", "economy", "Complete Full Shipment achievement", "grandpa.achievement.34", new[] { "achievement_full_shipment" }),
                Spec("raise_friendships", "social", "Raise NPC friendships", "grandpa.friendships", new[] { "friendships_5", "friendships_10" }),
                Spec("raise_skill_levels", "skills", "Raise total skill level", "grandpa.level", new[] { "player_level_15", "player_level_25" }),
                Spec("earn_pet_love", "farm", "Earn pet love", "grandpa.pet_love", new[] { "pet_love" })
            };

            return specs
                .Select(spec => Direction(spec, factorMap, globalBlocked))
                .Where(direction => direction.PotentialPoints > 0 || !direction.Known)
                .OrderByDescending(direction => direction.PriorityScore)
                .ThenBy(direction => direction.DirectionId, StringComparer.Ordinal)
                .ToArray();
        }

        private static CandidateDirection Direction(DirectionSpec spec, IReadOnlyDictionary<string, GrandpaEvaluationFactor> factors, bool globalBlocked)
        {
            var related = spec.FactorIds
                .Where(factors.ContainsKey)
                .Select(id => factors[id])
                .ToArray();
            var unknown = related.Where(factor => !factor.Known).ToArray();
            var open = related.Where(factor => factor.Known && !factor.Satisfied).ToArray();
            var potential = open.Sum(factor => factor.MaxPoints);
            var blocked = globalBlocked || unknown.Length > 0;
            var reasons = new List<string>();
            if (globalBlocked)
            {
                reasons.Add("sample_missing_required_facts");
            }
            if (unknown.Length > 0)
            {
                reasons.Add("direction_has_unknown_factors:" + string.Join(",", unknown.Select(factor => factor.Id)));
            }

            return new CandidateDirection
            {
                DirectionId = spec.Id,
                Domain = spec.Domain,
                Label = spec.Label,
                RelatedFactorIds = related.Select(factor => factor.Id).ToArray(),
                PotentialPoints = potential,
                Known = unknown.Length == 0,
                Blocked = blocked,
                BlockReasons = reasons.ToArray(),
                PriorityScore = Score(spec.Domain, potential, blocked),
                FeedbackKey = spec.FeedbackKey
            };
        }

        private static double Score(string domain, int potentialPoints, bool blocked)
        {
            var domainWeight = domain switch
            {
                "farm" => 1.2,
                "economy" => 1.1,
                "skills" => 1.0,
                "social" => 0.95,
                "world_progress" => 0.9,
                "exploration" => 0.85,
                _ => 0.75
            };
            var blockedPenalty = blocked ? 0.25 : 1.0;
            return Math.Round(potentialPoints * domainWeight * blockedPenalty, 4);
        }

        private static string DescribeContext(GrandpaEvaluationGoalReport report)
        {
            var context = report.EvaluationContext;
            return $"year={context.Year?.ToString() ?? "unknown"}; recorded_candles={context.RecordedGrandpaCandles?.ToString() ?? "unknown"}; reevaluation_available={context.ReevaluationAvailable?.ToString() ?? "unknown"}; holding_reevaluation_item={context.HoldingReevaluationItem?.ToString() ?? "unknown"}";
        }

        private static bool IsNonBlockingEvaluationContextFact(string path)
        {
            return string.Equals(path, "player.active_object_qualified_id", StringComparison.Ordinal);
        }

        private static DirectionSpec Spec(string id, string domain, string label, string feedbackKey, string[] factorIds)
        {
            return new DirectionSpec(id, domain, label, feedbackKey, factorIds);
        }

        private sealed class DirectionSpec
        {
            public DirectionSpec(string id, string domain, string label, string feedbackKey, string[] factorIds)
            {
                Id = id;
                Domain = domain;
                Label = label;
                FeedbackKey = feedbackKey;
                FactorIds = factorIds;
            }

            public string Id { get; }

            public string Domain { get; }

            public string Label { get; }

            public string FeedbackKey { get; }

            public string[] FactorIds { get; }
        }
    }
}
