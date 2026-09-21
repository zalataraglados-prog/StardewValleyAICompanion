using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioBuilder
{
    private static List<string> ValidateMetadata(
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        AcquisitionRoutePortfolioProposal proposal,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger)
    {
        var reasons = new List<string>();
        if (inventory.SchemaVersion !=
                "authoritative_goal_requirement_inventory.v1" ||
            !inventory.DenominatorComplete ||
            !inventory.AcquisitionRoutesComplete ||
            inventory.RequirementSets is null)
        {
            reasons.Add("authoritative_requirement_inventory_incomplete");
        }
        if (opportunity.SchemaVersion !=
                "acquisition_route_target_date_opportunity_cost.v1" ||
            !opportunity.RouteOccurrenceInventoryComplete ||
            !opportunity.OpportunityCostAxisResolutionComplete ||
            opportunity.TrainingLabelEligible ||
            opportunity.Routes is null ||
            opportunity.RouteOccurrenceCount != opportunity.Routes.Length)
        {
            reasons.Add("complete_opportunity_cost_frontier_unavailable");
        }
        if (proposal.SchemaVersion !=
                "acquisition_route_portfolio_proposal.v1" ||
            string.IsNullOrWhiteSpace(proposal.ProposalId) ||
            string.IsNullOrWhiteSpace(proposal.GoalId) ||
            proposal.ScopedRequirements is null ||
            proposal.SelectedRouteOccurrenceIds is null ||
            proposal.ReplacedRouteOccurrenceIds is null)
        {
            reasons.Add("route_portfolio_proposal_contract_invalid");
            return reasons;
        }
        if (proposal.GoalId != inventory.GoalId ||
            proposal.GoalId != opportunity.GoalId)
        {
            reasons.Add("route_portfolio_goal_identity_mismatch");
        }
        if (proposal.SnapshotStateHash != snapshot.StateHash ||
            proposal.SnapshotStateHash != opportunity.SnapshotStateHash)
        {
            reasons.Add("route_portfolio_snapshot_state_hash_mismatch");
        }
        if (proposal.ExpectedLedgerRevision != ledger.Revision)
            reasons.Add("route_portfolio_ledger_revision_mismatch");
        if (proposal.ScopedRequirements.Length == 0)
            reasons.Add("route_portfolio_requirement_scope_empty");
        if (proposal.SelectedRouteOccurrenceIds.Length == 0)
            reasons.Add("route_portfolio_selected_routes_empty");
        if (proposal.ScopedRequirements.Any(scope =>
                string.IsNullOrWhiteSpace(scope.RequirementSetId) ||
                string.IsNullOrWhiteSpace(scope.RequirementId)) ||
            proposal.ScopedRequirements.Select(ScopeKey)
                .Distinct(StringComparer.Ordinal).Count() !=
            proposal.ScopedRequirements.Length)
        {
            reasons.Add("route_portfolio_requirement_scope_invalid");
        }
        ValidateDistinctIds(
            proposal.SelectedRouteOccurrenceIds,
            "selected_route_occurrence_ids_invalid",
            reasons);
        ValidateDistinctIds(
            proposal.ReplacedRouteOccurrenceIds,
            "replaced_route_occurrence_ids_invalid",
            reasons);
        if (proposal.SelectedRouteOccurrenceIds.Intersect(
                proposal.ReplacedRouteOccurrenceIds,
                StringComparer.Ordinal).Any())
        {
            reasons.Add("selected_and_replaced_routes_overlap");
        }
        return reasons;
    }

    private static AcquisitionRouteTargetDateOpportunityCost[] SelectRoutes(
        AcquisitionRouteTargetDateOpportunityCostReport opportunity,
        AcquisitionRoutePortfolioProposal proposal,
        ICollection<string> reasons)
    {
        if (proposal.SelectedRouteOccurrenceIds is null ||
            opportunity.Routes is null)
        {
            return Array.Empty<AcquisitionRouteTargetDateOpportunityCost>();
        }
        var routes = opportunity.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        var result = new List<AcquisitionRouteTargetDateOpportunityCost>();
        foreach (var id in proposal.SelectedRouteOccurrenceIds
                     .Distinct(StringComparer.Ordinal))
        {
            if (!routes.TryGetValue(id, out var route))
                reasons.Add("selected_route_occurrence_not_found:" + id);
            else
                result.Add(route);
        }
        return result.ToArray();
    }

    private static bool ValidateSelectionRules(
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRoutePortfolioProposal proposal,
        AcquisitionRouteTargetDateOpportunityCost[] selected,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        if (proposal.ScopedRequirements is null)
            return false;
        var groupLookup = inventory.RequirementSets
            .SelectMany(set => set.Groups.Select(group => new
            {
                set.RequirementSetId,
                Group = group
            }))
            .ToDictionary(
                row => ScopeKey(row.RequirementSetId, row.Group.RequirementId),
                row => row.Group,
                StringComparer.Ordinal);
        var scopes = proposal.ScopedRequirements
            .Select(ScopeKey)
            .ToHashSet(StringComparer.Ordinal);
        var selectedByScope = selected.GroupBy(route =>
                ScopeKey(RequirementRoute(route).RequirementSetId,
                    RequirementRoute(route).RequirementId),
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(),
                StringComparer.Ordinal);
        foreach (var selectedScope in selectedByScope.Keys)
        {
            if (!scopes.Contains(selectedScope))
                reasons.Add("selected_route_outside_requirement_scope:" +
                    selectedScope);
        }
        foreach (var scope in scopes)
        {
            if (!groupLookup.TryGetValue(scope, out var group))
            {
                reasons.Add("scoped_requirement_not_found:" + scope);
                continue;
            }
            var routes = selectedByScope.GetValueOrDefault(
                scope,
                Array.Empty<AcquisitionRouteTargetDateOpportunityCost>());
            var alternativeIndexes = routes.Select(route =>
                    RequirementRoute(route).AlternativeIndex)
                .ToArray();
            if (alternativeIndexes.Distinct().Count() !=
                alternativeIndexes.Length)
            {
                reasons.Add("multiple_routes_selected_for_one_alternative:" +
                    scope);
                continue;
            }
            if (alternativeIndexes.Any(index =>
                    index < 0 || index >= group.Alternatives.Length))
            {
                reasons.Add("selected_alternative_index_out_of_range:" + scope);
                continue;
            }
            var satisfied = group.SelectionRule switch
            {
                "all_required" =>
                    group.RequiredAlternativeCount ==
                        group.Alternatives.Length &&
                    alternativeIndexes.Length == group.Alternatives.Length,
                "choose_at_least_required_slots" =>
                    group.RequiredAlternativeCount > 0 &&
                    group.RequiredAlternativeCount <=
                        group.Alternatives.Length &&
                    alternativeIndexes.Length >=
                        group.RequiredAlternativeCount,
                _ => false
            };
            if (!satisfied)
                reasons.Add("requirement_selection_rule_unsatisfied:" + scope);
        }
        return reasons.Count == initialCount;
    }

    private static bool ValidateParetoFrontier(
        AcquisitionRouteTargetDateOpportunityCost[] selected,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        foreach (var route in selected)
        {
            if (!route.OpportunityCostAxisResolved ||
                route.OpportunityCostMatchesTargetDate != true ||
                route.OpportunityCostAxisStatus !=
                    "resolved_opportunity_cost_pareto_frontier" ||
                route.CostVector is null ||
                route.DominatedByRouteOccurrenceIds.Length != 0)
            {
                reasons.Add("selected_route_not_on_complete_pareto_frontier:" +
                    route.RouteOccurrenceId);
            }
        }
        return reasons.Count == initialCount;
    }

    private static void ValidateDistinctIds(
        string[] values,
        string reason,
        ICollection<string> reasons)
    {
        if (values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            reasons.Add(reason);
        }
    }

    private static string ScopeKey(
        AcquisitionRoutePortfolioRequirementScope scope) =>
        ScopeKey(scope.RequirementSetId, scope.RequirementId);

    private static string ScopeKey(string setId, string requirementId) =>
        Uri.EscapeDataString(setId) + "/" +
        Uri.EscapeDataString(requirementId);

    internal static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateOpportunityCost route)
    {
        var daily = route.UpstreamRoute;
        return daily.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;
    }

    private static AcquisitionRouteTargetDateReservation ReservationRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute;
}
