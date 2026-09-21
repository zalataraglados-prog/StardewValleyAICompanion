namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioTeacherPreferenceBuilder
{
    private static List<string> ValidateRequest(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext
            context,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request)
    {
        var reasons = new List<string>();
        if (request.SchemaVersion !=
                "acquisition_route_portfolio_teacher_preference_request.v1" ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.GoalId) ||
            string.IsNullOrWhiteSpace(request.SnapshotStateHash) ||
            request.ExpectedLedgerRevision < 0 ||
            request.ScopedRequirements is null ||
            request.ScopedRequirements.Length == 0)
        {
            reasons.Add("portfolio_teacher_preference_request_invalid");
            return reasons;
        }
        if (context.Inventory.SchemaVersion !=
                "authoritative_goal_requirement_inventory.v1" ||
            !context.Inventory.DenominatorComplete ||
            !context.Inventory.AcquisitionRoutesComplete ||
            context.Inventory.RequirementSets is null ||
            context.Opportunity.SchemaVersion !=
                "acquisition_route_target_date_opportunity_cost.v1" ||
            !context.Opportunity.RouteOccurrenceInventoryComplete ||
            !context.Opportunity.OpportunityCostAxisResolutionComplete ||
            context.Opportunity.TrainingLabelEligible ||
            context.Opportunity.Routes is null ||
            context.Opportunity.RouteOccurrenceCount !=
                context.Opportunity.Routes.Length)
        {
            reasons.Add("portfolio_teacher_authoritative_denominator_incomplete");
        }
        if (request.GoalId != context.Inventory.GoalId ||
            request.GoalId != context.Opportunity.GoalId)
        {
            reasons.Add("portfolio_teacher_goal_identity_mismatch");
        }
        if (request.SnapshotStateHash != context.Snapshot.StateHash ||
            request.SnapshotStateHash !=
                context.Opportunity.SnapshotStateHash)
        {
            reasons.Add("portfolio_teacher_snapshot_state_hash_mismatch");
        }
        if (request.ExpectedLedgerRevision !=
            context.LedgerState.Ledger.Revision)
        {
            reasons.Add("portfolio_teacher_ledger_revision_mismatch");
        }
        if (request.ScopedRequirements.Any(scope =>
                scope is null ||
                string.IsNullOrWhiteSpace(scope.RequirementSetId) ||
                string.IsNullOrWhiteSpace(scope.RequirementId)) ||
            request.ScopedRequirements.Select(ScopeKey)
                .Distinct(StringComparer.Ordinal).Count() !=
            request.ScopedRequirements.Length)
        {
            reasons.Add("portfolio_teacher_requirement_scope_invalid");
            return reasons;
        }

        var groups = (context.Inventory.RequirementSets ??
                Array.Empty<GoalRequirementSet>())
            .SelectMany(set => set.Groups.Select(group => new
            {
                set.RequirementSetId,
                SetComplete = set.AcquisitionRoutesComplete,
                Group = group
            }))
            .ToDictionary(
                row => ScopeKey(row.RequirementSetId,
                    row.Group.RequirementId),
                row => row,
                StringComparer.Ordinal);
        foreach (var scope in request.ScopedRequirements)
        {
            var key = ScopeKey(scope);
            if (!groups.TryGetValue(key, out var row))
            {
                reasons.Add("portfolio_teacher_scoped_requirement_not_found:" +
                    key);
                continue;
            }
            if (!row.SetComplete ||
                !row.Group.RouteCovered ||
                row.Group.Alternatives is null ||
                row.Group.Alternatives.Length == 0)
            {
                reasons.Add(
                    "portfolio_teacher_scoped_requirement_incomplete:" + key);
            }
        }
        return reasons;
    }

    private static string ScopeKey(
        AcquisitionRoutePortfolioRequirementScope scope) =>
        ScopeKey(scope.RequirementSetId, scope.RequirementId);

    private static string ScopeKey(string setId, string requirementId) =>
        Uri.EscapeDataString(setId) + "/" +
        Uri.EscapeDataString(requirementId);
}
