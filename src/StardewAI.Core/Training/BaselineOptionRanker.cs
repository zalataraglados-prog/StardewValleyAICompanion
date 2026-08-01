using System;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class BaselineOptionRanker
    {
        private readonly PolicyTrainingAdmissionFilter admissionFilter;
        private readonly BaselinePolicyPredictor predictor;

        public BaselineOptionRanker()
            : this(new PolicyTrainingAdmissionFilter(), new BaselinePolicyPredictor())
        {
        }

        public BaselineOptionRanker(
            PolicyTrainingAdmissionFilter admissionFilter,
            BaselinePolicyPredictor predictor)
        {
            this.admissionFilter = admissionFilter;
            this.predictor = predictor;
        }

        public PolicyPredictionEnvelope Rank(
            BaselineTrainingReport report,
            string[] candidateOptionIds)
        {
            var candidates = candidateOptionIds.Length > 0
                ? admissionFilter.FilterOptionIds(candidateOptionIds)
                : admissionFilter.Allowlist.ToArray();
            if (candidateOptionIds.Length > 0 && candidates.Length == 0)
            {
                return new PolicyPredictionEnvelope();
            }

            return predictor.Predict(report, candidates);
        }
    }
}
