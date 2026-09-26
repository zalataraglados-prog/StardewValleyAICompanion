using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateOpportunityCostBuilder
{
    public static AcquisitionRouteTargetDateOpportunityCostReport Build(
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
        string targetDateDailyTimeEnergyPath,
        string fishingForecastManifestPath,
        string strategyLedgerPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var sourcePath = Path.GetFullPath(targetDateDailyTimeEnergyPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateDailyTimeEnergyReport>(
            sourcePath,
            "Acquisition route target-date daily time/energy budget");
        var recomputed = AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticCalendarResolutionPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            targetDateFestivalPath,
            targetDateLocationPath,
            targetDateFacilityPath,
            targetDateResourcePath,
            targetDateCurrencyPath,
            targetDateReservationPath,
            targetDateProcessingPath,
            targetDateFishingProbabilityPath,
            targetDateStochasticRetryPath,
            fishingForecastManifestPath,
            strategyLedgerPath,
            snapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date daily time/energy budget drifted from deterministic source compilation.");
        ValidateSource(source);

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
            "Target-date daily time/energy budget and snapshot identity disagree.");

        var state = AcquisitionOpportunityCostSnapshotState.Read(snapshot);
        var preliminary = source.Routes
            .Select(route => Evaluate(route, source.SnapshotStateHash, state))
            .ToArray();
        var routes = ApplyParetoDominance(preliminary);
        var blockedUpstream = routes.Count(route =>
            route.OpportunityCostAxisStatus ==
                "blocked_upstream_daily_time_energy_axis");
        var blockedCost = routes.Count(route =>
            route.OpportunityCostAxisStatus ==
                "blocked_opportunity_cost_evidence");
        return new AcquisitionRouteTargetDateOpportunityCostReport
        {
            Status = blockedUpstream == 0 && blockedCost == 0
                ? "complete_target_date_opportunity_cost_axis_downstream_pending"
                : "partial_target_date_opportunity_cost_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateDailyTimeEnergySha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            SnapshotSha256 = source.SnapshotSha256,
            SnapshotStateHash = source.SnapshotStateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            OpportunityCostAxisResolvedCount = routes.Count(route =>
                route.OpportunityCostAxisResolved),
            ParetoFrontierCount = routes.Count(route =>
                route.OpportunityCostAxisStatus ==
                    "resolved_opportunity_cost_pareto_frontier"),
            ParetoDominatedCount = routes.Count(route =>
                route.OpportunityCostAxisStatus ==
                    "resolved_opportunity_cost_pareto_dominated"),
            NotApplicableUpstreamCount = routes.Count(route =>
                route.OpportunityCostAxisStatus.StartsWith(
                    "not_applicable_upstream_",
                    StringComparison.Ordinal)),
            BlockedUpstreamCount = blockedUpstream,
            BlockedCostEvidenceCount = blockedCost,
            RouteOccurrenceInventoryComplete = true,
            OpportunityCostAxisResolutionComplete =
                blockedUpstream == 0 && blockedCost == 0,
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

}
