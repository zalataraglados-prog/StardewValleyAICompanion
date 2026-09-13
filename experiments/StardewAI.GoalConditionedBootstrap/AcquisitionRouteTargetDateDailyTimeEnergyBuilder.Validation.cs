namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateStochasticRetryReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_stochastic_retry_budget.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date stochastic-retry metadata is incomplete.");
        Require(source.StochasticRetryAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.StochasticRetryAxisResolved) &&
                source.StochasticRetryMatchCount == source.Routes.Count(route =>
                    route.StochasticRetryBudgetMatchesTargetDate == true) &&
                source.StochasticRetryNotRequiredCount == source.Routes.Count(
                    route => route.RetryBudgetKind ==
                        "stochastic_retry_not_required") &&
                source.MaterializedOutputCount == source.Routes.Count(route =>
                    route.RetryBudgetKind ==
                        "current_output_already_materialized") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.StochasticRetryAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.StochasticRetryAxisStatus ==
                        "blocked_upstream_processing_lead_time_axis") &&
                source.BlockedProbabilityEvidenceCount == source.Routes.Count(
                    route => route.StochasticRetryAxisStatus ==
                        "blocked_stochastic_probability_evidence") &&
                source.BlockedRetryReservationCount == source.Routes.Count(
                    route => route.StochasticRetryAxisStatus ==
                        "blocked_retry_expanded_reservation_revalidation"),
            "Target-date stochastic-retry counts drifted.");
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteCalendarResolution> ValidateStaticSource(
        AcquisitionRouteCalendarResolutionReport staticSource,
        AcquisitionRouteTargetDateStochasticRetryReport source)
    {
        Require(staticSource.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                staticSource.RouteOccurrenceInventoryComplete &&
                !staticSource.TrainingLabelEligible &&
                staticSource.GoalId == source.GoalId &&
                staticSource.GameVersion == source.GameVersion &&
                staticSource.RouteOccurrenceCount == staticSource.Routes.Length,
            "Static calendar metadata is incomplete for daily budgeting.");
        var result = staticSource.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        Require(result.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
                source.Routes.Select(route => route.RouteOccurrenceId)),
            "Static calendar and stochastic-retry inventories disagree.");
        foreach (var route in source.Routes)
        {
            var requirement = RequirementRoute(route);
            var staticRoute = result[route.RouteOccurrenceId];
            Require(staticRoute.RouteKind == requirement.RouteKind &&
                    staticRoute.QualifiedItemId ==
                        requirement.QualifiedItemId &&
                    staticRoute.RequiredAmount == requirement.RequiredAmount &&
                    staticRoute.MinimumQuality == requirement.MinimumQuality,
                "Static daily-budget contract disagrees with route: " +
                route.RouteOccurrenceId);
        }
        return result;
    }

    private static IReadOnlyDictionary<string,
        AcquisitionRouteTargetDateFishingProbability> ValidateFishingSource(
        AcquisitionRouteTargetDateFishingProbabilityReport fishingSource,
        AcquisitionRouteTargetDateStochasticRetryReport source,
        string fishingPath)
    {
        Require(fishingSource.SchemaVersion ==
                    "acquisition_route_target_date_fishing_probability.v1" &&
                fishingSource.RouteOccurrenceInventoryComplete &&
                !fishingSource.TrainingLabelEligible &&
                fishingSource.GoalId == source.GoalId &&
                fishingSource.GameVersion == source.GameVersion &&
                fishingSource.TargetTotalDay == source.TargetTotalDay &&
                fishingSource.BaseSnapshotSha256 == source.SnapshotSha256 &&
                fishingSource.BaseSnapshotStateHash ==
                    source.SnapshotStateHash &&
                string.Equals(
                    source.TargetDateFishingProbabilitySha256,
                    CurrentTeacherFrontierSupport.HashFile(fishingPath),
                    StringComparison.OrdinalIgnoreCase) &&
                fishingSource.RouteOccurrenceCount == source.RouteOccurrenceCount &&
                fishingSource.Routes.Length == source.RouteOccurrenceCount,
            "Fishing probability metadata is incomplete for daily budgeting.");
        var result = fishingSource.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        Require(result.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
                source.Routes.Select(route => route.RouteOccurrenceId)),
            "Fishing probability and stochastic-retry inventories disagree.");
        return result;
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateStochasticRetry route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute;

    private static AcquisitionRouteTargetDateLocation LocationRoute(
        AcquisitionRouteTargetDateStochasticRetry route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static AcquisitionRouteTargetDateCurrency CurrencyRoute(
        AcquisitionRouteTargetDateStochasticRetry route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
}
