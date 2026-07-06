using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class TrainingDatasetPipelineTests
    {
        [Fact]
        public void WriterAppendsJsonlAndBaselineTrainerExcludesCalibrationRowsFromPolicyScores()
        {
            var datasetPath = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"), "rows.jsonl");
            var writer = new JsonlTrainingDatasetWriter();

            var first = writer.Append(datasetPath, Row("row.one", "episode.one", "farm.maintain_crops", 0.09, false, true));
            var second = writer.Append(datasetPath, Row("row.two", "episode.two", "farm.maintain_crops", 0.05, true, true));
            writer.Append(datasetPath, Row("row.three", "episode.three", "economy.buy_supplies", 0.20, false, false));

            Assert.Equal(1, first.RowCount);
            Assert.Equal(2, second.RowCount);
            Assert.True(File.Exists(datasetPath));

            var report = new BaselineFeatureRowTrainer().Train(datasetPath);

            Assert.Equal("baseline_training_report.v1", report.SchemaVersion);
            Assert.Equal(3, report.RowCount);
            Assert.Equal(1, report.IncludedRowCount);
            Assert.Equal(2, report.ExcludedCalibrationRowCount);
            var score = Assert.Single(report.OptionScores);
            Assert.Equal("economy.buy_supplies", score.OptionId);
            Assert.Equal(1, score.ExampleCount);
            Assert.Equal(0.20, score.AverageGoalProgressDelta);
            Assert.Equal(0.20, score.AverageTotalReward);
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
