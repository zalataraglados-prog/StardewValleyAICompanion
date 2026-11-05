using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyNativeKeyReceiptBuilder
{
    private static void ValidateSnapshots(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        foreach (var snapshot in new[] { before, after })
            ValidateSnapshot(snapshot);
        Require(before.GameVersion == after.GameVersion && before.BridgeVersion == after.BridgeVersion &&
            Identity(before.SaveId) == Identity(after.SaveId) && Identity(before.PlayerId) == Identity(after.PlayerId) &&
            before.GameTick >= 0 && after.GameTick > before.GameTick && before.StateHash != after.StateHash,
            "snapshot_actor_version_or_frame_drift");
    }

    private static void ValidateOutcome(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        Require(Value(before, "player", "has_rusty_key").ValueKind == JsonValueKind.False &&
            Value(after, "player", "has_rusty_key").ValueKind == JsonValueKind.True,
            "native_key_false_to_true_missing");
        var beforeMuseum = Value(before, "world_progress", "museum");
        var afterMuseum = Value(after, "world_progress", "museum");
        Require(beforeMuseum.ValueKind == JsonValueKind.Object && afterMuseum.ValueKind == JsonValueKind.Object &&
            Integer(afterMuseum, "donated_count") == Integer(beforeMuseum, "donated_count") &&
            Integer(afterMuseum, "rusty_key_donation_threshold") == 60 &&
            String(afterMuseum, "rusty_key_reward_id") == "museum60" &&
            String(afterMuseum, "rusty_key_reward_action") == "MarkEventSeen Host 295672" &&
            ExplicitTrue(afterMuseum, "rusty_key_reward_claimed") && ExplicitTrue(afterMuseum, "rusty_key_prerequisite_event_seen") &&
            ExplicitTrue(afterMuseum, "rusty_key_event_seen") && ExplicitTrue(afterMuseum, "has_rusty_key"),
            "museum_native_terminal_mismatch");
        var story = Value(after, "player", "story_event");
        Require(story.ValueKind == JsonValueKind.Object && story.TryGetProperty("active", out var active) &&
            active.ValueKind == JsonValueKind.False && story.TryGetProperty("event_up", out var up) &&
            up.ValueKind == JsonValueKind.False, "native_event_control_not_restored");
        var beforeLocation = Value(before, "player", "location_id");
        var afterLocation = Value(after, "player", "location_id");
        Require(beforeLocation.ValueKind == JsonValueKind.String && beforeLocation.ValueEquals("Farm") &&
            afterLocation.ValueKind == JsonValueKind.String && afterLocation.ValueEquals("Farm"), "native_key_location_drift");
    }

    private static bool ExplicitTrue(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int Integer(JsonElement row, string name)
    {
        Require(row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "integer_field_missing_or_mistyped:" + name);
        return value.GetInt32();
    }

    private static string String(JsonElement row, string name) => CurrentTeacherFrontierSupport.RequiredString(row, name);

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Rusty Key native terminal: " + reason);
    }
}
