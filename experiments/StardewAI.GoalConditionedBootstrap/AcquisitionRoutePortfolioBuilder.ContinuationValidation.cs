namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioBuilder
{
    private static void ValidateContinuationEvidence(
        AcquisitionRoutePortfolioProposal proposal,
        AcquisitionRoutePortfolioContinuationEvidence? continuation,
        ICollection<string> reasons)
    {
        var proposalCompleted = proposal.CompletedAlternatives ??
            Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>();
        if (continuation is null)
        {
            if (!string.IsNullOrEmpty(
                    proposal.PriorRolloutCheckpointSha256) ||
                proposalCompleted.Length != 0)
            {
                reasons.Add("unverified_portfolio_continuation_evidence");
            }
            return;
        }
        if (proposalCompleted.Any(row => row is null ||
                row.AlternativeIndices is null))
        {
            reasons.Add("portfolio_completed_alternatives_invalid");
            return;
        }
        var expected = continuation.CompletedAlternatives
            .OrderBy(row => ScopeKey(
                row.RequirementSetId,
                row.RequirementId), StringComparer.Ordinal)
            .Select(row => new
            {
                row.RequirementSetId,
                row.RequirementId,
                AlternativeIndices = row.AlternativeIndices
                    .Order()
                    .ToArray()
            }).ToArray();
        var actual = proposalCompleted
            .OrderBy(row => ScopeKey(
                row.RequirementSetId,
                row.RequirementId), StringComparer.Ordinal)
            .Select(row => new
            {
                row.RequirementSetId,
                row.RequirementId,
                AlternativeIndices = row.AlternativeIndices
                    .Order()
                    .ToArray()
            }).ToArray();
        if (proposal.PriorRolloutCheckpointSha256 !=
                continuation.PriorCheckpointSha256 ||
            System.Text.Json.JsonSerializer.Serialize(actual) !=
                System.Text.Json.JsonSerializer.Serialize(expected))
        {
            reasons.Add("portfolio_continuation_evidence_mismatch");
        }
        if (actual.Any(row =>
                string.IsNullOrWhiteSpace(row.RequirementSetId) ||
                string.IsNullOrWhiteSpace(row.RequirementId) ||
                row.AlternativeIndices.Length == 0 ||
                row.AlternativeIndices.Any(index => index < 0) ||
                row.AlternativeIndices.Distinct().Count() !=
                    row.AlternativeIndices.Length))
        {
            reasons.Add("portfolio_completed_alternatives_invalid");
        }
        if (actual.Select(row => ScopeKey(
                    row.RequirementSetId,
                    row.RequirementId))
                .Distinct(StringComparer.Ordinal).Count() != actual.Length)
        {
            reasons.Add("portfolio_completed_alternative_scope_duplicate");
        }
        var scopes = proposal.ScopedRequirements.Select(ScopeKey)
            .ToHashSet(StringComparer.Ordinal);
        if (actual.Any(row => !scopes.Contains(ScopeKey(
                row.RequirementSetId,
                row.RequirementId))))
        {
            reasons.Add("portfolio_completed_alternative_outside_scope");
        }
    }
}
