using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRouteTargetDateCalendarBuilder
{
    private const string ResolvedSourceStatus =
        "resolved_static_source_window_target_date_pending";

    public static AcquisitionRouteTargetDateCalendarReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        int targetTotalDay)
    {
        var inventoryFullPath = Path.GetFullPath(inventoryPath);
        var loweringFullPath = Path.GetFullPath(loweringPath);
        var windowFullPath = Path.GetFullPath(masterAnglerWindowIndexPath);
        var calendarFullPath = Path.GetFullPath(staticCalendarResolutionPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            calendarFullPath,
            "Acquisition route static calendar resolution");
        var recomputed = AcquisitionRouteCalendarResolutionBuilder.Build(
            inventoryFullPath,
            loweringFullPath,
            windowFullPath);
        Require(EqualJson(source, recomputed),
            "Static calendar resolution drifted from deterministic source compilation.");
        ValidateSource(source, targetTotalDay);

        var routes = source.Routes.Select(route => Evaluate(route, targetTotalDay))
            .ToArray();
        var resolved = routes.Count(route => route.CalendarAxisResolved);
        var eligible = routes.Count(route =>
            route.StaticWindowMatchesTargetDate);
        var ineligible = routes.Count(route =>
            route.CalendarAxisResolved && !route.StaticWindowMatchesTargetDate);
        var blocked = routes.Length - resolved;
        return new AcquisitionRouteTargetDateCalendarReport
        {
            Status = blocked == 0
                ? "complete_target_date_calendar_axis_downstream_pending"
                : "partial_target_date_calendar_axis_source_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            RequirementInventorySha256 =
                CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
            AcquisitionLoweringSha256 =
                CurrentTeacherFrontierSupport.HashFile(loweringFullPath),
            StaticCalendarResolutionSha256 =
                CurrentTeacherFrontierSupport.HashFile(calendarFullPath),
            TargetTotalDay = targetTotalDay,
            DeadlineTotalDayExclusive = source.DeadlineTotalDayExclusive,
            RouteOccurrenceCount = routes.Length,
            CalendarAxisResolvedCount = resolved,
            StaticWindowMatchCount = eligible,
            StaticWindowMissCount = ineligible,
            BlockedStaticSourceCount = blocked,
            RouteOccurrenceInventoryComplete = true,
            CalendarAxisResolutionComplete = blocked == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static AcquisitionRouteTargetDateCalendar Evaluate(
        AcquisitionRouteCalendarResolution route,
        int targetTotalDay)
    {
        if (route.Status != ResolvedSourceStatus)
        {
            return Result(
                route,
                "blocked_static_calendar_source_unresolved",
                false,
                false,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                Array.Empty<string>(),
                route.BlockingReasons);
        }

        var matching = route.CalendarWindows
            .Where(window => targetTotalDay >= window.FirstTotalDay &&
                targetTotalDay <= window.LastTotalDay)
            .ToArray();
        if (matching.Length == 0)
        {
            return Result(
                route,
                "resolved_target_date_outside_static_window",
                true,
                false,
                matching,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        return Result(
            route,
            "resolved_target_date_inside_static_window_downstream_pending",
            true,
            true,
            matching,
            matching.SelectMany(window => window.DynamicConditions)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateCalendar Result(
        AcquisitionRouteCalendarResolution route,
        string axisStatus,
        bool axisResolved,
        bool eligible,
        AuthoritativeCalendarSourceWindow[] matching,
        string[] dynamicConditions,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route.RequirementSetId,
            route.RequirementId,
            route.AlternativeIndex,
            route.RouteIndex,
            route.QualifiedItemId,
            route.RouteKind,
            route.SourceId,
            route.Status,
            axisStatus,
            axisResolved,
            eligible,
            matching,
            dynamicConditions,
            blockingReasons);

    private static void ValidateSource(
        AcquisitionRouteCalendarResolutionReport source,
        int targetTotalDay)
    {
        Require(source.SchemaVersion == "acquisition_route_calendar_resolution.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() == source.Routes.Length,
            "Static calendar resolution metadata is incomplete.");
        Require(targetTotalDay >= 0 &&
                targetTotalDay < source.DeadlineTotalDayExclusive,
            "Target total day is outside the authoritative deadline horizon.");
        Require(source.ResolvedStaticSourceCount == source.Routes.Count(route =>
                    route.Status == ResolvedSourceStatus) &&
                source.BlockedStaticSourceCount == source.Routes.Count(route =>
                    route.Status != ResolvedSourceStatus),
            "Static calendar resolution counts drifted.");

        foreach (var route in source.Routes)
        {
            if (route.Status != ResolvedSourceStatus)
            {
                Require(route.CalendarWindows.Length == 0,
                    "A blocked source route carries calendar windows.");
                continue;
            }
            Require(route.CalendarWindows.Length > 0 &&
                    route.BlockingReasons.Length == 0,
                "A resolved source route lacks usable calendar windows.");
            foreach (var window in route.CalendarWindows)
            {
                Require(window.FirstTotalDay >= 0 &&
                        window.FirstTotalDay <= window.LastTotalDay &&
                        window.LastTotalDay < source.DeadlineTotalDayExclusive &&
                        window.TimeWindows.Length > 0 &&
                        window.TimeWindows.All(value =>
                            value.StartTime < value.EndTime) &&
                        window.WeatherModes.Length > 0 &&
                        !string.IsNullOrWhiteSpace(window.SourceKind) &&
                        !string.IsNullOrWhiteSpace(window.SourceKey),
                    "A resolved source route contains an invalid calendar window.");
            }
        }
    }

    private static bool EqualJson<T>(T left, T right)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return string.Equals(
            JsonSerializer.Serialize(left, options),
            JsonSerializer.Serialize(right, options),
            StringComparison.Ordinal);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
