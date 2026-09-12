using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateUnlockBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateCalendarReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_calendar.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date calendar metadata is incomplete.");
        Require(source.CalendarAxisResolvedCount == source.Routes.Count(route =>
                    route.CalendarAxisResolved) &&
                source.StaticWindowMatchCount == source.Routes.Count(route =>
                    route.StaticWindowMatchesTargetDate) &&
                source.StaticWindowMissCount == source.Routes.Count(route =>
                    route.CalendarAxisResolved &&
                    !route.StaticWindowMatchesTargetDate) &&
                source.BlockedStaticSourceCount == source.Routes.Count(route =>
                    !route.CalendarAxisResolved),
            "Target-date calendar counts drifted.");
    }

    private static string ValidateSnapshot(
        JsonElement snapshot,
        string gameVersion,
        int targetTotalDay)
    {
        Require(CurrentTeacherFrontierSupport.RequiredString(
                    snapshot, "schema_version") == "snapshot.v1" &&
                CurrentTeacherFrontierSupport.RequiredString(
                    snapshot, "game_version") == gameVersion,
            "Snapshot schema or game version disagrees with the calendar report.");
        var stateHash = CurrentTeacherFrontierSupport.RequiredString(
            snapshot,
            "state_hash");
        Require(snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                TryReadAvailableIntField(
                    state, "time", "total_days", out var totalDay) &&
                totalDay == targetTotalDay,
            "Snapshot total day disagrees with the target-date calendar.");
        return stateHash;
    }

    private static bool TryReadAvailableIntField(
        JsonElement state,
        string section,
        string field,
        out int value)
    {
        value = 0;
        return state.TryGetProperty(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out var fieldValue) &&
            fieldValue.TryGetInt32(out value);
    }
}
