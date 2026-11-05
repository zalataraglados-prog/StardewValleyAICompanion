namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodTeacherCoverageBuilder
{
    private static GoalMethodTeacherCoverageSourceDigest VerifyMasterAnglerCorpus(
        GoalMethodTeacherCoverageSource source, string path, GoalMethodFrontierReport frontier,
        IReadOnlyDictionary<string, MethodCoverageEvidence> evidence)
    {
        var corpus = MasterAnglerTeacherCorpusBuilder.Verify(path);
        Require(corpus.Rows.All(row =>
                row.Evidence.SourceSha256["native.requirement_inventory"] == frontier.RequirementInventorySha256 &&
                row.Evidence.SourceSha256["native.acquisition_lowering"] == frontier.AcquisitionRouteLoweringSha256),
            "Master Angler corpus authority does not match the current method frontier.");
        var methods = frontier.Methods.Where(method => method.DirectionId == corpus.DirectionId && method.MethodId == corpus.MethodId).ToArray();
        Require(methods.Length == 1, "Master Angler corpus does not map to exactly one authoritative goal method.");
        var methodEvidence = evidence[methods[0].MethodId];
        methodEvidence.TeacherSourceKinds.Add(source.SourceKind);
        methodEvidence.TeacherComparisonPartitions.UnionWith(corpus.TeacherComparisonPartitions);
        methodEvidence.NativeOutcomePartitions.UnionWith(corpus.NativeOutcomePartitions);
        return new(source.SourceId, source.SourceKind, path, CurrentTeacherFrontierSupport.HashFile(path), corpus.RowCount, corpus.CriterionTerminalRowCount);
    }
}
