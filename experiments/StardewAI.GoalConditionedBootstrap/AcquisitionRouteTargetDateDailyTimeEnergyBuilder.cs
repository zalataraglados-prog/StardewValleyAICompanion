using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    public static AcquisitionRouteTargetDateDailyTimeEnergyReport Build(
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
        string targetDateFishingProbabilityPath,
        string targetDateStochasticRetryPath,
        string fishingForecastManifestPath,
        string strategyLedgerPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateStochasticRetryPath);
        var fishingPath = Path.GetFullPath(targetDateFishingProbabilityPath);
        var staticPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var timingPath = Path.GetFullPath(routeTimingCalibrationPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateStochasticRetryReport>(
            sourcePath,
            "Acquisition route target-date stochastic retry budget");
        var recomputed = AcquisitionRouteTargetDateStochasticRetryBuilder.Build(
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
            targetDateProcessingPath,
            fishingPath,
            fishingForecastManifestPath,
            strategyLedgerPath,
            snapshotFullPath,
            timingPath);
        Require(EqualJson(source, recomputed),
            "Target-date stochastic retry budget drifted from deterministic source compilation.");
        ValidateSource(source);

        var staticSource = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            staticPath,
            "Acquisition route static calendar resolution");
        var staticRoutes = ValidateStaticSource(staticSource, source);
        var fishingSource = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateFishingProbabilityReport>(
            fishingPath,
            "Acquisition route target-date fishing probability");
        var fishingRoutes = ValidateFishingSource(
            fishingSource,
            source,
            fishingPath);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        Require(string.Equals(
                    source.SnapshotSha256,
                    CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    source.SnapshotStateHash,
                    stateHash,
                    StringComparison.Ordinal),
            "Target-date stochastic retry and snapshot identity disagree.");
        var state = AcquisitionDailyTimeEnergySnapshotState.Read(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay,
            timingPath);
        var routes = source.Routes.Select(route => Evaluate(
                route,
                staticRoutes[route.RouteOccurrenceId],
                fishingRoutes[route.RouteOccurrenceId],
                state))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.DailyTimeEnergyAxisStatus ==
                "blocked_upstream_stochastic_retry_axis");
        var blockedBudget = routes.Count(route =>
            route.DailyTimeEnergyAxisStatus.StartsWith(
                "blocked_daily_",
                StringComparison.Ordinal));
        return new AcquisitionRouteTargetDateDailyTimeEnergyReport
        {
            Status = blockedUpstream == 0 && blockedBudget == 0
                ? "complete_target_date_daily_time_energy_budget_axis_downstream_pending"
                : "partial_target_date_daily_time_energy_budget_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateStochasticRetrySha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            TargetDateFishingProbabilitySha256 =
                CurrentTeacherFrontierSupport.HashFile(fishingPath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticPath),
            SnapshotSha256 = source.SnapshotSha256,
            SnapshotStateHash = source.SnapshotStateHash,
            RouteTimingCalibrationSha256 =
                CurrentTeacherFrontierSupport.HashFile(timingPath),
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            DailyTimeEnergyAxisResolvedCount = routes.Count(route =>
                route.DailyTimeEnergyAxisResolved),
            DailyTimeEnergyMatchCount = routes.Count(route =>
                route.DailyTimeEnergyMatchesTargetDate == true),
            DailyTimeBudgetMissCount = routes.Count(route =>
                route.DailyTimeEnergyAxisStatus ==
                    "resolved_daily_time_budget_miss"),
            DailyEnergyBudgetMissCount = routes.Count(route =>
                route.DailyTimeEnergyAxisStatus ==
                    "resolved_daily_energy_budget_miss"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.DailyTimeEnergyAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedBudgetEvidenceCount = blockedBudget,
            RouteOccurrenceInventoryComplete = true,
            DailyTimeEnergyAxisResolutionComplete =
                blockedUpstream == 0 && blockedBudget == 0,
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
