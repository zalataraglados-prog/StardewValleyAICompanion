namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    internal static PortfolioParetoResult EvaluatePareto(
        PortfolioCostCandidate[] candidates)
    {
        if (candidates.Any(candidate =>
                string.IsNullOrWhiteSpace(candidate.ProposalId)) ||
            candidates.Select(candidate => candidate.ProposalId)
                .Distinct(StringComparer.Ordinal).Count() != candidates.Length)
        {
            throw new InvalidDataException(
                "Portfolio Pareto candidates have invalid identities.");
        }
        var dominatedBy = candidates.ToDictionary(
            candidate => candidate.ProposalId,
            candidate => candidates.Where(other =>
                    other.ProposalId != candidate.ProposalId &&
                    AcquisitionRouteTargetDateOpportunityCostBuilder
                        .OpportunityCostDominates(
                            other.CostVector,
                            candidate.CostVector))
                .Select(other => other.ProposalId)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            StringComparer.Ordinal);
        return new PortfolioParetoResult(
            dominatedBy,
            dominatedBy.Where(row => row.Value.Length == 0)
                .Select(row => row.Key)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    internal sealed record PortfolioCostCandidate(
        string ProposalId,
        AcquisitionOpportunityCostVector CostVector);

    internal sealed record PortfolioParetoResult(
        IReadOnlyDictionary<string, string[]> DominatedByProposalIds,
        string[] FrontierProposalIds);
}
