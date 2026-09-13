using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateLocationBuilder
{
    public static AcquisitionRouteTargetDateLocationReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string targetDateFestivalPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var festivalFullPath = Path.GetFullPath(targetDateFestivalPath);
        var staticFullPath = Path.GetFullPath(staticCalendarResolutionPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var timingFullPath = Path.GetFullPath(routeTimingCalibrationPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateFestivalReport>(
            festivalFullPath,
            "Acquisition route target-date festival state");
        var recomputed = AcquisitionRouteTargetDateFestivalBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticFullPath,
            targetDateCalendarPath,
            targetDateUnlockPath,
            snapshotFullPath);
        Require(EqualJson(source, recomputed),
            "Target-date festival state drifted from deterministic source compilation.");
        ValidateSource(source);

        var staticReport = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            staticFullPath,
            "Acquisition route static calendar resolution");
        var staticByOccurrence = ValidateStaticSource(source, staticReport);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        Require(string.Equals(
                source.SnapshotStateHash,
                stateHash,
                StringComparison.Ordinal),
            "Target-date festival state and snapshot state hash disagree.");
        var state = AcquisitionLocationRouteSnapshotState.Read(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay,
            timingFullPath);

        var activeRoutes = source.Routes
            .Where(IsLocationApplicable)
            .ToArray();
        var targetByOccurrence = state.RouteEvidenceAvailable
            ? activeRoutes.ToDictionary(
                route => route.RouteOccurrenceId,
                route => AcquisitionLocationRouteTargetResolver.Resolve(
                    route,
                    staticByOccurrence[route.RouteOccurrenceId],
                    state),
                StringComparer.Ordinal)
            : new Dictionary<string, AcquisitionLocationTargetResolution>(
                StringComparer.Ordinal);
        var routeByLocation = ProduceLocationRoutes(
            targetByOccurrence.Values,
            state,
            source.TargetTotalDay);
        var routes = source.Routes
            .Select(route => Evaluate(
                route,
                state,
                targetByOccurrence,
                routeByLocation))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.LocationRouteAxisStatus ==
                "blocked_upstream_calendar_condition_axis");
        var blockedLocation = routes.Count(route =>
            route.LocationRouteAxisStatus == "blocked_location_route_evidence");
        return new AcquisitionRouteTargetDateLocationReport
        {
            Status = blockedUpstream == 0 && blockedLocation == 0
                ? "complete_target_date_location_route_axis_downstream_pending"
                : "partial_target_date_location_route_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateFestivalSha256 =
                CurrentTeacherFrontierSupport.HashFile(festivalFullPath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(staticFullPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            RouteTimingCalibrationSha256 =
                CurrentTeacherFrontierSupport.HashFile(timingFullPath),
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            LocationRouteAxisResolvedCount = routes.Count(route =>
                route.LocationRouteAxisResolved),
            LocationRouteMatchCount = routes.Count(route =>
                route.LocationRouteMatchesTargetDate == true),
            LocationRouteMissCount = routes.Count(route =>
                route.LocationRouteMatchesTargetDate == false),
            NotApplicableStaticWindowCount = routes.Count(route =>
                route.LocationRouteAxisStatus ==
                    "not_applicable_static_window_miss"),
            NotApplicableUnlockStateCount = routes.Count(route =>
                route.LocationRouteAxisStatus ==
                    "not_applicable_unlock_state_miss"),
            NotApplicableCalendarConditionCount = routes.Count(route =>
                route.LocationRouteAxisStatus ==
                    "not_applicable_calendar_condition_miss"),
            BlockedUpstreamCount = blockedUpstream,
            BlockedLocationEvidenceCount = blockedLocation,
            RouteOccurrenceInventoryComplete = true,
            LocationRouteAxisResolutionComplete = blockedUpstream == 0 &&
                blockedLocation == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static IReadOnlyDictionary<string,
        FutureLocationRouteDateEvidenceProduction> ProduceLocationRoutes(
            IEnumerable<AcquisitionLocationTargetResolution> resolutions,
            AcquisitionLocationRouteSnapshotState state,
            int targetTotalDay)
    {
        if (!state.RouteEvidenceAvailable)
        {
            return new Dictionary<string,
                FutureLocationRouteDateEvidenceProduction>(
                    StringComparer.OrdinalIgnoreCase);
        }

        var locations = resolutions
            .Where(resolution => resolution.EvidenceComplete)
            .SelectMany(resolution => resolution.Targets)
            .Select(target => target.LocationId)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requests = locations.Select(location =>
            new FutureLocationRouteDateEvidenceRequest
            {
                TotalDays = targetTotalDay,
                StartLocation = state.CurrentLocationId,
                StartTileX = state.CurrentTileX,
                StartTileY = state.CurrentTileY,
                EarliestDepartureTime = state.CurrentTime,
                TargetLocation = location
            }).ToArray();
        var productions = new FutureRouteDateEvidenceProducer()
            .ProduceLocationArrivals(
                state.RouteGraph,
                state.SocialRouteDateEvidence,
                requests,
                state.Timing!);
        return locations.Select((location, index) => new
        {
            Location = location,
            Production = productions[index]
        })
            .ToDictionary(
                value => value.Location,
                value => value.Production,
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsLocationApplicable(
        AcquisitionRouteTargetDateFestival route) =>
        route.CalendarConditionAxisResolved &&
        route.UpstreamRoute.StaticWindowMatchesTargetDate &&
        route.UpstreamRoute.UnlockStateMatchesTargetDate == true &&
        route.CalendarConditionsMatchTargetDate == true;
}
