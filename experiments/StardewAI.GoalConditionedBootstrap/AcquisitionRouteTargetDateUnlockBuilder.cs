using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateUnlockBuilder
{
    public static AcquisitionRouteTargetDateUnlockReport Build(
        string inventoryPath,
        string loweringPath,
        string masterAnglerWindowIndexPath,
        string staticCalendarResolutionPath,
        string targetDateCalendarPath,
        string snapshotPath)
    {
        var targetPath = Path.GetFullPath(targetDateCalendarPath);
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var source = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateCalendarReport>(
            targetPath,
            "Acquisition route target-date calendar");
        var recomputed = AcquisitionRouteTargetDateCalendarBuilder.Build(
            inventoryPath,
            loweringPath,
            masterAnglerWindowIndexPath,
            staticCalendarResolutionPath,
            source.TargetTotalDay);
        Require(EqualJson(source, recomputed),
            "Target-date calendar drifted from deterministic source compilation.");
        ValidateSource(source);

        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotFullPath));
        var snapshot = snapshotDocument.RootElement;
        var stateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            snapshot,
            source.GameVersion,
            source.TargetTotalDay);
        var evaluator = new AcquisitionUnlockConditionEvaluator(snapshot);
        var routes = source.Routes
            .Select(route => Evaluate(route, evaluator))
            .ToArray();

        var blockedUpstream = routes.Count(route =>
            route.UnlockAxisStatus == "blocked_upstream_calendar");
        var blockedEvidence = routes.Count(route =>
            route.StaticWindowMatchesTargetDate &&
            !route.UnlockAxisResolved);
        return new AcquisitionRouteTargetDateUnlockReport
        {
            Status = blockedUpstream == 0 && blockedEvidence == 0
                ? "complete_target_date_unlock_axis_downstream_pending"
                : "partial_target_date_unlock_axis_blocks",
            GoalId = source.GoalId,
            GameVersion = source.GameVersion,
            TargetDateCalendarSha256 =
                CurrentTeacherFrontierSupport.HashFile(targetPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            SnapshotStateHash = stateHash,
            TargetTotalDay = source.TargetTotalDay,
            RouteOccurrenceCount = routes.Length,
            UnlockAxisResolvedCount = routes.Count(route =>
                route.UnlockAxisResolved),
            UnlockStateMatchCount = routes.Count(route =>
                route.UnlockStateMatchesTargetDate == true),
            UnlockStateMissCount = routes.Count(route =>
                route.UnlockStateMatchesTargetDate == false),
            StaticWindowMissCount = routes.Count(route =>
                route.UnlockAxisStatus == "not_applicable_static_window_miss"),
            BlockedUpstreamCalendarCount = blockedUpstream,
            BlockedUnlockEvidenceCount = blockedEvidence,
            PendingCalendarConditionCount = routes.Sum(route =>
                route.PendingCalendarConditions.Length),
            PendingStochasticConditionCount = routes.Sum(route =>
                route.PendingStochasticConditions.Length),
            PendingResourceConditionCount = routes.Sum(route =>
                route.PendingResourceConditions.Length),
            PendingLocationConditionCount = routes.Sum(route =>
                route.PendingLocationConditions.Length),
            UnsupportedConditionCount = routes.Sum(route =>
                route.UnsupportedConditions.Length),
            RouteOccurrenceInventoryComplete = true,
            UnlockAxisResolutionComplete = blockedUpstream == 0 &&
                blockedEvidence == 0,
            TrainingLabelEligible = false,
            Routes = routes
        };
    }

    private static AcquisitionRouteTargetDateUnlock Evaluate(
        AcquisitionRouteTargetDateCalendar route,
        AcquisitionUnlockConditionEvaluator evaluator)
    {
        if (!route.CalendarAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_calendar",
                false,
                null,
                Array.Empty<AcquisitionUnlockConditionEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_calendar_axis_unresolved" });
        }
        if (!route.StaticWindowMatchesTargetDate)
        {
            return Result(
                route,
                "not_applicable_static_window_miss",
                true,
                null,
                Array.Empty<AcquisitionUnlockConditionEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var unlock = new List<AcquisitionUnlockConditionEvaluation>();
        var calendar = new List<string>();
        var stochastic = new List<string>();
        var resource = new List<string>();
        var location = new List<string>();
        var unsupported = new List<string>();
        foreach (var condition in route.PendingDynamicConditions)
        {
            switch (AcquisitionUnlockConditionEvaluator.Classify(condition))
            {
                case AcquisitionUnlockConditionEvaluator.UnlockAxis:
                    unlock.Add(evaluator.Evaluate(condition));
                    break;
                case AcquisitionUnlockConditionEvaluator.CalendarAxis:
                    calendar.Add(condition);
                    break;
                case AcquisitionUnlockConditionEvaluator.StochasticAxis:
                    stochastic.Add(condition);
                    break;
                case AcquisitionUnlockConditionEvaluator.ResourceAxis:
                    resource.Add(condition);
                    break;
                case AcquisitionUnlockConditionEvaluator.LocationAxis:
                    location.Add(condition);
                    break;
                default:
                    unsupported.Add(condition);
                    break;
            }
        }

        var blocking = unlock
            .Where(value => value.ConditionMatches is null)
            .Select(value => value.BlockingReason!)
            .Concat(unsupported.Select(value =>
                "unsupported_dynamic_condition:" + value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Result(route, "blocked_unlock_evidence", false, null,
                unlock.ToArray(), calendar, stochastic, resource, location,
                unsupported, blocking);
        }

        var matches = unlock.All(value => value.ConditionMatches == true);
        return Result(
            route,
            matches
                ? "resolved_unlock_state_match_downstream_pending"
                : "resolved_unlock_state_miss",
            true,
            matches,
            unlock.ToArray(),
            calendar,
            stochastic,
            resource,
            location,
            unsupported,
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateUnlock Result(
        AcquisitionRouteTargetDateCalendar route,
        string status,
        bool resolved,
        bool? matches,
        AcquisitionUnlockConditionEvaluation[] unlock,
        IEnumerable<string> calendar,
        IEnumerable<string> stochastic,
        IEnumerable<string> resource,
        IEnumerable<string> location,
        IEnumerable<string> unsupported,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route.RequirementSetId,
            route.RequirementId,
            route.AlternativeIndex,
            route.RouteIndex,
            route.QualifiedItemId,
            route.RouteKind,
            route.SourceId,
            route.SourceResolutionStatus,
            route.CalendarAxisStatus,
            route.StaticWindowMatchesTargetDate,
            route.MatchingWindows,
            status,
            resolved,
            matches,
            unlock,
            Ordered(calendar),
            Ordered(stochastic),
            Ordered(resource),
            Ordered(location),
            Ordered(unsupported),
            blockingReasons);

    private static string[] Ordered(IEnumerable<string> values) => values
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

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
