using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class BaselinePolicyPredictorTests
    {
        [Fact]
        public void PredictRanksOptionsByRewardProgressAndBlockRisk()
        {
            var report = new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore
                    {
                        OptionId = "farm.maintain_crops",
                        ExampleCount = 5,
                        AverageGoalProgressDelta = 0.09,
                        AverageTotalReward = 0.09,
                        HardBlockRate = 0
                    },
                    new BaselineOptionScore
                    {
                        OptionId = "exploration.visit_location",
                        ExampleCount = 3,
                        AverageGoalProgressDelta = 0.20,
                        AverageTotalReward = 0.20,
                        HardBlockRate = 1
                    }
                }
            };

            var prediction = new BaselinePolicyPredictor().Predict(report, new[]
            {
                "farm.maintain_crops",
                "exploration.visit_location",
                "social.gift_npc"
            });

            Assert.Equal("policy_prediction.v1", prediction.SchemaVersion);
            Assert.Equal("farm.maintain_crops", prediction.RankedOptions[0].OptionId);
            Assert.Equal(1, prediction.RankedOptions[0].Rank);
            Assert.Equal(0.18, prediction.RankedOptions[0].Score);
            Assert.Equal("exploration.visit_location", prediction.RankedOptions[1].OptionId);
            Assert.Equal(0.15, prediction.RankedOptions[1].Score);
            Assert.Equal("social.gift_npc", prediction.RankedOptions[2].OptionId);
            Assert.Equal("unseen_option", prediction.RankedOptions[2].Evidence);
            Assert.Equal(1, prediction.RankedOptions[2].HardBlockRisk);
        }

        [Fact]
        public void PredictUsesReportOptionsWhenNoCandidatesProvided()
        {
            var report = new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore { OptionId = "farm.maintain_crops", ExampleCount = 1 },
                    new BaselineOptionScore { OptionId = "recovery.stabilize_day", ExampleCount = 1 }
                }
            };

            var prediction = new BaselinePolicyPredictor().Predict(report, Array.Empty<string>());

            Assert.Equal(2, prediction.RankedOptions.Length);
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "farm.maintain_crops");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "recovery.stabilize_day");
        }
    }
}
