namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyMuseumCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence, string dependencyExpansionPath)
    {
        var corpus = MuseumTeacherCorpusBuilder.Verify(path);
        ApplyMuseumCorpus(corpus, source.SourceKind, frontier, evidence, path, dependencyExpansionPath);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path), corpus.RowCount, corpus.StrictPairCount);
    }

    private static void ApplyMuseumCorpus(MuseumTeacherCorpus corpus, string sourceKind, GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence, string? corpusPath = null, string? dependencyExpansionPath = null)
    {
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256 && corpus.RequirementInventorySha256 == frontier.RequirementInventorySha256,
            "Museum corpus does not share the current knowledge/95-item requirement authority.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.SequenceEqual(new[] { MuseumTeacherCorpus.CriterionId }),
            "Museum corpus does not map to exactly its authoritative full-collection criterion.");
        if (methods[0].Status == "executable_frontier")
        {
            Require(!string.IsNullOrWhiteSpace(corpusPath) && !string.IsNullOrWhiteSpace(dependencyExpansionPath),
                "Executable Museum method requires its separately verified local method certificate, not only a terminal corpus.");
            Require(CurrentTeacherFrontierSupport.HashFile(dependencyExpansionPath!) == frontier.DependencyExpansionSha256,
                "Museum method view changed after frontier construction.");
            MuseumLocalDependencyViewBuilder.VerifyForCoverage(corpus, corpusPath!, dependencyExpansionPath!);
        }
        var target = evidence[MuseumTeacherCorpus.CriterionId];
        target.TeacherSourceKinds.Add(sourceKind);
        target.TeacherComparisonPartitions.UnionWith(corpus.TeacherComparisonPartitions);
        target.NativeOutcomePartitions.UnionWith(corpus.NativeOutcomePartitions);
    }
}
