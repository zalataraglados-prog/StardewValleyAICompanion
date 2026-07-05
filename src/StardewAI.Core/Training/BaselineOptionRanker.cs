using System;
using System.Linq;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Training
{
    public sealed class BaselineOptionRanker
    {
        private readonly OptionRegistry.OptionRegistry optionRegistry;
        private readonly BaselinePolicyPredictor predictor;

        public BaselineOptionRanker()
            : this(new OptionRegistry.OptionRegistry(), new BaselinePolicyPredictor())
        {
        }

        public BaselineOptionRanker(
            OptionRegistry.OptionRegistry optionRegistry,
            BaselinePolicyPredictor predictor)
        {
            this.optionRegistry = optionRegistry;
            this.predictor = predictor;
        }

        public PolicyPredictionEnvelope Rank(
            BaselineTrainingReport report,
            string[] candidateOptionIds)
        {
            var candidates = candidateOptionIds.Length > 0
                ? candidateOptionIds
                : optionRegistry.All
                    .Select(option => option.OptionId)
                    .OrderBy(optionId => optionId, StringComparer.Ordinal)
                    .ToArray();

            return predictor.Predict(report, candidates);
        }
    }
}
