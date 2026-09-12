using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFestivalBuilder
{
    public static AcquisitionRouteTargetDateFestivalReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string targetDateUnlockPath,
        string snapshotPath)
    {
        var sourcePath = Path.GetFullPath(targetDateUnlockPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateUnlockReport>(
            sourcePath,
            "Acquisition route target-date unlock state");
        var recomputed = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticCalendarResolutionPath,
            targetDateCalendarPath,
            snapshotFullPath);
        Require(EqualJson(source, recomputed),
            "Target-date unlock state drifted from deterministic source compilation.");
        ValidateSource(source);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        var calendarState = AcquisitionCalendarSnapshotState.Read(snapshot);
        Require(!calendarState.Available ||
                calendarState.CurrentTotalDay == source.TargetTotalDay,
            "Calendar bridge state disagrees with the target total day.");
        var evaluator = new AcquisitionCalendarConditionEvaluator(calendarState);
        var routes = source.Routes
            .Select(route => Evaluate(route, evaluator))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.CalendarConditionAxisStatus == "blocked_upstream_unlock_axis");
        var blockedEvidence = routes.Count(route =>
            route.CalendarConditionAxisStatus == "blocked_calendar_evidence");
        return new AcquisitionRouteTargetDateFestivalReport
        {
            Status = blockedUpstream == 0 && blockedEvidence == 0
                ? "complete_target_date_festival_axis_downstream_pending"
                : "partial_target_date_festival_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateUnlockSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            CalendarConditionAxisResolvedCount = routes.Count(route =>
                route.CalendarConditionAxisResolved),
            CalendarConditionMatchCount = routes.Count(route =>
                route.CalendarConditionsMatchTargetDate == true),
            CalendarConditionMissCount = routes.Count(route =>
                route.CalendarConditionsMatchTargetDate == false),
            NotApplicableStaticWindowCount = routes.Count(route =>
                route.CalendarConditionAxisStatus ==
                    "not_applicable_static_window_miss"),
            NotApplicableUnlockStateCount = routes.Count(route =>
                route.CalendarConditionAxisStatus ==
                    "not_applicable_unlock_state_miss"),
            BlockedUpstreamCount = blockedUpstream,
            BlockedCalendarEvidenceCount = blockedEvidence,
            PendingStochasticConditionCount = source.Routes.Sum(route =>
                route.PendingStochasticConditions.Length),
            PendingResourceConditionCount = source.Routes.Sum(route =>
                route.PendingResourceConditions.Length),
            PendingLocationConditionCount = source.Routes.Sum(route =>
                route.PendingLocationConditions.Length),
            UnsupportedConditionCount = source.Routes.Sum(route =>
                route.UnsupportedConditions.Length),
            RouteOccurrenceInventoryComplete = true,
            CalendarConditionAxisResolutionComplete = blockedUpstream == 0 &&
                blockedEvidence == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static AcquisitionRouteTargetDateFestival Evaluate(
        AcquisitionRouteTargetDateUnlock route,
        AcquisitionCalendarConditionEvaluator evaluator)
    {
        if (!route.UnlockAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_unlock_axis",
                false,
                null,
                Array.Empty<AcquisitionCalendarConditionEvaluation>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_unlock_axis_unresolved" });
        }
        if (!route.StaticWindowMatchesTargetDate)
        {
            return Result(
                route,
                "not_applicable_static_window_miss",
                true,
                null,
                Array.Empty<AcquisitionCalendarConditionEvaluation>(),
                Array.Empty<string>());
        }
        if (route.UnlockStateMatchesTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_unlock_state_miss",
                true,
                null,
                Array.Empty<AcquisitionCalendarConditionEvaluation>(),
                Array.Empty<string>());
        }
        Require(route.UnlockStateMatchesTargetDate == true,
            "A resolved static-window route lacks an unlock-state result.");

        if (route.PendingCalendarConditions.Length == 0)
        {
            return Result(
                route,
                "resolved_no_calendar_conditions",
                true,
                true,
                Array.Empty<AcquisitionCalendarConditionEvaluation>(),
                Array.Empty<string>());
        }

        var evaluations = route.PendingCalendarConditions
            .Select(evaluator.Evaluate)
            .ToArray();
        var blocking = evaluations
            .Where(value => value.ConditionMatches is null)
            .Select(value => value.BlockingReason!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Result(
                route,
                "blocked_calendar_evidence",
                false,
                null,
                evaluations,
                blocking);
        }

        var matches = evaluations.All(value => value.ConditionMatches == true);
        return Result(
            route,
            matches
                ? "resolved_calendar_conditions_match_downstream_pending"
                : "resolved_calendar_conditions_miss",
            true,
            matches,
            evaluations,
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateFestival Result(
        AcquisitionRouteTargetDateUnlock route,
        string status,
        bool resolved,
        bool? matches,
        AcquisitionCalendarConditionEvaluation[] evaluations,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            evaluations,
            blockingReasons);

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
