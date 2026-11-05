namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyRustyKeyCorpus(
        GoalMethodTeacherCoverageSource source, string path, GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var corpus = RustyKeyTeacherCorpusBuilder.Verify(path);
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1, "Rusty Key corpus does not map to exactly one authoritative goal method.");
        var methodEvidence = evidence[methods[0].MethodId];
        methodEvidence.TeacherSourceKinds.Add(source.SourceKind);
        foreach (var row in corpus.Rows)
        {
            methodEvidence.TeacherComparisonPartitions.Add(row.Evidence.DatasetPartition);
            methodEvidence.NativeOutcomePartitions.Add(row.Evidence.DatasetPartition);
        }
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path), corpus.RowCount, corpus.RowCount);
    }
}
