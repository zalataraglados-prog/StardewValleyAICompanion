using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageNativeJointContinuity
{
    // This validates a boundary projection, not the native action. The receipt
    // builder separately replays the complete original wedding queue first.
    public static void ValidateWeddingReturn(SnapshotEnvelope weddingBefore, SnapshotEnvelope weddingAfter, SnapshotEnvelope settled)
    {
        ValidateSnapshot(weddingBefore);
        ValidateCore(weddingAfter, settled, allowWeddingExit: true);
        var story = Value(weddingBefore, "player", "story_event");
        var context = Value(weddingAfter, "player", "return_scepter");
        Require(story.GetProperty("event_id").GetString() == "-2" && story.GetProperty("active").ValueKind == JsonValueKind.True &&
            Value(weddingAfter, "player", "movement_timing_context").GetProperty("is_warping").ValueKind == JsonValueKind.True &&
            Value(settled, "player", "movement_timing_context").GetProperty("is_warping").ValueKind == JsonValueKind.False &&
            Value(settled, "player", "movement_timing_context").GetProperty("player_can_move_now").ValueKind == JsonValueKind.True &&
            context.GetProperty("projection_status").GetString() == "complete_current_native_return_scepter_context",
            "Marriage deferred return lacks the native wedding/warp/control boundary.");
        var destination = context.GetProperty("destination");
        var settledDestination = Value(settled, "player", "return_scepter").GetProperty("destination");
        Require(destination.GetProperty("home_runtime_type").GetString() == "FarmHouse" &&
            destination.GetProperty("home_is_cabin").ValueKind == JsonValueKind.False &&
            destination.GetProperty("home_location_id").GetString() == "FarmHouse" &&
            destination.GetProperty("destination_location_id").GetString() == "Farm" &&
            SameNativeHome(destination, settledDestination),
            "Marriage deferred-return proof requires the same native main farmhouse entry; cabins need their own source.");
        // Pinned vanilla FarmHouse porch is main entry + (2,0). Event's wedding
        // end sets the non-cabin exit one tile left of that porch: entry + (1,0).
        var x = destination.GetProperty("front_door_tile_x").GetInt32();
        var y = destination.GetProperty("front_door_tile_y").GetInt32();
        Require(x < int.MaxValue && Value(settled, "player", "location_id").GetString() == "Farm" &&
            Value(settled, "player", "tile_x").GetInt32() == x + 1 && Value(settled, "player", "tile_y").GetInt32() == y,
            "Marriage deferred return differs from the original native wedding exit.");
    }

    private static bool SameNativeHome(JsonElement left, JsonElement right) =>
        new[] { "home_runtime_type", "home_is_cabin", "home_location_id", "destination_location_id", "front_door_tile_x", "front_door_tile_y" }
            .All(name => left.GetProperty(name).GetRawText() == right.GetProperty(name).GetRawText());
}
