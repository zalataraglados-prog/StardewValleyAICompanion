using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class TrainingFeatureRowExporterTests
    {
        [Fact]
        public void BuildExportsStableStateActionAndLabelVectors()
        {
            var world = new WorldModelEnvelope
            {
                StateHash = "hash.before",
                Mode = "training",
                Completeness = new WorldModelCompleteness
                {
                    RequiredFactCount = 10,
                    ReadableRequiredFactCount = 8,
                    UnavailableCount = 2,
                    AllRequiredFactsReadable = false
                },
                PlannerInputs = new PlannerInputSummary
                {
                    Blocked = false
                },
                Facts = new WorldModelFacts
                {
                    Game = JsonObject("""{"time":610,"day":1,"year":3,"season":"spring","weather":"sun"}"""),
                    Player = JsonObject("""{"location_id":"Farm","money":500,"energy":270,"health":100,"level":25,"total_money_earned":1000000}"""),
                    Farm = JsonObject("""{"crops":[{"tile_x":1,"tile_y":2,"needs_watering":true},{"tile_x":3,"tile_y":4,"needs_watering":false}]}""")
                }
            };
            var episode = new TrainingEpisodeEnvelope
            {
                EpisodeId = "episode.test",
                SourceStateHash = "hash.before",
                QueueId = "queue.test",
                ActionSummary = new EpisodeActionSummary
                {
                    OptionIds = new[] { "farm.maintain_crops" },
                    ExecutionMode = "training_singleplayer",
                    Actor = new ActionActorRef { ActorType = "training_farmer" }
                },
                StrategyValue = new StrategyValueFeedback
                {
                    GoalProgressDelta = 0.09,
                    RewardTerms = new[]
                    {
                        new EpisodeRewardTerm { Name = "crop_watered", Value = 0.10 },
                        new EpisodeRewardTerm { Name = "energy_spent", Value = -0.01 }
                    }
                },
                HardFeasibility = new HardFeasibilityFeedback
                {
                    Blocked = false,
                    TimeBudget = new TimeBudgetReport
                    {
                        RequiredMinutes = 30,
                        OptionalMinutes = 0,
                        AvailableMinutes = 1070
                    }
                },
                ExecutorCalibration = new ExecutorCalibrationFeedback
                {
                    ExecutionProfile = "perfect_human_player"
                }
            };

            var row = new TrainingFeatureRowExporter().Build(world, episode);

            Assert.Equal("training_feature_row.v1", row.SchemaVersion);
            Assert.Equal("episode.test", row.EpisodeId);
            Assert.Contains(row.StateFeatures.Numeric, item => item.Name == "farm.crops_needing_watering" && item.Value == 1);
            Assert.Contains(row.StateFeatures.Numeric, item => item.Name == "completeness.required_readable_ratio" && item.Value == 0.8);
            Assert.Contains(row.StateFeatures.Categorical, item => item.Name == "game.season" && item.Value == "spring");
            Assert.Contains(row.ActionFeatures.Features.Categorical, item => item.Name == "action.intent_category" && item.Value == "mechanical");
            Assert.Equal("executor_calibration", row.ActionFeatures.TrainingRole);
            Assert.Equal("calibration_only", row.ActionFeatures.LearningScope);
            Assert.True(row.ActionFeatures.ExcludeFromPolicyTraining);
            Assert.Contains(row.ActionFeatures.Features.Categorical, item => item.Name == "action.training_role" && item.Value == "executor_calibration");
            Assert.Contains(row.ActionFeatures.Features.Boolean, item => item.Name == "action.exclude_from_policy_training" && item.Value);
            Assert.Contains(row.ActionFeatures.Features.Boolean, item => item.Name == "action.hard_blocked" && item.Value == false);
            Assert.Equal(0.09, row.Labels.GoalProgressDelta);
            Assert.Equal(0.09, row.Labels.TotalReward, 4);
            Assert.Equal(30, row.Labels.RequiredMinutes);
            Assert.Contains("crop_watered", row.Labels.RewardTermNames);
        }

        private static Dictionary<string, JsonElement> JsonObject(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.EnumerateObject()
                .ToDictionary(item => item.Name, item => item.Value.Clone());
        }
    }
}
