using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionTargetDateSnapshotValidator
{
    public static string Validate(
        JsonElement snapshot,
        string gameVersion,
        int targetTotalDay)
    {
        Require(CurrentTeacherFrontierSupport.RequiredString(
                    snapshot, "schema_version") == "snapshot.v1" &&
                CurrentTeacherFrontierSupport.RequiredString(
                    snapshot, "game_version") == gameVersion,
            "Snapshot schema or game version disagrees with the target-date report.");
        var stateHash = CurrentTeacherFrontierSupport.RequiredString(
            snapshot,
            "state_hash");
        Require(snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                TryReadAvailableIntField(
                    state, "time", "total_days", out var totalDay) &&
                totalDay == targetTotalDay,
            "Snapshot total day disagrees with the target-date report.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
