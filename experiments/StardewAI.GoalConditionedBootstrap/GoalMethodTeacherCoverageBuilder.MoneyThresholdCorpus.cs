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
        foreach (var row in corpus.Rows)
        {
            if (row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified)
            {
                throw new InvalidDataException(
                    "Raw Money-threshold corpus mixed Teacher preference with native outcome evidence.");
            }
            foreach (var threshold in row.CrossedThresholds)
            {
                if (threshold.TeacherComparisonVerified ||
                    !threshold.NativeTerminalOutcomeVerified ||
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
            }
        }

        return new GoalMethodTeacherCoverageSourceDigest(
            source.SourceId,
            source.SourceKind,
            corpusPath,
            CurrentTeacherFrontierSupport.HashFile(corpusPath),
            corpus.Rows.Length,
            0);
    }
}
