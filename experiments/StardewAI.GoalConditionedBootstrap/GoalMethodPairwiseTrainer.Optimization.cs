namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class GoalMethodPairwiseTrainer
{
    private static TrainingPair[] BuildPairs(
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusRow> rows,
        GoalMethodPairwiseLinearModel model)
    {
        var pairs = new List<TrainingPair>();
        foreach (var row in rows)
        {
            var teacher = row.SupervisionRow.Payload.TeacherPreference;
            var candidates = teacher.CandidateEvaluations.ToDictionary(
                candidate => candidate.ProposalId,
                StringComparer.Ordinal);
            foreach (var preference in teacher.PairwisePreferences)
            {
                var preferred = candidates[preference.PreferredProposalId];
                var alternative = candidates[
                    preference.AlternativeProposalId];
                var positive = GoalMethodPairwiseFeatureEncoder.Encode(
                    row,
                    preferred,
                    model);
                var negative = GoalMethodPairwiseFeatureEncoder.Encode(
                    row,
                    alternative,
                    model);
                var difference = new double[positive.Length];
                for (var index = 0; index < difference.Length; index++)
                    difference[index] = positive[index] - negative[index];
                pairs.Add(new TrainingPair(difference));
            }
        }
        return pairs.ToArray();
    }

    private static void Optimize(
        double[] weights,
        IReadOnlyList<TrainingPair> pairs,
        GoalMethodPairwiseHyperparameters parameters)
    {
        var gradient = new double[weights.Length];
        for (var epoch = 0; epoch < parameters.Epochs; epoch++)
        {
            Array.Clear(gradient, 0, gradient.Length);
            foreach (var pair in pairs)
            {
                var margin = Dot(weights, pair.Difference);
                var negativeProbability = margin >= 0
                    ? Math.Exp(-margin) / (1 + Math.Exp(-margin))
                    : 1 / (1 + Math.Exp(margin));
                for (var index = 0; index < gradient.Length; index++)
                {
                    gradient[index] -=
                        negativeProbability * pair.Difference[index];
                }
            }
            for (var index = 0; index < gradient.Length; index++)
            {
                gradient[index] = gradient[index] / pairs.Count +
                    parameters.L2Regularization * weights[index];
                weights[index] -= parameters.LearningRate * gradient[index];
            }
        }
    }

    private static GoalMethodPairwiseTrainingSummary BuildSummary(
        GoalMethodPairwiseLinearModel model,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] trainRows,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] validationRows,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] testRows,
        IReadOnlyList<TrainingPair> trainPairs)
    {
        var validationPairs = BuildPairs(validationRows, model);
        var testPairs = BuildPairs(testRows, model);
        return new GoalMethodPairwiseTrainingSummary
        {
            TrainRows = trainRows.Length,
            TrainPairs = trainPairs.Count,
            TrainPairAccuracy = Accuracy(model.Weights, trainPairs),
            ValidationRows = validationRows.Length,
            ValidationPairs = validationPairs.Length,
            ValidationPairAccuracy = Accuracy(
                model.Weights,
                validationPairs),
            TestRows = testRows.Length,
            TestPairs = testPairs.Length,
            TestPairAccuracy = Accuracy(model.Weights, testPairs),
            FeatureCount = model.FeatureNames.Length
        };
    }

    private static double Accuracy(
        double[] weights,
        IReadOnlyList<TrainingPair> pairs)
    {
        if (pairs.Count == 0)
            throw new InvalidDataException(
                "Goal-method evaluation partition has no pairs.");
        return Math.Round(
            (double)pairs.Count(pair =>
                Dot(weights, pair.Difference) > 0) / pairs.Count,
            6);
    }

    private static double Dot(double[] left, double[] right)
    {
        var result = 0d;
        for (var index = 0; index < left.Length; index++)
            result += left[index] * right[index];
        return result;
    }

    private sealed record TrainingPair(double[] Difference);
}
