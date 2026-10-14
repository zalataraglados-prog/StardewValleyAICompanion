namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest
        VerifyMoneyThresholdCorpus(
            GoalMethodTeacherCoverageSource source,
            string corpusPath,
            GoalMethodFrontierReport frontier,
            IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        var corpus = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
        var methods = frontier.Methods.Where(method =>
                method.DirectionId == corpus.DirectionId &&
                method.MethodId == corpus.MethodId)
            .ToArray();
        if (methods.Length != 1)
        {
            throw new InvalidDataException(
                "Money-threshold corpus does not map to exactly one authoritative goal method.");
        }
        var methodCriteria = methods[0].CriterionIds
            .ToHashSet(StringComparer.Ordinal);
        var creditedComparisons = 0;
        foreach (var row in corpus.Rows)
        {
            if (!row.NativeTerminalOutcomeVerified)
            {
                throw new InvalidDataException(
                    "Money-threshold corpus contains an unverified native outcome.");
            }
            foreach (var threshold in row.CrossedThresholds)
            {
                if (!threshold.NativeTerminalOutcomeVerified ||
                    !methodCriteria.Contains(threshold.CriterionId) ||
                    !evidence.TryGetValue(
                        threshold.CriterionId,
                        out var criterionEvidence))
                {
                    throw new InvalidDataException(
                        "Money-threshold corpus derived a non-authoritative criterion.");
                }
                criterionEvidence.TeacherSourceKinds.Add(source.SourceKind);
                criterionEvidence.NativeOutcomePartitions.Add(
                    row.DatasetPartition);
                if (threshold.TeacherComparison is not null)
                {
                    criterionEvidence.TeacherComparisonPartitions.Add(
                        row.DatasetPartition);
                    creditedComparisons += threshold.TeacherComparison
                        .RejectedAlternatives.Length;
                }
            }
        }

        return new GoalMethodTeacherCoverageSourceDigest(
            source.SourceId,
            source.SourceKind,
            corpusPath,
            CurrentTeacherFrontierSupport.HashFile(corpusPath),
            corpus.Rows.Length,
            creditedComparisons);
    }
}
