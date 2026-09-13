using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFishingProbabilityBuilder
{
    private const string FishingRouteKind = "native_location_fish_spawn";

    public static AcquisitionRouteTargetDateFishingProbabilityReport Build(
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
        string baseSnapshotPath,
        string routeTimingCalibrationPath,
        string forecastManifestPath)
    {
        var processingFullPath = Path.GetFullPath(targetDateProcessingPath);
        var baseSnapshotFullPath = Path.GetFullPath(baseSnapshotPath);
        var manifestFullPath = Path.GetFullPath(forecastManifestPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateProcessingReport>(
            processingFullPath,
            "Acquisition route target-date processing lead time");
        var recomputed = AcquisitionRouteTargetDateProcessingBuilder.Build(
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
            strategyLedgerPath,
            baseSnapshotFullPath,
            routeTimingCalibrationPath);
        Require(EqualJson(source, recomputed),
            "Target-date processing lead time drifted from deterministic source compilation.");
        Require(source.SchemaVersion ==
                "acquisition_route_target_date_processing_lead_time.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                source.Routes.Length == source.RouteOccurrenceCount,
            "Target-date processing lead-time inventory is incomplete.");
        Require(string.Equals(
                source.SnapshotSha256,
                CurrentTeacherFrontierSupport.HashFile(baseSnapshotFullPath),
                StringComparison.OrdinalIgnoreCase),
            "Processing report and base snapshot digest disagree.");

        var forecasts = LoadForecasts(
            manifestFullPath,
            baseSnapshotFullPath,
            source.GameVersion,
            source.TargetTotalDay,
            source.SnapshotStateHash);
        var routes = source.Routes
            .Select(route => Evaluate(route, forecasts))
            .ToArray();
        var fishingRoutes = routes.Where(route => route.RouteKind == FishingRouteKind)
            .Where(route => !route.Status.StartsWith(
                "not_applicable_upstream_",
                StringComparison.Ordinal))
            .ToArray();
        var blocked = fishingRoutes.Count(route => !route.ProbabilityAxisResolved);
        return new AcquisitionRouteTargetDateFishingProbabilityReport
        {
            Status = blocked == 0
                ? "complete_target_date_fishing_probability_axis_downstream_pending"
                : "partial_target_date_fishing_probability_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateProcessingLeadTimeSha256 =
                CurrentTeacherFrontierSupport.HashFile(processingFullPath),
            BaseSnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(baseSnapshotFullPath),
            BaseSnapshotStateHash = source.SnapshotStateHash,
            ForecastManifestSha256 =
                CurrentTeacherFrontierSupport.HashFile(manifestFullPath),
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            FishingRouteCount = fishingRoutes.Length,
            PositiveProbabilityCount = fishingRoutes.Count(route =>
                route.PositiveProbabilityAvailable == true),
            ZeroProbabilityCount = fishingRoutes.Count(route =>
                route.ProbabilityAxisResolved &&
                route.PositiveProbabilityAvailable == false),
            BlockedProbabilityCount = blocked,
            RouteOccurrenceInventoryComplete = routes.Length ==
                source.RouteOccurrenceCount,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static AcquisitionRouteTargetDateFishingProbability Evaluate(
        AcquisitionRouteTargetDateProcessing route,
        IReadOnlyList<LoadedFishingForecast> forecasts)
    {
        var requirement = RequirementRoute(route);
        if (!route.ProcessingLeadTimeAxisResolved)
        {
            return Result(route, requirement,
                "not_applicable_upstream_processing_axis_unresolved",
                true, null, null, null, Array.Empty<string>(),
                route.BlockingReasons);
        }
        if (route.ProcessingLeadTimeMatchesTargetDate != true)
        {
            return Result(route, requirement,
                "not_applicable_upstream_processing_axis_miss",
                true, null, null, null, Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (!string.Equals(
                requirement.RouteKind,
                FishingRouteKind,
                StringComparison.Ordinal))
        {
            return Result(route, requirement,
                "not_applicable_non_fishing_route",
                true, null, null, null, Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (requirement.MinimumQuality > 0)
        {
            return Result(route, requirement,
                "blocked_fishing_quality_probability",
                false, null, null, null, Array.Empty<string>(),
                new[] { "minimum_fishing_quality_probability_not_proven" });
        }

        var locationRoute = LocationRoute(route);
        var targets = locationRoute.TargetEvaluations
            .Where(target => target.Status == "resolved_location_route_match")
            .Select(target => target.TargetLocationId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (targets.Length == 0)
        {
            return Result(route, requirement,
                "blocked_fishing_target_location_missing",
                false, null, null, null, Array.Empty<string>(),
                new[] { "resolved_fishing_location_target_missing" });
        }

        var matchingForecasts = forecasts
            .Where(forecast => targets.Contains(
                forecast.TargetLocationId,
                StringComparer.Ordinal))
            .ToArray();
        var requestUrls = matchingForecasts.Length > 0
            ? matchingForecasts.Select(ForecastUrl).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray()
            : targets.Select(location =>
                    "/api/v1/snapshot?profile=fishing_forecast&fresh=true&location_id=" +
                    Uri.EscapeDataString(location) +
                    "&rod_slot_index={fishing_rod_slot}")
                .ToArray();
        if (matchingForecasts.Length == 0)
        {
            return Result(route, requirement,
                "blocked_fishing_forecast_missing",
                false, null, null, null, requestUrls,
                targets.Select(location =>
                    "fishing_forecast_snapshot_missing:" + location).ToArray());
        }

        var projections = matchingForecasts
            .SelectMany(forecast => ProjectAllTerminalPairs(
                forecast,
                requirement.QualifiedItemId))
            .ToArray();
        var selected = projections
            .Where(projection => projection.Resolved &&
                                 projection.Probability?.SingleAttemptProbabilityLowerBound
                                     is not null)
            .OrderByDescending(projection =>
                projection.Probability!.SingleAttemptProbabilityLowerBound)
            .ThenBy(projection => projection.TargetLocationId, StringComparer.Ordinal)
            .ThenBy(projection => projection.RodSlotIndex)
            .ThenBy(projection => projection.BobberTileIndex)
            .ThenBy(projection => projection.StandTileY)
            .ThenBy(projection => projection.StandTileX)
            .FirstOrDefault();
        var hashes = matchingForecasts.Select(forecast => forecast.SnapshotSha256)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (selected is null)
        {
            var blockers = projections.SelectMany(projection =>
                    projection.BlockingReasons)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Result(route, requirement,
                "blocked_fishing_terminal_probability",
                false, null, null, null, requestUrls,
                blockers.Length > 0
                    ? blockers
                    : new[] { "no_terminal_fishing_projection_resolved" },
                hashes);
        }

        var probability = selected.Probability!
            .SingleAttemptProbabilityLowerBound!.Value;
        return Result(route, requirement,
            probability > 0d
                ? "resolved_positive_conservative_first_pass_probability"
                : "resolved_zero_conservative_first_pass_probability",
            true,
            probability > 0d,
            probability,
            selected,
            requestUrls,
            Array.Empty<string>(),
            hashes);
    }

    private static IEnumerable<FishingTerminalProbabilityProjectionResult>
        ProjectAllTerminalPairs(
            LoadedFishingForecast forecast,
            string targetQualifiedItemId)
    {
        var tileCount = ReadFishableTileCount(forecast.Snapshot);
        for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            var (x, y) = ReadFishableTile(forecast.Snapshot, tileIndex);
            for (var distance = 2; distance <= 8; distance++)
            {
                yield return Project(x, y + distance);
                yield return Project(x - distance, y);
                yield return Project(x, y - distance);
                yield return Project(x + distance, y);
            }

            FishingTerminalProbabilityProjectionResult Project(
                int standX,
                int standY) =>
                FishingTerminalProbabilitySnapshotProjector.Project(
                    forecast.Snapshot,
                    forecast.TargetLocationId,
                    forecast.RodSlotIndex,
                    targetQualifiedItemId,
                    tileIndex,
                    standX,
                    standY);
        }
    }

    private static AcquisitionRouteTargetDateFishingProbability Result(
        AcquisitionRouteTargetDateProcessing route,
        AcquisitionRouteTargetDateUnlock requirement,
        string status,
        bool resolved,
        bool? positive,
        double? probability,
        FishingTerminalProbabilityProjectionResult? projection,
        string[] requestUrls,
        string[] blockers,
        string[]? hashes = null) => new(
            route.RouteOccurrenceId,
            requirement.RouteKind,
            requirement.QualifiedItemId,
            requirement.RequiredAmount,
            requirement.MinimumQuality,
            status,
            resolved,
            positive,
            probability,
            projection,
            requestUrls,
            hashes ?? Array.Empty<string>(),
            blockers.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray());

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateProcessing route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute;

    private static AcquisitionRouteTargetDateLocation LocationRoute(
        AcquisitionRouteTargetDateProcessing route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute;

    private static string ForecastUrl(LoadedFishingForecast forecast) =>
        "/api/v1/snapshot?profile=fishing_forecast&fresh=true&location_id=" +
        Uri.EscapeDataString(forecast.TargetLocationId) +
        "&rod_slot_index=" + forecast.RodSlotIndex;

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
