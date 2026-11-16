using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("valid_epoch_refresh")]
    [InlineData("owner")]
    [InlineData("actor")]
    [InlineData("world")]
    [InlineData("epoch")]
    [InlineData("policy")]
    [InlineData("manual_pause")]
    [InlineData("position")]
    [InlineData("currency")]
    [InlineData("clock")]
    [InlineData("control")]
    [InlineData("unknown_state")]
    public async Task SameLiveEntryReusesOwnedPauseWitnessWithoutHidingWorldChanges(string fault)
    {
        using var f = new SavedReadbackOwnerFixture();
        await using var owner = await f.Night.Open();
        await f.CompleteNight(owner, loaded =>
        {
            SavedReadbackOwnerFixture.RefreshDeclaredLoadedFieldTicks(loaded);
            ChangeState(loaded, "player", player => player["movement_timing_context"] = JsonSerializer.SerializeToNode(new
            {
                status = "available", read_at_tick = loaded.GameTick,
                value = new
                {
                    projection_status = "exact_current_player_native_cardinal_movement_context",
                    player_id = loaded.PlayerId.Value, game_unique_id = "456", executor_owned_idle_pause = (object?)null,
                    game_paused = false, native_control_gate_open = true, route_timing_ready_now = true,
                    player_can_move_now = true, player_using_tool_now = false, player_freeze_pause = 0,
                    dialogue_up = false, global_fade = false, freeze_controls = false, is_warping = false,
                    current_event_active = false, event_up = false, immobilizing_buff_19_active = false
                }
            }));
            if (fault == "valid_epoch_refresh") SetPause(loaded, 7);
        });
        await f.Run(owner, await f.Admit(owner));
        var accepted = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var live = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(accepted, JsonOptions), JsonOptions)!;
        SetPause(live, 9);
        ChangeState(live, "player", player =>
        {
            var timing = player["movement_timing_context"]!["value"]!;
            var pause = timing["executor_owned_idle_pause"]!;
            if (fault == "owner") pause["owner_mod_id"] = "Another.Mod";
            if (fault == "actor") pause["player_id"] = "another-actor";
            if (fault == "world") pause["game_unique_id"] = "another-world";
            if (fault == "epoch") pause["pause_epoch"] = 0;
            if (fault == "policy") pause["release_policy"] = "manual_resume";
            if (fault == "manual_pause") timing["executor_owned_idle_pause"] = null;
            if (fault == "position") player["tile_x"]!["value"] = player["tile_x"]!["value"]!.GetValue<int>() + 1;
            if (fault == "currency") player["money"]!["value"] = 1;
            if (fault == "control") timing["player_can_move_now"] = false;
            if (fault == "unknown_state") timing["unrecognized_control_change"] = true;
        });
        if (fault == "clock") ChangeState(live, "time", time => time["time"]!["value"] = 610);
        var process = f.Witness.Observation.Process;
        var started = DateTimeOffset.Parse(process.StartedAtUtc);
        var original = File.ReadAllBytes(owner.CurrentSnapshotPath);
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        var initialStart = DateTimeOffset.Parse(LifecycleRead<SnapshotEnvelope>(f.Night.Options.InitialSnapshotPath).RealTimestamp).AddSeconds(-1);
        Assert.Throws<InvalidDataException>(() => ReadLiveMetadata(owner, live, initialStart));
        if (fault.StartsWith("valid", StringComparison.Ordinal))
        {
            var source = ReadLiveProcessMetadata(owner, live, process.ProcessId, process.ExecutablePath, started);
            Assert.Equal("executor_owned_idle_pause_refresh", ContinuationProperty<string>(source, "LiveWorldMatchKind"));
            Assert.Equal(accepted.StateHash, ContinuationProperty<string>(source, "AcceptedStateHash"));
        }
        else Assert.Throws<InvalidDataException>(() => ReadLiveProcessMetadata(owner, live, process.ProcessId, process.ExecutablePath, started));
        Assert.Equal(original, File.ReadAllBytes(owner.CurrentSnapshotPath));
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);

        static void SetPause(SnapshotEnvelope snapshot, int epoch) => ChangeState(snapshot, "player", player =>
        {
            var timing = player["movement_timing_context"]!["value"]!;
            timing["executor_owned_idle_pause"] = JsonSerializer.SerializeToNode(new
            {
                schema_version = "executor_owned_idle_pause.v1", owner_mod_id = "StardewAI.RuntimeTestHarness", applied = true,
                release_policy = "release_before_pending_execution_update", player_id = snapshot.PlayerId.Value,
                game_unique_id = "456", pause_epoch = epoch
            });
            timing["game_paused"] = true; timing["native_control_gate_open"] = false; timing["route_timing_ready_now"] = false;
        });
    }
}
