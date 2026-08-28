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

            Assert.Equal(39, prediction.RankedOptions.Length);
            Assert.DoesNotContain(prediction.RankedOptions, item => item.OptionId == "farm.maintain_crops");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "animals.manage_animal" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "animals.purchase" && item.Evidence == "unseen_option");
            Assert.DoesNotContain(prediction.RankedOptions, item => item.OptionId == "buildings.change_skin");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "buildings.construct" && item.Evidence == "unseen_option");
            Assert.DoesNotContain(prediction.RankedOptions, item => item.OptionId == "buildings.paint");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "crafting.cook_recipe" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "crafting.forge_item" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "economy.buy_supplies" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "economy.sell_items" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "economy.ship_items" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "farm.collect_animal_products" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "farm.care_for_pets" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "farming.collect_slime_ball" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "fishing.catch_fish" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "rewards.claim_pot_of_gold" && item.Evidence == "unseen_option");
            Assert.Contains(prediction.RankedOptions, item => item.OptionId == "rewards.claim_statue_blessing" && item.Evidence == "unseen_option");
            Assert.DoesNotContain(prediction.RankedOptions, item => item.OptionId == "world.rotate_house_plant");
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
