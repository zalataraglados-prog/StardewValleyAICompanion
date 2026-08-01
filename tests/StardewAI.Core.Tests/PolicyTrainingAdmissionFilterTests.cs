using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class PolicyTrainingAdmissionFilterTests
    {
        [Fact]
        public void AllowsExactlyOneEvidenceAdmittedModelOption()
        {
            var filter = new PolicyTrainingAdmissionFilter();
            var decision = filter.Evaluate(Row("social.talk_npc"));

            Assert.True(decision.Included);
            Assert.False(decision.CalibrationExcluded);
            Assert.Equal(new[] { "social.talk_npc" }, decision.OptionIds);
            Assert.Empty(decision.Reasons);
        }

        [Fact]
        public void RejectsNonAdmittedAndMultiOptionPolicyRows()
        {
            var filter = new PolicyTrainingAdmissionFilter();

            var nonAdmitted = filter.Evaluate(Row("strategy.grandpa_progress"));
            Assert.False(nonAdmitted.Included);
            Assert.False(nonAdmitted.CalibrationExcluded);
            Assert.Equal(
                new[] { PolicyTrainingAdmissionFilter.OptionNotAdmittedReason },
                nonAdmitted.Reasons);

            var multiple = filter.Evaluate(Row("social.talk_npc", "social.gift_npc"));
            Assert.False(multiple.Included);
            Assert.Equal(
                new[] { PolicyTrainingAdmissionFilter.MultipleOptionsReason },
                multiple.Reasons);
        }

        [Fact]
        public void CalibrationExclusionTakesPrecedenceOverOptionAdmission()
        {
            var row = Row("social.gift_npc");
            row.ActionFeatures.ExcludeFromPolicyTraining = true;
            var decision = new PolicyTrainingAdmissionFilter().Evaluate(row);

            Assert.False(decision.Included);
            Assert.True(decision.CalibrationExcluded);
            Assert.Equal(
                new[] { PolicyTrainingAdmissionFilter.CalibrationExcludedReason },
                decision.Reasons);
        }

        private static TrainingFeatureRowEnvelope Row(params string[] optionIds)
        {
            return new TrainingFeatureRowEnvelope
            {
                ActionFeatures = new ActionFeatureVector
                {
                    OptionIds = optionIds,
                    TrainingRole = "strategy_value",
                    LearningScope = "policy_ranker"
                }
            };
        }
    }
}
