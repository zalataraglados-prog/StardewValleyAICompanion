using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateLocationBuilder
{
    private static readonly HashSet<string> UniversalWeatherModes = new(
        new[] { "sun", "rain", "storm", "green_rain" },
        StringComparer.Ordinal);

    private static AcquisitionRouteTargetDateLocation Evaluate(
        AcquisitionRouteTargetDateFestival route,
        AcquisitionLocationRouteSnapshotState state,
        IReadOnlyDictionary<string, AcquisitionLocationTargetResolution>
            targetByOccurrence,
        IReadOnlyDictionary<string, FutureLocationRouteDateEvidenceProduction>
            routeByLocation)
    {
        if (!route.CalendarConditionAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_calendar_condition_axis",
                false,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_calendar_condition_axis_unresolved" });
        }
        if (!route.UpstreamRoute.StaticWindowMatchesTargetDate)
        {
            return Result(
                route,
                "not_applicable_static_window_miss",
                true,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (route.UpstreamRoute.UnlockStateMatchesTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_unlock_state_miss",
                true,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (route.CalendarConditionsMatchTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_calendar_condition_miss",
                true,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        Require(route.UpstreamRoute.UnlockStateMatchesTargetDate == true &&
                route.CalendarConditionsMatchTargetDate == true,
            "A location-applicable route lacks an upstream match result.");

        if (route.UpstreamRoute.PendingLocationConditions.Length > 0)
        {
            return Result(
                route,
                "blocked_location_route_evidence",
                false,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                route.UpstreamRoute.PendingLocationConditions
                    .Select(condition =>
                        "location_condition_evaluator_pending:" + condition)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }

        if (!state.RouteEvidenceAvailable)
        {
            return Result(
                route,
                "blocked_location_route_evidence",
                false,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                state.RouteEvidenceBlockingReasons.Length > 0
                    ? state.RouteEvidenceBlockingReasons
                    : new[] { "location_route_evidence_unavailable" });
        }
        if (!targetByOccurrence.TryGetValue(
                route.RouteOccurrenceId,
                out var targetResolution) ||
            targetResolution is null)
        {
            throw new InvalidDataException(
                "An applicable route lacks a location target resolution.");
        }
        if (!targetResolution.EvidenceComplete)
        {
            return Result(
                route,
                "blocked_location_route_evidence",
                false,
                null,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                Array.Empty<string>(),
                targetResolution.Reasons);
        }
        if (targetResolution.Targets.Length == 0)
        {
            return Result(
                route,
                "resolved_location_route_miss",
                true,
                false,
                Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
                targetResolution.Reasons.Length > 0
                    ? targetResolution.Reasons
                    : new[] { "no_target_location_bound_on_target_date" },
                Array.Empty<string>());
        }

        var evaluations = targetResolution.Targets
            .Select(target => EvaluateTarget(
                route,
                target,
                state,
                routeByLocation))
            .ToArray();
        if (evaluations.Any(value => value.Status ==
                "resolved_location_route_match"))
        {
            return Result(
                route,
                "resolved_location_route_match_downstream_pending",
                true,
                true,
                evaluations,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var blocking = evaluations
            .SelectMany(value => value.BlockingReasons)
            .Concat(targetResolution.Reasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Result(
                route,
                "blocked_location_route_evidence",
                false,
                null,
                evaluations,
                Array.Empty<string>(),
                blocking);
        }

        var nonMatching = evaluations
            .Select(value => value.Status + ":" + value.TargetLocationId)
            .Concat(targetResolution.Reasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Result(
            route,
            "resolved_location_route_miss",
            true,
            false,
            evaluations,
            nonMatching,
            Array.Empty<string>());
    }

    private static AcquisitionLocationRouteTargetEvaluation EvaluateTarget(
        AcquisitionRouteTargetDateFestival route,
        AcquisitionLocationTarget target,
        AcquisitionLocationRouteSnapshotState state,
        IReadOnlyDictionary<string, FutureLocationRouteDateEvidenceProduction>
            routeByLocation)
    {
        state.TryGetLocation(target.LocationId, out var location);
        var contextId = location?.LocationContextId ?? string.Empty;
        if (!routeByLocation.TryGetValue(target.LocationId, out var production) ||
            production.Status != FutureRouteDateEvidenceProductionStatus.Produced ||
            !production.GuaranteedArrivalByTime.HasValue)
        {
            var blocking = production?.BlockingReasons.Length > 0
                ? production.BlockingReasons
                : new[] { "future_location_route_production_missing" };
            var resolvedMiss = production is not null &&
                blocking.Length > 0 &&
                blocking.All(IsConclusiveRouteMiss);
            return TargetResult(
                target,
                contextId,
                resolvedMiss
                    ? "resolved_route_unavailable_on_target_date"
                    : "blocked_route_evidence",
                string.Empty,
                production?.GuaranteedArrivalByTime,
                0,
                production,
                target.EvidencePaths,
                resolvedMiss ? Array.Empty<string>() : blocking);
        }

        var arrival = production.GuaranteedArrivalByTime.Value;
        var timeFeasible = route.UpstreamRoute.MatchingWindows
            .Where(window => window.TimeWindows.Any(time =>
                Math.Max(arrival, time.StartTime) < time.EndTime))
            .ToArray();
        if (timeFeasible.Length == 0)
        {
            return TargetResult(
                target,
                contextId,
                "resolved_arrival_outside_source_window",
                string.Empty,
                arrival,
                0,
                production,
                EvidencePaths(target, false),
                Array.Empty<string>());
        }

        var universal = timeFeasible.Where(window =>
            UniversalWeatherModes.SetEquals(window.WeatherModes)).ToArray();
        string sourceWeather;
        AuthoritativeCalendarSourceWindow[] matching;
        var usesWeatherEvidence = false;
        if (universal.Length > 0)
        {
            sourceWeather = "all";
            matching = universal;
        }
        else if (!state.TryGetWeather(
                     target.LocationId,
                     out sourceWeather,
                     out _))
        {
            return TargetResult(
                target,
                contextId,
                "blocked_location_weather_evidence",
                string.Empty,
                arrival,
                0,
                production,
                EvidencePaths(target, false),
                new[]
                {
                    "target_location_weather_context_evidence_missing:" +
                    target.LocationId
                });
        }
        else
        {
            usesWeatherEvidence = true;
            matching = timeFeasible.Where(window =>
                window.WeatherModes.Contains(
                    sourceWeather,
                    StringComparer.Ordinal)).ToArray();
        }

        return TargetResult(
            target,
            contextId,
            matching.Length > 0
                ? "resolved_location_route_match"
                : "resolved_source_weather_miss",
            sourceWeather,
            arrival,
            matching.Length,
            production,
            EvidencePaths(target, usesWeatherEvidence),
            Array.Empty<string>());
    }

    private static bool IsConclusiveRouteMiss(string reason) => reason is
        "future_route_source_map_inaccessible_on_date" or
        "future_route_target_map_inaccessible_on_date" or
        "future_route_connector_approach_unreachable" or
        "future_route_connector_not_allowed_on_date" or
        "future_route_connector_closed_before_arrival" or
        "future_route_connector_arrival_outside_supported_day";

    private static string[] EvidencePaths(
        AcquisitionLocationTarget target,
        bool weather) => target.EvidencePaths
        .Concat(new[]
        {
            "state.locations.route_graph.value",
            "state.locations.social_route_date_evidence.value",
            "static_calendar_resolution.routes[].calendar_windows[].time_windows",
            "route_timing_calibration"
        })
        .Concat(weather
            ? new[]
            {
                "state.locations.social_route_date_evidence.value.locations[].location_context_id",
                "state.time.location_context_weather.value[]"
            }
            : Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static AcquisitionLocationRouteTargetEvaluation TargetResult(
        AcquisitionLocationTarget target,
        string locationContextId,
        string status,
        string sourceWeather,
        int? arrival,
        int matchedWindowCount,
        FutureLocationRouteDateEvidenceProduction? production,
        string[] evidencePaths,
        string[] blockingReasons) => new(
            target.BindingKind,
            target.SourceKey,
            target.LocationId,
            locationContextId,
            status,
            sourceWeather,
            arrival,
            matchedWindowCount,
            production?.TimingEvidenceKind.ToString() ?? string.Empty,
            production?.TimingEvidenceId ?? string.Empty,
            production?.Path ?? Array.Empty<TransparentRouteEdge>(),
            evidencePaths,
            blockingReasons);

    private static AcquisitionRouteTargetDateLocation Result(
        AcquisitionRouteTargetDateFestival route,
        string status,
        bool resolved,
        bool? matches,
        AcquisitionLocationRouteTargetEvaluation[] evaluations,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            evaluations,
            nonMatchingReasons,
            blockingReasons);
}
