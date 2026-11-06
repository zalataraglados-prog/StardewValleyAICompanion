namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyFriendshipCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence,
        string dependencyExpansionPath)
    {
        var corpus = FriendshipTeacherCorpusBuilder.Verify(path);
        ApplyFriendshipCorpus(corpus, source.SourceKind, frontier, evidence, path, dependencyExpansionPath);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path),
            corpus.RowCount, corpus.Rows.Sum(row => row.StrictPairCount));
    }

    private static void ApplyFriendshipCorpus(FriendshipTeacherCorpus corpus, string sourceKind,
        GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence,
        string? corpusPath = null, string? dependencyExpansionPath = null)
    {
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256,
            "Friendship corpus authority does not match the current method frontier.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.Order(StringComparer.Ordinal)
            .SequenceEqual(corpus.CriterionCoverage.Select(row => row.CriterionId).Order(StringComparer.Ordinal)),
            "Friendship corpus does not map to the exact authoritative criteria.");
        if (methods[0].Status == "executable_frontier")
        {
            Require(!string.IsNullOrWhiteSpace(corpusPath) && !string.IsNullOrWhiteSpace(dependencyExpansionPath),
                "Executable Friendship method requires a separately verified method certificate, not only a threshold corpus.");
            Require(CurrentTeacherFrontierSupport.HashFile(dependencyExpansionPath!) == frontier.DependencyExpansionSha256,
                "Friendship method view changed after frontier construction.");
            FriendshipLocalDependencyViewBuilder.VerifyForCoverage(corpus, corpusPath!, dependencyExpansionPath!);
        }
        foreach (var criterion in corpus.CriterionCoverage)
        {
            if (criterion.TeacherComparisonPartitions.Length == 0 && criterion.NativeOutcomePartitions.Length == 0) continue;
            var target = evidence[criterion.CriterionId];
            target.TeacherSourceKinds.Add(sourceKind);
            target.TeacherComparisonPartitions.UnionWith(criterion.TeacherComparisonPartitions);
            target.NativeOutcomePartitions.UnionWith(criterion.NativeOutcomePartitions);
        }
    }
}
