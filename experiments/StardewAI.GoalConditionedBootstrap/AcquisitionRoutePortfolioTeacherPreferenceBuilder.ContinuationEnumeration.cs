namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    private static ResolvedScope[] ResolveScopes(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioContinuationTeacherRequest request)
    {
        var initial = ResolveScopes(
            context,
            new AcquisitionRoutePortfolioTeacherPreferenceRequest
            {
                RequestId = request.RequestId,
                GoalId = request.GoalId,
                SnapshotStateHash = request.SnapshotStateHash,
                ExpectedLedgerRevision = request.ExpectedLedgerRevision,
                ScopedRequirements = request.ScopedProgress.Select(row =>
                        new AcquisitionRoutePortfolioRequirementScope(
                            row.RequirementSetId,
                            row.RequirementId))
                    .ToArray()
            });
        var progress = request.ScopedProgress.ToDictionary(
            ScopeKey,
            StringComparer.Ordinal);
        return initial.Select(scope =>
        {
            var row = progress[ScopeKey(scope.Scope)];
            return scope with
            {
                CompletedAlternativeIndices =
                    row.CompletedAlternativeIndices.ToArray(),
                RemainingRequiredAlternativeCount =
                    row.RemainingRequiredSlots
            };
        }).ToArray();
    }

    private static string ScopeKey(
        AcquisitionRoutePortfolioScopeProgress scope) =>
        ScopeKey(scope.RequirementSetId, scope.RequirementId);
}
