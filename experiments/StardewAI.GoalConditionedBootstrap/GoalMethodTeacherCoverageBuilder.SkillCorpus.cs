namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifySkillCorpus(GoalMethodTeacherCoverageSource source,
        string path, GoalMethodFrontierReport frontier, IReadOnlyDictionary<string, CriterionCoverageEvidence> evidence)
    {
        var corpus = SkillTeacherCorpusBuilder.Verify(path);
        Require(corpus.KnowledgeSha256 == frontier.KnowledgeSha256,
            "Skills corpus authority does not match the current method frontier.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1 && methods[0].CriterionIds.Order(StringComparer.Ordinal)
            .SequenceEqual(corpus.CriterionCoverage.Select(row => row.CriterionId).Order(StringComparer.Ordinal)),
            "Skills corpus does not map to the exact authoritative criteria.");
        foreach (var criterion in corpus.CriterionCoverage)
        {
            if (criterion.TeacherComparisonPartitions.Length == 0 && criterion.NativeOutcomePartitions.Length == 0) continue;
            var target = evidence[criterion.CriterionId];
            target.TeacherSourceKinds.Add(source.SourceKind);
            target.TeacherComparisonPartitions.UnionWith(criterion.TeacherComparisonPartitions);
            target.NativeOutcomePartitions.UnionWith(criterion.NativeOutcomePartitions);
        }
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path),
            corpus.RowCount, corpus.Rows.Sum(row => row.StrictPairCount));
    }
}
