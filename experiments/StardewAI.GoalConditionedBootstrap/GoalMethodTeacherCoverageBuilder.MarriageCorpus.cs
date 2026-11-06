namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyMarriageCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence,
        string dependencyExpansionPath)
    {
        var corpus = MarriageTeacherCorpusBuilder.Verify(path);
        ApplyMarriageCorpus(corpus, source.SourceKind, frontier, evidence, path, dependencyExpansionPath);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path),
            corpus.RowCount, corpus.Runs.Sum(run => run.PreparationDecisions.Sum(row => row.StrictPairCount)));
    }

    private static void ApplyMarriageCorpus(MarriageTeacherCorpus corpus, string sourceKind,
        GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence,
        string? corpusPath = null, string? dependencyExpansionPath = null)
    {
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256,
            "Marriage corpus authority does not match the current method frontier.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.SequenceEqual(new[] { MarriageTeacherCorpus.CriterionId }),
            "Marriage corpus does not map to the exact authoritative joint criterion.");
        if (methods[0].Status == "executable_frontier")
        {
            Require(!string.IsNullOrWhiteSpace(corpusPath) && !string.IsNullOrWhiteSpace(dependencyExpansionPath),
                "Executable Marriage method requires its separately verified local method certificate, not only a joint corpus.");
            Require(CurrentTeacherFrontierSupport.HashFile(dependencyExpansionPath!) == frontier.DependencyExpansionSha256,
                "Marriage method view changed after frontier construction.");
            MarriageLocalDependencyViewBuilder.VerifyForCoverage(corpus, corpusPath!, dependencyExpansionPath!);
        }
        var criterion = corpus.CriterionCoverage.Single();
        var target = evidence[criterion.CriterionId];
        target.TeacherSourceKinds.Add(sourceKind);
        target.TeacherComparisonPartitions.UnionWith(criterion.TeacherComparisonPartitions);
        target.NativeOutcomePartitions.UnionWith(criterion.NativeOutcomePartitions);
    }
}
