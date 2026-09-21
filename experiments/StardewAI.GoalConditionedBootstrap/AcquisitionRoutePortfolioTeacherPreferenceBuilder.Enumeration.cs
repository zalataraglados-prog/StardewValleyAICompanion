using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    private const string RouteSourceDecisionPrefix =
        "target-date-acquisition-route:";

    private static long CountCandidates(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request,
        ICollection<string> reasons)
    {
        var total = BigInteger.One;
        foreach (var scoped in ResolveScopes(context, request))
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
        var scopes = request.ScopedRequirements
            .OrderBy(ScopeKey, StringComparer.Ordinal)
            .ToArray();
        var groupChoices = ResolveScopes(context, request)
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
                RouteSourceDecisionPrefix,
                StringComparison.Ordinal))
            .Select(value => value[RouteSourceDecisionPrefix.Length..])
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
                request.RequestId,
                scopes,
                routeIds,
                replacedRouteIds);
            yield return new AcquisitionRoutePortfolioProposal
            {
                ProposalId = proposalId,
                GoalId = request.GoalId,
                SnapshotStateHash = request.SnapshotStateHash,
                ExpectedLedgerRevision = request.ExpectedLedgerRevision,
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

    internal static BigInteger CountGroupCandidates(
        ResolvedScope scope,
        ICollection<string> reasons)
    {
        if (scope.SelectionRule == "all_required")
        {
            if (scope.RequiredAlternativeCount != scope.AlternativeCount)
            {
                reasons.Add("portfolio_teacher_selection_rule_invalid:" +
                    ScopeKey(scope.Scope));
                return BigInteger.Zero;
            }
            var count = BigInteger.One;
            for (var index = 0; index < scope.AlternativeCount; index++)
            {
                count *= scope.RoutesByAlternative
                    .GetValueOrDefault(index, Array.Empty<string>()).Length;
            }
            return count;
        }
        if (scope.SelectionRule != "choose_at_least_required_slots" ||
            scope.RequiredAlternativeCount <= 0 ||
            scope.RequiredAlternativeCount > scope.AlternativeCount)
        {
            reasons.Add("portfolio_teacher_selection_rule_invalid:" +
                ScopeKey(scope.Scope));
            return BigInteger.Zero;
        }
        var available = Enumerable.Range(0, scope.AlternativeCount)
            .Where(index => scope.RoutesByAlternative
                .GetValueOrDefault(index, Array.Empty<string>()).Length > 0)
            .ToArray();
        var subsetCounts = new BigInteger[available.Length + 1];
        subsetCounts[0] = BigInteger.One;
        var visited = 0;
        foreach (var index in available)
        {
            var routeCount = scope.RoutesByAlternative[index].Length;
            visited++;
            for (var selectedCount = visited; selectedCount >= 1;
                 selectedCount--)
            {
                subsetCounts[selectedCount] +=
                    subsetCounts[selectedCount - 1] * routeCount;
            }
        }
        return subsetCounts
            .Skip(scope.RequiredAlternativeCount)
            .Aggregate(BigInteger.Zero, (sum, count) => sum + count);
    }

    internal static string[][] EnumerateGroupRouteSelections(
        ResolvedScope scope)
    {
        var available = Enumerable.Range(0, scope.AlternativeCount)
            .Where(index => scope.RoutesByAlternative
                .GetValueOrDefault(index, Array.Empty<string>()).Length > 0)
            .ToArray();
        IEnumerable<int[]> alternativeSelections =
            scope.SelectionRule == "all_required"
                ? new[] { Enumerable.Range(0, scope.AlternativeCount).ToArray() }
                : Enumerable.Range(
                        scope.RequiredAlternativeCount,
                        available.Length -
                        scope.RequiredAlternativeCount + 1)
                    .SelectMany(selectedCount => Combinations(
                        available,
                        selectedCount));
        return alternativeSelections
            .SelectMany(indexes => CartesianProduct(indexes.Select(index =>
                    scope.RoutesByAlternative.GetValueOrDefault(
                        index,
                        Array.Empty<string>())).ToArray())
                .Select(routes => routes.ToArray()))
            .ToArray();
    }

    private static IEnumerable<T[]> CartesianProduct<T>(T[][] choices)
    {
        IEnumerable<T[]> result = new[] { Array.Empty<T>() };
        foreach (var options in choices)
        {
            result = result.SelectMany(prefix => options.Select(option =>
                prefix.Append(option).ToArray()));
        }
        return result;
    }

    private static IEnumerable<int[]> Combinations(int[] values, int count)
    {
        if (count == 0)
        {
            yield return Array.Empty<int>();
            yield break;
        }
        for (var index = 0; index <= values.Length - count; index++)
        {
            foreach (var suffix in Combinations(
                         values[(index + 1)..],
                         count - 1))
            {
                yield return new[] { values[index] }.Concat(suffix).ToArray();
            }
        }
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
        IReadOnlyDictionary<int, string[]> RoutesByAlternative);
}
