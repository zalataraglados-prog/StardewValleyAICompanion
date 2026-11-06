using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageNativeJointContinuityTests
{
    private static SnapshotEnvelope Boundary(bool married = false, int level = 3)
    {
        var snapshot = CurrentMarriageHouseStateTests.Snapshot(married, level);
        snapshot.BridgeVersion = "fixture-bridge";
        snapshot.SaveId.Status = snapshot.PlayerId.Status = FieldStatus.Available;
        snapshot.SaveId.Value = "save"; snapshot.PlayerId.Value = "player";
        snapshot.State["environment"] = JsonSerializer.SerializeToElement(new
        {
            training_run_id = CurrentMarriageHouseStateTests.Field("run"),
            save_isolation_path = CurrentMarriageHouseStateTests.Field("isolated-saves"),
            training_mode = CurrentMarriageHouseStateTests.Field("1")
        });
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(new
            { friendships = CurrentMarriageHouseStateTests.Field(new[] { new { npc_name = "Abigail", points = 2500 } }) });
        Set(snapshot, "time", "total_days", 212);
        foreach (var name in new[] { "health", "energy" }) Set(snapshot, "player", name, 100);
        Set(snapshot, "player", "inventory", Array.Empty<object>());
        Set(snapshot, "player", "current_tool", null);
        Set(snapshot, "player", "location_id", "Town");
        Set(snapshot, "player", "tile_x", 27); Set(snapshot, "player", "tile_y", 63);
        Set(snapshot, "player", "story_event", new { active = true, event_up = true, event_id = "-2" });
        Set(snapshot, "player", "movement_timing_context", new { is_warping = false, player_can_move_now = true });
        Set(snapshot, "player", "return_scepter", new
        {
            projection_status = "complete_current_native_return_scepter_context",
            destination = new { home_runtime_type = "FarmHouse", home_is_cabin = false, home_location_id = "FarmHouse",
                destination_location_id = "Farm", front_door_tile_x = 64, front_door_tile_y = 15 }
        });
        return snapshot;
    }

    private static (SnapshotEnvelope Before, SnapshotEnvelope After, SnapshotEnvelope Settled) WeddingReturn()
    {
        var before = Boundary(married: true);
        var after = Copy(before);
        Set(after, "player", "story_event", new { active = false, event_up = false, event_id = "" });
        Set(after, "player", "movement_timing_context", new { is_warping = true, player_can_move_now = false });
        var settled = Copy(after); settled.GameTick += 44;
        Set(settled, "player", "location_id", "Farm");
        Set(settled, "player", "tile_x", 65); Set(settled, "player", "tile_y", 15);
        Set(settled, "player", "movement_timing_context", new { is_warping = false, player_can_move_now = true });
        return (before, after, settled);
    }

    private static SnapshotEnvelope Copy(SnapshotEnvelope snapshot) =>
        JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(snapshot))!;

    private static void Set(SnapshotEnvelope snapshot, string domain, string name, object? value) =>
        CurrentMarriageHouseStateTests.Mutate(snapshot, domain, node => node[name] =
            JsonSerializer.SerializeToNode(CurrentMarriageHouseStateTests.Field(value)));
}
