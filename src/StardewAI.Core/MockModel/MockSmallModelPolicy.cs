using System;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.MockModel
{
    public sealed class MockSmallModelPolicy
    {
        private readonly TaskIntentClassifier classifier;

        public MockSmallModelPolicy()
            : this(new TaskIntentClassifier())
        {
        }

        public MockSmallModelPolicy(TaskIntentClassifier classifier)
        {
            this.classifier = classifier;
        }

        public SmallModelActionEnvelope Generate(SnapshotEnvelope snapshot, string goal, string executionMode)
        {
            var classification = classifier.Classify(goal);
            var parameters = classification.OptionId == "strategy.grandpa_progress"
                ? BuildGrandpaStrategyParameters(snapshot, goal, executionMode)
                : classification.Parameters;
            var actor = string.Equals(executionMode, "coop_companion", StringComparison.Ordinal)
                ? new ActionActorRef
                {
                    ActorId = "ai_companion.main",
                    ActorType = "ai_companion",
                    ControlSurface = "companion_actor"
                }
                : new ActionActorRef
                {
                    ActorId = "training_farmer.main",
                    ActorType = "training_farmer",
                    ControlSurface = "training_sandbox"
                };

            return new SmallModelActionEnvelope
            {
                ModelOutputId = "mock_model_output." + Guid.NewGuid().ToString("N"),
                SourceModel = "mock-small-model.rule.v1",
                StateHash = snapshot.StateHash,
                GoalId = "goal.mock." + Guid.NewGuid().ToString("N"),
                ExecutionMode = string.IsNullOrWhiteSpace(executionMode) ? "training_singleplayer" : executionMode,
                Actor = actor,
                Actions = new[]
                {
                    new SmallModelAction
                    {
                        ActionId = "mock_action." + Guid.NewGuid().ToString("N"),
                        OptionId = classification.OptionId,
                        Rationale = "mock policy classified goal as " + classification.Category,
                        Parameters = AppendCategory(parameters, classification.Category)
                    }
                }
            };
        }

        private static SmallModelActionParameter[] BuildGrandpaStrategyParameters(SnapshotEnvelope snapshot, string goal, string executionMode)
        {
            var worldModel = new WorldModelProjector().Project(snapshot, goal, executionMode);
            var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
            var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);
            var eligibleDirection = sample.CandidateDirections
                .Where(item => item.Known && !item.Blocked && item.PotentialPoints > 0)
                .OrderByDescending(item => item.PriorityScore)
                .ThenBy(item => item.DirectionId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (eligibleDirection is null)
            {
                return NoEligibleDirectionParameters(report, sample);
            }

            return BuildDirectionParameters(report, eligibleDirection);
        }

        private static SmallModelActionParameter[] NoEligibleDirectionParameters(
            GrandpaEvaluationGoalReport report,
            TrainingSampleEnvelope sample)
        {
            var blockReasons = new System.Collections.Generic.List<string>
            {
                "no_eligible_direction_available"
            };

            if (sample.PlannerState.Blocked)
            {
                blockReasons.Add("sample_planner_blocked");
            }

            if (sample.Target.Complete)
            {
                blockReasons.Add("target_already_met");
            }

            var knownBlocked = sample.CandidateDirections
                .Where(item => item.Known && item.Blocked)
                .ToArray();
            if (knownBlocked.Length > 0)
            {
                blockReasons.Add("all_known_directions_blocked:" + string.Join(",", knownBlocked.Select(item => item.DirectionId)));
            }

            var knownNoPotential = sample.CandidateDirections
                .Where(item => item.Known && !item.Blocked && item.PotentialPoints <= 0)
                .ToArray();
            if (knownNoPotential.Length > 0)
            {
                blockReasons.Add("all_positive_directions_have_zero_potential:" + string.Join(",", knownNoPotential.Select(item => item.DirectionId)));
            }

            var unknownDirections = sample.CandidateDirections
                .Where(item => !item.Known)
                .ToArray();
            if (unknownDirections.Length > 0)
            {
                blockReasons.Add("unknown_directions_present:" + string.Join(",", unknownDirections.Select(item => item.DirectionId)));
            }

            return new[]
            {
                Parameter("strategic_goal", GrandpaEvaluationGoalDefinition.StrategicGoal),
                Parameter("target_score", report.TargetScore.ToString()),
                Parameter("direction_id", string.Empty),
                Parameter("direction_domain", "blocked"),
                Parameter("potential_points", "0"),
                Parameter("priority_score", "0"),
                Parameter("feedback_key", "grandpa.no_eligible_direction"),
                Parameter("required_minutes", "-1"),
                Parameter("optional_minutes", "-1"),
                Parameter("requires_direction_selection", "failed_no_eligible_candidate"),
                Parameter("block_reason", string.Join("; ", blockReasons)),
                Parameter("candidate_direction_count", sample.CandidateDirections.Length.ToString()),
                Parameter("planner_blocked", sample.PlannerState.Blocked.ToString().ToLowerInvariant()),
                Parameter("target_complete", sample.Target.Complete.ToString().ToLowerInvariant())
            };
        }

        private static SmallModelActionParameter[] BuildDirectionParameters(
            GrandpaEvaluationGoalReport report,
            CandidateDirection direction)
        {
            return new[]
            {
                Parameter("strategic_goal", GrandpaEvaluationGoalDefinition.StrategicGoal),
                Parameter("target_score", report.TargetScore.ToString()),
                Parameter("direction_id", direction.DirectionId),
                Parameter("direction_domain", string.IsNullOrWhiteSpace(direction.Domain) ? "unknown" : direction.Domain),
                Parameter("potential_points", direction.PotentialPoints.ToString()),
                Parameter("priority_score", direction.PriorityScore.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Parameter("feedback_key", direction.FeedbackKey),
                Parameter("required_minutes", GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(direction).ToString()),
                Parameter("optional_minutes", "0"),
                Parameter("requires_direction_selection", "false"),
                Parameter("direction_known", direction.Known.ToString().ToLowerInvariant()),
                Parameter("direction_blocked", direction.Blocked.ToString().ToLowerInvariant()),
                Parameter("candidate_direction_count", "1"),
                Parameter("planner_blocked", "false"),
                Parameter("target_complete", "false")
            };
        }

        private static SmallModelActionParameter[] AppendCategory(SmallModelActionParameter[] parameters, string category)
        {
            var result = new SmallModelActionParameter[parameters.Length + 1];
            Array.Copy(parameters, result, parameters.Length);
            result[result.Length - 1] = new SmallModelActionParameter
            {
                Name = "intent_category",
                Value = category
            };
            return result;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value
            };
        }
    }
}
