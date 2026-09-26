using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    private static long CountCandidates(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request,
        ICollection<string> reasons)
    {
        return CountCandidates(ResolveScopes(context, request), reasons);
    }

    private static long CountCandidates(
        ResolvedScope[] scopes,
        ICollection<string> reasons)
    {
        var total = BigInteger.One;
        foreach (var scoped in scopes)
        {
            var groupCount = CountGroupCandidates(scoped, reasons);
            total *= groupCount;
            if (total > long.MaxValue)
                return long.MaxValue;
        }
        return (long)total;
    }

    private static IEnumerable<AcquisitionRoutePortfolioProposal>
        EnumerateProposals(
            AcquisitionRoutePortfolioBuilder
                .AcquisitionRoutePortfolioBuildContext context,
            AcquisitionRoutePortfolioTeacherPreferenceRequest request)
    {
        var resolved = ResolveScopes(context, request);
        return EnumerateProposals(
            context,
            request.RequestId,
            request.GoalId,
            request.SnapshotStateHash,
            request.ExpectedLedgerRevision,
            resolved,
            string.Empty,
            Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>(),
            string.Empty);
    }

    private static IEnumerable<AcquisitionRoutePortfolioProposal>
        EnumerateProposals(
            AcquisitionRoutePortfolioBuilder
                .AcquisitionRoutePortfolioBuildContext context,
            string requestId,
            string goalId,
            string snapshotStateHash,
            int expectedLedgerRevision,
            ResolvedScope[] resolved,
            string priorCheckpointSha256,
            AcquisitionRoutePortfolioCompletedAlternatives[] completed,
            string priorSupportingTransitionReplanSha256)
    {
        var scopes = resolved.Select(row => row.Scope)
            .OrderBy(ScopeKey, StringComparer.Ordinal)
            .ToArray();
        var groupChoices = resolved
            .Select(EnumerateGroupRouteSelections)
            .ToArray();
        var scopedRouteIds = context.Opportunity.Routes.Where(route =>
            scopes.Any(scope =>
            {
                var requirement = AcquisitionRoutePortfolioBuilder
                    .RequirementRoute(route);
                return requirement.RequirementSetId == scope.RequirementSetId &&
                    requirement.RequirementId == scope.RequirementId;
            })).Select(route => route.RouteOccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        var activeRouteIds = context.LedgerState.Ledger.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active)
            .Select(row => row.SourceDecisionId)
            .Concat(context.LedgerState.Ledger.CurrencyReservations
                .Where(row => row.Status == StrategyCommitmentStatuses.Active)
                .Select(row => row.SourceDecisionId))
            .Where(value => value.StartsWith(
                AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix,
                StringComparison.Ordinal))
            .Select(value => value[
                AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix.Length..])
            .Where(scopedRouteIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        foreach (var selected in CartesianProduct(groupChoices))
        {
            var routeIds = selected.SelectMany(value => value)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var selectedSet = routeIds.ToHashSet(StringComparer.Ordinal);
            var replacedRouteIds = activeRouteIds
                .Where(routeId => !selectedSet.Contains(routeId))
                .ToArray();
            var proposalId = ProposalId(
                requestId,
                scopes,
                routeIds,
                replacedRouteIds);
            yield return new AcquisitionRoutePortfolioProposal
            {
                ProposalId = proposalId,
                GoalId = goalId,
                SnapshotStateHash = snapshotStateHash,
                ExpectedLedgerRevision = expectedLedgerRevision,
                PriorRolloutCheckpointSha256 = priorCheckpointSha256,
                PriorSupportingTransitionReplanSha256 =
                    priorSupportingTransitionReplanSha256,
                CompletedAlternatives = completed
                    .Select(AcquisitionRoutePortfolioBuilder
                        .CloneCompletedAlternatives)
                    .ToArray(),
                ScopedRequirements = scopes.Select(scope =>
                        new AcquisitionRoutePortfolioRequirementScope(
                            scope.RequirementSetId,
                            scope.RequirementId))
                    .ToArray(),
                SelectedRouteOccurrenceIds = routeIds,
                ReplacedRouteOccurrenceIds = replacedRouteIds
            };
        }
    }

    private static ResolvedScope[] ResolveScopes(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request)
    {
        var groups = context.Inventory.RequirementSets
            .SelectMany(set => set.Groups.Select(group => new
            {
                set.RequirementSetId,
                Group = group
            }))
            .ToDictionary(
                row => ScopeKey(row.RequirementSetId,
                    row.Group.RequirementId),
                row => row,
                StringComparer.Ordinal);
        return request.ScopedRequirements
            .OrderBy(ScopeKey, StringComparer.Ordinal)
            .Select(scope =>
            {
                var row = groups[ScopeKey(scope)];
                var routes = context.Opportunity.Routes
                    .Where(route =>
                    {
                        var requirement = AcquisitionRoutePortfolioBuilder
                            .RequirementRoute(route);
                        return requirement.RequirementSetId ==
                                scope.RequirementSetId &&
                            requirement.RequirementId == scope.RequirementId &&
                            route.OpportunityCostAxisResolved &&
                            route.OpportunityCostMatchesTargetDate == true &&
                            route.OpportunityCostAxisStatus ==
                                "resolved_opportunity_cost_pareto_frontier" &&
                            route.CostVector is not null &&
                            route.DominatedByRouteOccurrenceIds.Length == 0 &&
                            route.NonMatchingReasons.Length == 0 &&
                            route.BlockingReasons.Length == 0;
                    })
                    .GroupBy(route => AcquisitionRoutePortfolioBuilder
                        .RequirementRoute(route).AlternativeIndex)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderBy(route => route.RouteOccurrenceId,
                                StringComparer.Ordinal)
                            .Select(route => route.RouteOccurrenceId)
                            .ToArray());
                return new ResolvedScope(
                    scope,
                    row.Group.SelectionRule,
                    row.Group.RequiredAlternativeCount,
                    row.Group.Alternatives.Length,
                    routes);
            }).ToArray();
    }

    private static string ProposalId(
        string requestId,
        AcquisitionRoutePortfolioRequirementScope[] scopes,
        string[] routeIds,
        string[] replacedRouteIds)
    {
        var identity = requestId + "|" +
            string.Join(";", scopes.Select(ScopeKey)) + "|" +
            string.Join(";", routeIds) + "|replace=" +
            string.Join(";", replacedRouteIds);
        var digest = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return requestId + ":portfolio:" + digest;
    }

    internal sealed record ResolvedScope(
        AcquisitionRoutePortfolioRequirementScope Scope,
        string SelectionRule,
        int RequiredAlternativeCount,
        int AlternativeCount,
        IReadOnlyDictionary<int, string[]> RoutesByAlternative,
        int[]? CompletedAlternativeIndices = null,
        int? RemainingRequiredAlternativeCount = null);
}
