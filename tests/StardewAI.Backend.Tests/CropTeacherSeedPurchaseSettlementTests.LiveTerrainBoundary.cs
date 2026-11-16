using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("position")]
    [InlineData("money")]
    [InlineData("machine")]
    [InlineData("clock")]
    [InlineData("buff")]
    [InlineData("scalar")]
    [InlineData("positive_speed")]
    [InlineData("unknown_control")]
    [InlineData("status")]
    [InlineData("forged_hash")]
    public async Task SameLiveNativeTerrainRefreshKeepsOriginalOwnerAndAllOtherStateStrict(string fault)
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
                    riding_horse_now = false, base_speed_now = 5f, added_speed_now = 0f,
                    temporary_speed_buff_now = 0f, current_native_on_foot_speed_scalar = 5f
                }
            }));
        });
        await f.Run(owner, await f.Admit(owner));
        var accepted = LifecycleRead<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var live = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(accepted, JsonOptions), JsonOptions)!;
        ChangeState(live, "player", player =>
        {
            var movement = player["movement_timing_context"]!["value"]!;
            movement["temporary_speed_buff_now"] = -1f;
            movement["current_native_on_foot_speed_scalar"] = 4f;
            if (fault == "position") player["tile_x"]!["value"] = player["tile_x"]!["value"]!.GetValue<int>() + 1;
            if (fault == "money") player["money"]!["value"] = 1;
            if (fault == "buff") { movement["added_speed_now"] = 1f; movement["current_native_on_foot_speed_scalar"] = 5f; }
            if (fault == "scalar") movement["current_native_on_foot_speed_scalar"] = 3f;
            if (fault == "positive_speed") { movement["temporary_speed_buff_now"] = 1f; movement["current_native_on_foot_speed_scalar"] = 6f; }
            if (fault == "unknown_control") movement["unrecognized_control_change"] = true;
            if (fault == "status") player["movement_timing_context"]!["status"] = "unavailable";
        });
        if (fault == "machine") ChangeState(live, "farm", farm => farm["unexpected_machine_state"] = true);
        if (fault == "clock") ChangeState(live, "time", time => time["time"]!["value"] = 610);
        if (fault == "forged_hash") live.StateHash = accepted.StateHash;
        var process = f.Witness.Observation.Process;
        var started = DateTimeOffset.Parse(process.StartedAtUtc);
        var acceptedBytes = File.ReadAllBytes(owner.CurrentSnapshotPath);
        var commits = f.Night.Shared.Runtime.Handler.Commits;
        var initialStart = DateTimeOffset.Parse(LifecycleRead<SnapshotEnvelope>(f.Night.Options.InitialSnapshotPath).RealTimestamp).AddSeconds(-1);
        Assert.Throws<InvalidDataException>(() => ReadLiveMetadata(owner, live, initialStart));
        if (fault == "valid")
        {
            var source = ReadLiveProcessMetadata(owner, live, process.ProcessId, process.ExecutablePath, started);
            Assert.Equal("native_terrain_speed_refresh", ContinuationProperty<string>(source, "LiveWorldMatchKind"));
            Assert.Equal(accepted.StateHash, ContinuationProperty<string>(source, "AcceptedStateHash"));
            Assert.Equal(owner.CurrentSnapshotPath, ContinuationProperty<string>(source, "SnapshotPath"));
            Assert.NotEqual(accepted.StateHash, live.StateHash);
        }
        else Assert.Throws<InvalidDataException>(() => ReadLiveProcessMetadata(owner, live, process.ProcessId, process.ExecutablePath, started));
        Assert.Equal(acceptedBytes, File.ReadAllBytes(owner.CurrentSnapshotPath));
        Assert.Equal(commits, f.Night.Shared.Runtime.Handler.Commits);
        Assert.Null(owner.Pending);
    }
}
