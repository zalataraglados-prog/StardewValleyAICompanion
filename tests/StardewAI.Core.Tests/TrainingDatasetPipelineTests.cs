using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class TrainingDatasetPipelineTests
    {
        [Fact]
        public void WriterAndBaselineTrainerSeparateCalibrationAndAdmissionExclusions()
        {
            var datasetPath = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"), "rows.jsonl");
            var writer = new JsonlTrainingDatasetWriter();

            var first = writer.Append(datasetPath, Row("row.one", "episode.one", "farm.maintain_crops", 0.09, false, true));
            var second = writer.Append(datasetPath, Row("row.two", "episode.two", "farm.maintain_crops", 0.05, true, true));
            writer.Append(datasetPath, Row("row.three", "episode.three", "economy.buy_supplies", 0.20, false, false));
            writer.Append(datasetPath, Row("row.four", "episode.four", "social.gift_npc", 0.30, false, false));

            Assert.Equal(1, first.RowCount);
            Assert.Equal(2, second.RowCount);
            Assert.True(File.Exists(datasetPath));

            var report = new BaselineFeatureRowTrainer().Train(datasetPath);

            Assert.Equal("baseline_training_report.v1", report.SchemaVersion);
            Assert.Equal(4, report.RowCount);
            Assert.Equal(1, report.IncludedRowCount);
            Assert.Equal(2, report.ExcludedCalibrationRowCount);
            Assert.Equal(1, report.ExcludedAdmissionRowCount);
            Assert.Equal(new[] { "economy.buy_supplies" }, report.ExcludedOptionIds);
            Assert.Equal(
                new[]
                {
                    "farm.collect_machine_outputs",
                    "fishing.collect_crab_pots", "fishing.service_fish_ponds", "foraging.clear_green_rain_bushes", "foraging.collect_spawned_objects", "foraging.harvest_bushes", "foraging.harvest_ginger", "foraging.pan_ore_spot",
                    "inventory.transfer_item",
                    "mining.claim_reward_chests", "mining.obtain_skull_key", "mining.reach_depth",
                    "skills.read_books", "social.gift_npc", "social.talk_npc", "volcano.reach_caldera"
                },
                report.TrainingAllowlist);
            Assert.Contains(PolicyTrainingAdmissionFilter.CalibrationExcludedReason, report.ExcludedReasons);
            Assert.Contains(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason, report.ExcludedReasons);
            var score = Assert.Single(report.OptionScores);
            Assert.Equal("social.gift_npc", score.OptionId);
            Assert.Equal(1, score.ExampleCount);
            Assert.Equal(0.30, score.AverageGoalProgressDelta);
            Assert.Equal(0.30, score.AverageTotalReward);
            Assert.Equal(0, score.HardBlockRate);
        }

        private static TrainingFeatureRowEnvelope Row(
            string rowId,
            string episodeId,
            string optionId,
            double reward,
            bool blocked,
            bool excludeFromPolicy)
        {
            return new TrainingFeatureRowEnvelope
            {
                RowId = rowId,
                EpisodeId = episodeId,
                SourceStateHash = "hash.before",
                QueueId = "queue.test",
                ActionFeatures = new ActionFeatureVector
                {
                    OptionIds = new[] { optionId },
                    TrainingRole = excludeFromPolicy ? "executor_calibration" : "strategy_value",
                    LearningScope = excludeFromPolicy ? "calibration_only" : "policy_ranker",
                    ExcludeFromPolicyTraining = excludeFromPolicy
                },
                Labels = new TrainingLabelVector
                {
                    GoalProgressDelta = reward,
                    TotalReward = reward,
                    HardBlocked = blocked
                }
            };
        }
    }
}
