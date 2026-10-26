using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed class RouteOscillationGuard
{
    private readonly int maximumReverseTransitionStreak;
    private RouteTransition? previous;

    public RouteOscillationGuard(int maximumReverseTransitionStreak = 2)
    {
        this.maximumReverseTransitionStreak = Math.Max(
            1,
            maximumReverseTransitionStreak);
    }

    public int ReverseTransitionStreak { get; private set; }

    public RouteOscillationDecision Observe(
        JsonObject execution,
        JsonObject beforeSnapshot,
        JsonObject afterSnapshot)
    {
        if (!TryReadTransition(
                execution,
                beforeSnapshot,
                afterSnapshot,
                out var current))
        {
            Reset();
            return RouteOscillationDecision.None;
        }

        var reversesPrevious = previous is not null &&
            string.Equals(
                current.FromLocation,
                previous.ToLocation,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                current.ToLocation,
                previous.FromLocation,
                StringComparison.OrdinalIgnoreCase) &&
            current.BeforeTotalDay == previous.AfterTotalDay &&
            current.BeforeTime == previous.AfterTime &&
            current.AfterTotalDay == current.BeforeTotalDay &&
            current.AfterTime == current.BeforeTime;
        ReverseTransitionStreak = reversesPrevious
            ? ReverseTransitionStreak + 1
            : 0;
        previous = current;

        var detected = ReverseTransitionStreak >=
            maximumReverseTransitionStreak;
        return new RouteOscillationDecision(
            detected,
            ReverseTransitionStreak,
            current.ObjectiveKey,
            current.FromLocation,
            current.ToLocation,
            current.BeforeTotalDay,
            current.BeforeTime);
    }

    public void Reset()
    {
        previous = null;
        ReverseTransitionStreak = 0;
    }

    public static bool IsObservedRouteExecution(JsonObject execution) =>
        string.Equals(
            ReadString(execution, "status"),
            "applied",
            StringComparison.Ordinal) &&
        string.Equals(
            ReadString(execution, "primitive_kind"),
            "traverse_connector",
            StringComparison.Ordinal) &&
        string.Equals(
            ReadString(execution, "primitive_verification_status"),
            "verified",
            StringComparison.Ordinal) &&
        execution["after_snapshot_fresh"]?.GetValue<bool>() == true;

    private static bool TryReadTransition(
        JsonObject execution,
        JsonObject beforeSnapshot,
        JsonObject afterSnapshot,
        out RouteTransition transition)
    {
        transition = null!;
        if (!IsObservedRouteExecution(execution))
        {
            return false;
        }

        if (!TryReadSnapshotPosition(
                beforeSnapshot,
                out var fromLocation,
                out var beforeTotalDay,
                out var beforeTime) ||
            !TryReadSnapshotPosition(
                afterSnapshot,
                out var toLocation,
                out var afterTotalDay,
                out var afterTime) ||
            string.Equals(
                fromLocation,
                toLocation,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        transition = new RouteTransition(
            ReadObjectiveKey(execution),
            fromLocation,
            toLocation,
            beforeTotalDay,
            beforeTime,
            afterTotalDay,
            afterTime);
        return true;
    }

    private static string ReadObjectiveKey(JsonObject execution)
    {
        if (execution["objective_continuation"] is not JsonObject continuation)
        {
            return ReadString(execution, "option_id");
        }

        var kind = ReadString(continuation, "kind");
        var optionId = ReadString(continuation, "option_id");
        var targetId = ReadString(
            continuation,
            "master_angler_target_qualified_item_id");
        return string.Join(
            "|",
            new[] { kind, optionId, targetId }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static bool TryReadSnapshotPosition(
        JsonObject snapshot,
        out string location,
        out int totalDay,
        out int time)
    {
        location = ReadSnapshotFieldString(
            snapshot,
            "player",
            "location_id");
        totalDay = 0;
        time = 0;
        return !string.IsNullOrWhiteSpace(location) &&
            TryReadSnapshotFieldInt(
                snapshot,
                "time",
                "total_days",
                out totalDay) &&
            TryReadSnapshotFieldInt(snapshot, "time", "time", out time);
    }

    private static string ReadSnapshotFieldString(
        JsonObject snapshot,
        string domain,
        string field) =>
        snapshot["state"]?[domain]?[field]?["value"] is JsonValue value &&
        value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;

    private static bool TryReadSnapshotFieldInt(
        JsonObject snapshot,
        string domain,
        string field,
        out int result)
    {
        result = 0;
        return snapshot["state"]?[domain]?[field]?["value"] is
                JsonValue value &&
            value.TryGetValue<int>(out result);
    }

    private static string ReadString(
        JsonObject value,
        string propertyName) =>
        value[propertyName] is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;

    private sealed record RouteTransition(
        string ObjectiveKey,
        string FromLocation,
        string ToLocation,
        int BeforeTotalDay,
        int BeforeTime,
        int AfterTotalDay,
        int AfterTime);
}

public sealed record RouteOscillationDecision(
    bool Detected,
    int ReverseTransitionStreak,
    string ObjectiveKey,
    string FromLocation,
    string ToLocation,
    int TotalDay,
    int Time)
{
    public static RouteOscillationDecision None { get; } = new(
        false,
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        -1,
        -1);
}
