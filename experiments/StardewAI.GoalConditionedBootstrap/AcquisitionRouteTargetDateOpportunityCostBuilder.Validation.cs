namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateOpportunityCostBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateDailyTimeEnergyReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_daily_time_energy_budget.v2" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.DailyTimeEnergyAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.DailyTimeEnergyAxisResolved) &&
                source.DailyTimeEnergyMatchCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyMatchesTargetDate == true) &&
                source.DailyTimeBudgetMissCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyAxisStatus ==
                        "resolved_daily_time_budget_miss") &&
                source.DailyEnergyBudgetMissCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyAxisStatus ==
                        "resolved_daily_energy_budget_miss") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyAxisStatus ==
                        "blocked_upstream_stochastic_retry_axis") &&
                source.BlockedBudgetEvidenceCount == source.Routes.Count(route =>
                    route.DailyTimeEnergyAxisStatus.StartsWith(
                        "blocked_daily_",
                        StringComparison.Ordinal)) &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date daily time/energy budget metadata is incomplete.");

        foreach (var route in source.Routes)
        {
            Require(route.RouteOccurrenceId == route.UpstreamRoute.RouteOccurrenceId,
                "Daily time/energy route identity drifted: " +
                route.RouteOccurrenceId);
            var requirement = RequirementRoute(route);
            Require(route.RouteOccurrenceId == requirement.RouteOccurrenceId &&
                    !string.IsNullOrWhiteSpace(requirement.RequirementSetId) &&
                    !string.IsNullOrWhiteSpace(requirement.RequirementId) &&
                    requirement.AlternativeIndex >= 0,
                "Opportunity-cost comparison identity is incomplete: " +
                route.RouteOccurrenceId);
        }
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateDailyTimeEnergy route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;

    private static AcquisitionRouteTargetDateReservation ReservationRoute(
        AcquisitionRouteTargetDateDailyTimeEnergy route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute;

    private static string ComparisonGroupKey(
        AcquisitionRouteTargetDateDailyTimeEnergy route)
    {
        var requirement = RequirementRoute(route);
        return "requirement_set_id=" +
            Uri.EscapeDataString(requirement.RequirementSetId) +
            ";requirement_id=" +
            Uri.EscapeDataString(requirement.RequirementId) +
            ";alternative_index=" + requirement.AlternativeIndex;
    }
}
