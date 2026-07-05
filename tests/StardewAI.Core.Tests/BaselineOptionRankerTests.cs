using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class BaselineOptionRankerTests
    {
        [Fact]
        public void RankUsesRegisteredOptionsWhenCandidatesAreEmpty()
        {
            var report = new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore
                    {
                        OptionId = "farm.maintain_crops",
                        ExampleCount = 2,
                        AverageGoalProgressDelta = 0.09,
                        AverageTotalReward = 0.09,
                        HardBlockRate = 0
                    }
                }
            };

            var prediction = new BaselineOptionRanker().Rank(report, Array.Empty<string>());

            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "farm.maintain_crops");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "social.gift_npc" && item.Evidence == "unseen_option");
            Assert.True(prediction.RankedOptions.Length >= 8);
            Assert.Equal("farm.maintain_crops", prediction.RankedOptions[0].OptionId);
        }
    }
}
