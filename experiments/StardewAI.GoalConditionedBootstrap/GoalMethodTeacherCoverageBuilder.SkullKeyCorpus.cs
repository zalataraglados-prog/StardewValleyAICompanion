namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifySkullKeyCorpus(
        GoalMethodTeacherCoverageSource source,
        string corpusPath,
        GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var corpus = SkullKeyTeacherCorpusBuilder.Verify(corpusPath);
        var methods = frontier.Methods
            .Where(method => method.DirectionId == corpus.DirectionId &&
                method.MethodId == corpus.MethodId)
            .ToArray();
        if (methods.Length != 1)
        {
            throw new InvalidDataException(
                "Skull Key corpus does not map to exactly one authoritative goal method.");
        }
        var methodEvidence = evidence[methods[0].MethodId];
        methodEvidence.TeacherSourceKinds.Add(source.SourceKind);
        foreach (var row in corpus.Rows)
        {
            if (!row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified)
            {
                throw new InvalidDataException(
                    "Skull Key corpus contains an unverified supervision channel.");
            }
            methodEvidence.TeacherComparisonPartitions.Add(
                row.DatasetPartition);
            methodEvidence.NativeOutcomePartitions.Add(
                row.DatasetPartition);
        }

        return new GoalMethodTeacherCoverageSourceDigest(
            source.SourceId,
            source.SourceKind,
            corpusPath,
            CurrentTeacherFrontierSupport.HashFile(corpusPath),
            corpus.Rows.Length,
            corpus.Rows.Length);
    }
}
