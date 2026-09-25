namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyPetLoveCorpus(
        GoalMethodTeacherCoverageSource source,
        string corpusPath,
        GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var corpus = PetLoveTeacherCorpusBuilder.Verify(corpusPath);
        var methods = frontier.Methods
            .Where(method => method.DirectionId == corpus.DirectionId &&
                method.MethodId == corpus.MethodId)
            .ToArray();
        if (methods.Length != 1)
        {
            throw new InvalidDataException(
                "Pet-love corpus does not map to exactly one authoritative goal method.");
        }
        var method = methods[0];
        var methodEvidence = evidence[method.MethodId];
        methodEvidence.TeacherSourceKinds.Add(source.SourceKind);
        foreach (var row in corpus.Rows)
        {
            if (!row.TeacherComparisonVerified ||
                !row.NativeTerminalOutcomeVerified)
            {
                throw new InvalidDataException(
                    "Pet-love corpus contains an unverified supervision channel.");
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
