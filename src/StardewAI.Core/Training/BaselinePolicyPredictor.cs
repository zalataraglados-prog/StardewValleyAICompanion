using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class BaselinePolicyPredictor
    {
        private const double BlockRiskPenalty = 0.25;
        private const double UnseenOptionPenalty = 0.05;

        public PolicyPredictionEnvelope Predict(
            BaselineTrainingReport report,
            IReadOnlyCollection<string> candidateOptionIds)
        {
            var candidates = candidateOptionIds.Count > 0
                ? candidateOptionIds.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray()
                : report.OptionScores.Select(item => item.OptionId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var scoresByOption = report.OptionScores.ToDictionary(item => item.OptionId, StringComparer.Ordinal);
            var predictions = candidates
                .Select(optionId => PredictOption(optionId, scoresByOption))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.OptionId, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < predictions.Length; i++)
            {
                predictions[i].Rank = i + 1;
            }

            return new PolicyPredictionEnvelope
            {
                RankedOptions = predictions
            };
        }

        private static PolicyOptionPrediction PredictOption(
            string optionId,
            IReadOnlyDictionary<string, BaselineOptionScore> scoresByOption)
        {
            if (!scoresByOption.TryGetValue(optionId, out var score))
            {
                return new PolicyOptionPrediction
                {
                    OptionId = optionId,
                    Score = -UnseenOptionPenalty,
                    ExpectedReward = 0,
                    ExpectedGoalProgressDelta = 0,
                    HardBlockRisk = 1,
                    ExampleCount = 0,
                    Evidence = "unseen_option"
                };
            }

            return new PolicyOptionPrediction
            {
                OptionId = optionId,
                Score = Math.Round(score.AverageTotalReward + score.AverageGoalProgressDelta - score.HardBlockRate * BlockRiskPenalty, 4),
                ExpectedReward = score.AverageTotalReward,
                ExpectedGoalProgressDelta = score.AverageGoalProgressDelta,
                HardBlockRisk = score.HardBlockRate,
                ExampleCount = score.ExampleCount,
                Evidence = "baseline_option_score"
            };
        }
    }
}
