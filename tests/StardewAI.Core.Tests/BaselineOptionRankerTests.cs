using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class BaselineOptionRankerTests
    {
        [Fact]
        public void RankUsesOnlyEvidenceAdmittedOptionsWhenCandidatesAreEmpty()
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

            Assert.Equal(15, prediction.RankedOptions.Length);
            Assert.DoesNotContain(prediction.RankedOptions, item => item.OptionId == "farm.maintain_crops");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "social.gift_npc" && item.Evidence == "unseen_option");
        }

        [Fact]
        public void RankFiltersExplicitCandidatesThroughTheSameAdmissionSource()
        {
            var prediction = new BaselineOptionRanker().Rank(
                new BaselineTrainingReport(),
                new[] { "recovery.stabilize_day", "social.gift_npc", "social.gift_npc" });

            var option = Assert.Single(prediction.RankedOptions);
            Assert.Equal("social.gift_npc", option.OptionId);
        }

        [Fact]
        public void RankFailsClosedWhenEveryExplicitCandidateIsNotAdmitted()
        {
            var report = new BaselineTrainingReport
            {
                OptionScores = new[]
                {
                    new BaselineOptionScore
                    {
                        OptionId = "recovery.stabilize_day",
                        ExampleCount = 10,
                        AverageTotalReward = 10
                    }
                }
            };

            var prediction = new BaselineOptionRanker().Rank(
                report,
                new[] { "recovery.stabilize_day" });

            Assert.Empty(prediction.RankedOptions);
        }
    }
}
