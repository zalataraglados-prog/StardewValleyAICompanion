using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    public static AcquisitionRouteTargetDateStochasticRetryReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string targetDateFestivalPath,
        string targetDateLocationPath,
        string targetDateFacilityPath,
        string targetDateResourcePath,
        string targetDateCurrencyPath,
        string targetDateReservationPath,
        string targetDateProcessingPath,
        string strategyLedgerPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateProcessingPath);
        var staticPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateProcessingReport>(
            sourcePath,
            "Acquisition route target-date processing lead time");
        var recomputed = AcquisitionRouteTargetDateProcessingBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            targetDateFacilityPath,
            targetDateResourcePath,
            targetDateCurrencyPath,
            targetDateReservationPath,
            strategyLedgerPath,
            snapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date processing lead time drifted from deterministic source compilation.");
        ValidateSource(source);

        var staticSource = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            staticPath,
            "Acquisition route static calendar resolution");
        ValidateStaticSource(staticSource, source);
        var staticRoutes = staticSource.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        var routes = source.Routes.Select(route => Evaluate(
                route,
                staticRoutes[route.RouteOccurrenceId]))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.StochasticRetryAxisStatus ==
                "blocked_upstream_processing_lead_time_axis");
        var blockedProbability = routes.Count(route =>
            route.StochasticRetryAxisStatus ==
                "blocked_stochastic_probability_evidence");
        var blockedReservation = routes.Count(route =>
            route.StochasticRetryAxisStatus ==
                "blocked_retry_expanded_reservation_revalidation");
        return new AcquisitionRouteTargetDateStochasticRetryReport
        {
            Status = blockedUpstream == 0 && blockedProbability == 0 &&
                    blockedReservation == 0
                ? "complete_target_date_stochastic_retry_budget_axis_downstream_pending"
                : "partial_target_date_stochastic_retry_budget_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateProcessingLeadTimeSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticPath),
            SnapshotSha256 = source.SnapshotSha256,
            SnapshotStateHash = source.SnapshotStateHash,
            TargetTotalDay = source.TargetTotalDay,
            TargetSuccessProbability =
                StochasticRetryPolicy.TargetSuccessProbability,
            RouteOccurrenceCount = routes.Length,
            StochasticRetryAxisResolvedCount = routes.Count(route =>
                route.StochasticRetryAxisResolved),
            StochasticRetryMatchCount = routes.Count(route =>
                route.StochasticRetryBudgetMatchesTargetDate == true),
            StochasticRetryNotRequiredCount = routes.Count(route =>
                route.RetryBudgetKind == "stochastic_retry_not_required"),
            MaterializedOutputCount = routes.Count(route =>
                route.RetryBudgetKind == "current_output_already_materialized"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.StochasticRetryAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedProbabilityEvidenceCount = blockedProbability,
            BlockedRetryReservationCount = blockedReservation,
            RouteOccurrenceInventoryComplete = true,
            StochasticRetryAxisResolutionComplete = blockedUpstream == 0 &&
                blockedProbability == 0 && blockedReservation == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static bool EqualJson<T>(T left, T right)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Serialize(left, options) ==
            JsonSerializer.Serialize(right, options);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
