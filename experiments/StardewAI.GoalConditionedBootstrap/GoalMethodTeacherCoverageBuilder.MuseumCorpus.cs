namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyMuseumCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        var corpus = MuseumTeacherCorpusBuilder.Verify(path);
        ApplyMuseumCorpus(corpus, source.SourceKind, frontier, evidence);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path), corpus.RowCount, corpus.StrictPairCount);
    }

    private static void ApplyMuseumCorpus(MuseumTeacherCorpus corpus, string sourceKind, GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256 && corpus.RequirementInventorySha256 == frontier.RequirementInventorySha256,
            "Museum corpus does not share the current knowledge/95-item requirement authority.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.SequenceEqual(new[] { MuseumTeacherCorpus.CriterionId }),
            "Museum corpus does not map to exactly its authoritative full-collection criterion.");
        Require(methods[0].Status != "executable_frontier",
            "Executable Museum method requires its separately verified local method certificate, not only a terminal corpus.");
        var target = evidence[MuseumTeacherCorpus.CriterionId];
        target.TeacherSourceKinds.Add(sourceKind);
        target.TeacherComparisonPartitions.UnionWith(corpus.TeacherComparisonPartitions);
        target.NativeOutcomePartitions.UnionWith(corpus.NativeOutcomePartitions);
    }
}
