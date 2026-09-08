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
            return GrandpaDirectionCatalog.Entries
                .Select(spec => Direction(spec, factorMap, globalBlocked))
                .Where(direction => direction.PotentialPoints > 0 || !direction.Known)
                .OrderByDescending(direction => direction.PriorityScore)
                .ThenBy(direction => direction.DirectionId, StringComparer.Ordinal)
                .ToArray();
        }

        private static CandidateDirection Direction(GrandpaDirectionCatalogEntry spec, IReadOnlyDictionary<string, GrandpaEvaluationFactor> factors, bool globalBlocked)
        {
            var related = spec.CriterionIds
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
                DirectionId = spec.DirectionId,
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
            return path is
                "player.active_object_qualified_id" or
                "farm.grandpa_score";
        }

    }
}
