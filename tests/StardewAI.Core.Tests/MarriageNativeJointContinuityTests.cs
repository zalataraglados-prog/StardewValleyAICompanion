using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageNativeJointContinuityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(214)]
    [InlineData(360)]
    public void BoundedReadOnlyControlSettlingDoesNotRequireIdenticalUpdateTicks(int ticks)
    {
        var left = Boundary();
        var right = Copy(left);
        right.GameTick += ticks;
        var original = JsonSerializer.Serialize(new[] { left, right });
        MarriageNativeJointContinuity.Validate(left, right);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { left, right }));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("player")]
    [InlineData("run")]
    [InlineData("isolation")]
    [InlineData("mode")]
    [InlineData("version")]
    [InlineData("bridge")]
    [InlineData("reverse_tick")]
    [InlineData("large_gap")]
    [InlineData("day")]
    [InlineData("native_date_only")]
    [InlineData("clock")]
    [InlineData("hash")]
    [InlineData("engagement")]
    [InlineData("house")]
    [InlineData("money")]
    [InlineData("energy")]
    [InlineData("health")]
    [InlineData("inventory")]
    [InlineData("current_tool")]
    [InlineData("location_id")]
    [InlineData("tile_x")]
    [InlineData("tile_y")]
    [InlineData("friendship")]
    [InlineData("unreadable_inventory")]
    public void HandoffsRejectUnrecordedProgressResourceMovementAndIdentityChanges(string change)
    {
        var left = Boundary();
        var right = change == "house" ? Boundary(level: 2) : Copy(left);
        switch (change)
        {
            case "save": right.SaveId.Value = "other"; break;
            case "player": right.PlayerId.Value = "other"; break;
            case "version": right.GameVersion = "1.6.14"; break;
            case "bridge": right.BridgeVersion = "other"; break;
            case "reverse_tick": right.GameTick--; break;
            case "large_gap": right.GameTick += MarriageNativeJointContinuity.MaximumPassiveGapTicks + 1; break;
            case "day": Set(right, "time", "total_days", 213); break;
            case "native_date_only": Set(right, "time", "day", 18); break;
            case "clock": Set(right, "time", "time", 1120); break;
            case "hash": right.StateHash = "stale"; break;
            case "run": Set(right, "environment", "training_run_id", "other"); break;
            case "isolation": Set(right, "environment", "save_isolation_path", "other"); break;
            case "mode": Set(right, "environment", "training_mode", "0"); break;
            case "engagement":
                Set(right, "player", "engaged", true);
                CurrentMarriageHouseStateTests.Mutate(right, "world_progress", node => node["marriage_house"]!["value"]!["engaged"] = true);
                break;
            case "friendship": Set(right, "npcs", "friendships", new[] { new { npc_name = "Abigail", points = 2501 } }); break;
            case "unreadable_inventory":
                CurrentMarriageHouseStateTests.Mutate(right, "player", node => node["inventory"]!["status"] = "unknown"); break;
            case "money":
                Set(right, "player", "money", 80001);
                CurrentMarriageHouseStateTests.Mutate(right, "world_progress", node => node["marriage_house"]!["value"]!["money"] = 80001);
                break;
            case "energy": case "health": case "tile_x": case "tile_y": Set(right, "player", change, 999); break;
            case "inventory": Set(right, "player", change, new[] { new { slot_index = 0, qualified_item_id = "(O)66", stack = 1 } }); break;
            case "current_tool": case "location_id": Set(right, "player", change, "other"); break;
        }
        Assert.Throws<InvalidDataException>(() => MarriageNativeJointContinuity.Validate(left, right));
    }

    [Fact]
    public void ExactDeferredNativeWeddingExitIsSeparateFromPassiveMovementPermission()
    {
        var (before, after, settled) = WeddingReturn();
        Assert.Throws<InvalidDataException>(() => MarriageNativeJointContinuity.Validate(after, settled));
        MarriageNativeJointContinuity.ValidateWeddingReturn(before, after, settled);
    }

    [Fact]
    public void UnavailableCurrentToolObservationIsRetainedWithoutInventingToolAbsence()
    {
        var left = Boundary();
        CurrentMarriageHouseStateTests.Mutate(left, "player", node => node["current_tool"]!["status"] = "unavailable");
        var right = Copy(left);
        MarriageNativeJointContinuity.Validate(left, right);
        Assert.Equal("unavailable", right.State["player"].GetProperty("current_tool").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, right.State["player"].GetProperty("current_tool").GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("not_warping")]
    [InlineData("still_warping")]
    [InlineData("immobile")]
    [InlineData("tile")]
    [InlineData("location")]
    [InlineData("money")]
    [InlineData("cabin")]
    [InlineData("home_changed")]
    public void WeddingReturnDoesNotAuthorizeArbitraryWarpOrProgress(string change)
    {
        var (before, after, settled) = WeddingReturn();
        if (change == "event") Set(before, "player", "story_event", new { active = true, event_up = true, event_id = "unrelated" });
        if (change == "not_warping") Set(after, "player", "movement_timing_context", new { is_warping = false, player_can_move_now = false });
        if (change == "still_warping") Set(settled, "player", "movement_timing_context", new { is_warping = true, player_can_move_now = true });
        if (change == "immobile") Set(settled, "player", "movement_timing_context", new { is_warping = false, player_can_move_now = false });
        if (change == "tile") Set(settled, "player", "tile_x", 64);
        if (change == "location") Set(settled, "player", "location_id", "FarmHouse");
        if (change == "money") Set(settled, "player", "money", 99999);
        if (change == "cabin") CurrentMarriageHouseStateTests.Mutate(after, "player", node => node["return_scepter"]!["value"]!["destination"]!["home_is_cabin"] = true);
        if (change == "home_changed") CurrentMarriageHouseStateTests.Mutate(settled, "player", node => node["return_scepter"]!["value"]!["destination"]!["front_door_tile_x"] = 63);
        Assert.Throws<InvalidDataException>(() => MarriageNativeJointContinuity.ValidateWeddingReturn(before, after, settled));
    }
}
