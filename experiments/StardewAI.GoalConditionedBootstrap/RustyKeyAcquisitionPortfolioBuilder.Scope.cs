namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyAcquisitionPortfolioBuilder
{
    private static HashSet<string> ValidateSelectedScope(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext context,
        AcquisitionRoutePortfolioProposal proposal, GoalRequirementSet museum,
        CurrentCollectionProgress progress, ICollection<string> reasons)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var groups = museum.Groups.ToDictionary(group => group.RequirementId, StringComparer.Ordinal);
        if (proposal.ScopedRequirements is null || proposal.SelectedRouteOccurrenceIds is null ||
            proposal.ReplacedRouteOccurrenceIds is null)
        {
            reasons.Add("rusty_key_proposal_scope_missing");
            return selected;
        }
        foreach (var scope in proposal.ScopedRequirements)
        {
            if (scope is null || scope.RequirementSetId != MuseumSetId ||
                !groups.ContainsKey(scope.RequirementId) || !selected.Add(scope.RequirementId))
                reasons.Add("rusty_key_scope_not_unique_museum_requirement");
            else if (progress.Available && progress.Requirements[scope.RequirementId].Completed)
                reasons.Add("rusty_key_selected_item_already_donated:" + scope.RequirementId);
        }
        var byId = context.Opportunity.Routes.ToDictionary(route => route.RouteOccurrenceId, StringComparer.Ordinal);
        foreach (var routeId in proposal.ReplacedRouteOccurrenceIds)
        {
            if (!byId.TryGetValue(routeId, out var route))
            {
                reasons.Add("rusty_key_replaced_route_unavailable:" + routeId);
                continue;
            }
            var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(route);
            if (requirement.RequirementSetId != MuseumSetId || !selected.Contains(requirement.RequirementId))
                reasons.Add("rusty_key_cannot_release_other_requirement_reservations:" + routeId);
        }
        return selected;
    }

    private static void ValidateMuseumRequirementShape(GoalRequirementSet museum)
    {
        CurrentCollectionTeacherFrontierBuilder.ValidateMuseumRequirementShape(museum);
        if (museum.DenominatorStatus != "complete" || !museum.AcquisitionRoutesComplete ||
            museum.RequiredGroupCount != museum.Groups.Length || museum.Groups.Length < 60 ||
            museum.Groups.Select(group => group.RequirementId).Distinct(StringComparer.Ordinal).Count() != museum.Groups.Length ||
            museum.Groups.Any(group => string.IsNullOrWhiteSpace(group.Alternatives[0].ItemId)) ||
            museum.Groups.Select(group => group.Alternatives[0].QualifiedItemId)
                .Distinct(StringComparer.Ordinal).Count() != museum.Groups.Length)
            throw new InvalidDataException("Rusty Key requires the complete exact-item museum denominator.");
    }
}
