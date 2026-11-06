using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageNativeJointReceiptBuilder
{
    private static void ValidateIdentity(SnapshotEnvelope first, VerifiedNativeExecutionQueue source, string runId)
    {
        foreach (var snapshot in source.Actions.SelectMany(frame => new[] { frame.Before, frame.After }))
        {
            Require(snapshot.BridgeVersion == first.BridgeVersion && Identity(snapshot.SaveId) == Identity(first.SaveId) &&
                Identity(snapshot.PlayerId) == Identity(first.PlayerId) && TotalDay(snapshot) is >= 0 and < 224 &&
                Value(snapshot, "environment", "training_run_id").GetString() == runId &&
                Value(snapshot, "environment", "save_isolation_path").GetString() == Value(first, "environment", "save_isolation_path").GetString(),
                "Marriage chain changed run/save/player/isolation/version or crossed the evaluation deadline.");
            var year = Value(snapshot, "time", "year").GetInt32();
            var season = Array.IndexOf(new[] { "spring", "summer", "fall", "winter" }, Value(snapshot, "time", "season").GetString());
            var day = Value(snapshot, "time", "day").GetInt32();
            Require(year >= 1 && season >= 0 && day is >= 1 and <= 28 &&
                (year - 1L) * 112 + season * 28 + day - 1 == TotalDay(snapshot),
                "Marriage native calendar and canonical total-day disagree.");
        }
    }

    private static CurrentMarriageHouseState State(SnapshotEnvelope snapshot)
    {
        var state = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        Require(state.Status == "ready_current_state_only", "Marriage native current facts are incomplete or inconsistent.");
        return state;
    }

    private static JsonElement Friend(SnapshotEnvelope snapshot, string npc)
    {
        var rows = Value(snapshot, "npcs", "friendships").EnumerateArray()
            .Where(row => row.GetProperty("npc_name").GetString() == npc).ToArray();
        Require(rows.Length == 1, "Marriage native partner relationship is missing or ambiguous.");
        return rows[0];
    }

    private static int Int(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out _),
            "Marriage native integer missing: " + name);
        return value.GetProperty(name).GetInt32();
    }

    private static bool EventActive(SnapshotEnvelope snapshot)
    {
        var story = Value(snapshot, "player", "story_event");
        Require(story.TryGetProperty("active", out var active) && active.ValueKind is JsonValueKind.True or JsonValueKind.False &&
            story.TryGetProperty("event_up", out var up) && up.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Marriage native story boundary is unreadable.");
        return active.GetBoolean() || story.GetProperty("event_up").GetBoolean();
    }

    private static bool MenuOpen(SnapshotEnvelope snapshot)
    {
        var menu = Value(snapshot, "menus", "active_menu");
        Require(menu.TryGetProperty("is_open", out var open) && open.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Marriage native menu boundary is unreadable.");
        return open.GetBoolean();
    }
}
