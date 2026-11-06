namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyMarriageCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        var corpus = MarriageTeacherCorpusBuilder.Verify(path);
        ApplyMarriageCorpus(corpus, source.SourceKind, frontier, evidence);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path),
            corpus.RowCount, corpus.Runs.Sum(run => run.PreparationDecisions.Sum(row => row.StrictPairCount)));
    }

    private static void ApplyMarriageCorpus(MarriageTeacherCorpus corpus, string sourceKind,
        GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256,
            "Marriage corpus authority does not match the current method frontier.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.SequenceEqual(new[] { MarriageTeacherCorpus.CriterionId }),
            "Marriage corpus does not map to the exact authoritative joint criterion.");
        Require(methods[0].Status != "executable_frontier",
            "Marriage preparation corpus alone cannot certify an executable method; a separately verified local method is still required.");
        var criterion = corpus.CriterionCoverage.Single();
        var target = evidence[criterion.CriterionId];
        target.TeacherSourceKinds.Add(sourceKind);
        target.TeacherComparisonPartitions.UnionWith(criterion.TeacherComparisonPartitions);
        target.NativeOutcomePartitions.UnionWith(criterion.NativeOutcomePartitions);
    }
}
