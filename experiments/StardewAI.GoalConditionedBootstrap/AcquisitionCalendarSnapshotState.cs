using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record AcquisitionCalendarSnapshotState(
    bool Available,
    string BlockingReason,
    int CurrentTotalDay,
    int TimeOfDay,
    uint DaysPlayed,
    HashSet<string> FestivalDateKeys,
    HashSet<string> ActivePassiveFestivalIds,
    Dictionary<string, int> PassiveFestivalStartTimes)
{
    private const string FieldName = "game_state_query_calendar_state";
    private const string ExpectedAdapter = "vanilla_1_6_15_gsq_calendar";

    public static AcquisitionCalendarSnapshotState Read(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("state", out var state) ||
            state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty(FieldName, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object)
        {
            return Unavailable("game_state_query_calendar_state_missing");
        }
        if (!envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() != "available")
        {
            return Unavailable("game_state_query_calendar_state_unavailable");
        }

        var value = default(JsonElement);
        Require(envelope.TryGetProperty("adapter", out var adapter) &&
                adapter.ValueKind == JsonValueKind.String &&
                adapter.GetString() == ExpectedAdapter &&
                envelope.TryGetProperty("value", out value) &&
                value.ValueKind == JsonValueKind.Object,
            "Available calendar state lacks the exact bridge adapter or object value.");
        var currentTotalDay = 0;
        var timeOfDay = 0;
        uint daysPlayed = 0;
        Require(value.TryGetProperty("current_total_day", out var totalDay) &&
                totalDay.TryGetInt32(out currentTotalDay) &&
                value.TryGetProperty("time_of_day", out var time) &&
                time.TryGetInt32(out timeOfDay) && timeOfDay >= 0 &&
                value.TryGetProperty("days_played", out var days) &&
                days.TryGetUInt32(out daysPlayed),
            "Calendar state date or time metadata is incomplete.");
        Require(value.TryGetProperty(
                    "festival_location_context_resolution_status",
                    out var contextStatus) &&
                contextStatus.ValueKind == JsonValueKind.String &&
                contextStatus.GetString() == "not_projected",
            "Calendar state location-context policy is missing.");
        Require(TryReadAvailableIntField(state, "time", out var stateTime) &&
                stateTime == timeOfDay,
            "Calendar bridge time disagrees with the snapshot time field.");

        return new AcquisitionCalendarSnapshotState(
            true,
            string.Empty,
            currentTotalDay,
            timeOfDay,
            daysPlayed,
            RequiredStringSet(value, "festival_date_keys"),
            RequiredStringSet(value, "active_passive_festival_ids"),
            RequiredPassiveFestivalStartTimes(value));
    }

    private static Dictionary<string, int> RequiredPassiveFestivalStartTimes(
        JsonElement value)
    {
        Require(value.TryGetProperty("passive_festivals", out var rows) &&
                rows.ValueKind == JsonValueKind.Array,
            "Calendar state passive-festival catalog is missing.");
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            var idValue = default(JsonElement);
            Require(row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty("festival_id", out idValue) &&
                    idValue.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(idValue.GetString()) &&
                    row.TryGetProperty("season", out var season) &&
                    season.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(season.GetString()) &&
                    row.TryGetProperty("start_day", out var startDay) &&
                    startDay.TryGetInt32(out var start) && start > 0 &&
                    row.TryGetProperty("end_day", out var endDay) &&
                    endDay.TryGetInt32(out var end) && end >= start,
                "Calendar state passive-festival row is malformed.");
            Require(row.TryGetProperty("start_time", out var startTime) &&
                    startTime.TryGetInt32(out var startTimeValue) &&
                    startTimeValue >= 0 &&
                    row.TryGetProperty("condition", out var condition) &&
                    (condition.ValueKind is JsonValueKind.String or
                        JsonValueKind.Null) &&
                    result.TryAdd(idValue.GetString()!, startTimeValue),
                "Calendar state passive-festival row is invalid or duplicated.");
        }
        return result;
    }

    private static HashSet<string> RequiredStringSet(
        JsonElement value,
        string propertyName)
    {
        Require(value.TryGetProperty(propertyName, out var rows) &&
                rows.ValueKind == JsonValueKind.Array,
            "Calendar state " + propertyName + " is missing.");
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.EnumerateArray())
        {
            Require(row.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(row.GetString()) &&
                    result.Add(row.GetString()!),
                "Calendar state " + propertyName +
                " contains an invalid or duplicate value.");
        }
        return result;
    }

    private static bool TryReadAvailableIntField(
        JsonElement state,
        string fieldName,
        out int value)
    {
        value = 0;
        return state.TryGetProperty("time", out var time) &&
            time.ValueKind == JsonValueKind.Object &&
            time.TryGetProperty(fieldName, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out var fieldValue) &&
            fieldValue.TryGetInt32(out value);
    }

    private static AcquisitionCalendarSnapshotState Unavailable(string reason) =>
        new(
            false,
            reason,
            0,
            0,
            0,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
