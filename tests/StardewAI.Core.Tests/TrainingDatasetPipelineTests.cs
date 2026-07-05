using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class TrainingDatasetPipelineTests
    {
        [Fact]
        public void WriterAppendsJsonlAndBaselineTrainerAggregatesByOption()
        {
            var datasetPath = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"), "rows.jsonl");
            var writer = new JsonlTrainingDatasetWriter();

            var first = writer.Append(datasetPath, Row("row.one", "episode.one", "farm.maintain_crops", 0.09, false));
            var second = writer.Append(datasetPath, Row("row.two", "episode.two", "farm.maintain_crops", 0.05, true));

            Assert.Equal(1, first.RowCount);
            Assert.Equal(2, second.RowCount);
            Assert.True(File.Exists(datasetPath));

            var report = new BaselineFeatureRowTrainer().Train(datasetPath);

            Assert.Equal("baseline_training_report.v1", report.SchemaVersion);
            Assert.Equal(2, report.RowCount);
            var score = Assert.Single(report.OptionScores);
            Assert.Equal("farm.maintain_crops", score.OptionId);
            Assert.Equal(2, score.ExampleCount);
            Assert.Equal(0.07, score.AverageGoalProgressDelta);
            Assert.Equal(0.07, score.AverageTotalReward);
            Assert.Equal(0.5, score.HardBlockRate);
        }

        private static TrainingFeatureRowEnvelope Row(
            string rowId,
            string episodeId,
            string optionId,
            double reward,
            bool blocked)
        {
            return new TrainingFeatureRowEnvelope
            {
                RowId = rowId,
                EpisodeId = episodeId,
                SourceStateHash = "hash.before",
                QueueId = "queue.test",
                ActionFeatures = new ActionFeatureVector
                {
                    OptionIds = new[] { optionId }
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
