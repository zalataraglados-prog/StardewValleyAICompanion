using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class MiningFloorStepPlannerTests
{
    [Fact]
    public void ReachableLadderAlwaysPrecedesMiningAndCombat()
    {
        var plan = Plan(
            ladders: "[{\"tile_x\":4,\"tile_y\":2}]",
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":true}}]",
            monsters: "[{\"tile_x\":3,\"tile_y\":3}]");

        Assert.Equal("ready", plan.Status);
        Assert.Equal(MiningFloorStepKinds.DescendLadder, plan.StepKind);
        Assert.Equal(4, plan.TargetTileX);
        Assert.Equal("reachable_ladder_available", plan.Reason);
    }

    [Fact]
    public void KillAllFloorSelectsReachableMonsterBeforeStone()
    {
        var plan = Plan(
            objects: "[{\"tile_x\":2,\"tile_y\":2,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":true}}]",
            monsters: "[{\"tile_x\":4,\"tile_y\":2}]",
            mustKillAll: true);

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal(4, plan.TargetTileX);
        Assert.Equal("kill_all_floor_requires_combat", plan.Reason);
    }

    [Fact]
    public void DeterministicLadderStonePrecedesCloserOrdinaryStone()
    {
        var plan = Plan(objects: """
        [
          {"tile_x":2,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"ladder_preview":{"creates_ladder":false}},
          {"tile_x":5,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":3,"ladder_preview":{"creates_ladder":true}}
        ]
        """);

        Assert.Equal(MiningFloorStepKinds.MineStone, plan.StepKind);
        Assert.Equal(5, plan.TargetTileX);
        Assert.True(plan.DeterministicLadderAfterBreak);
        Assert.Equal(3, plan.EstimatedToolSwings);
    }

    [Fact]
    public void OrdinaryStoneUsesMovementPlusActualSwingCost()
    {
        var plan = Plan(objects: """
        [
          {"tile_x":2,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":8,"ladder_preview":{"creates_ladder":false}},
          {"tile_x":5,"tile_y":2,"is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"ladder_preview":{"creates_ladder":false}}
        ]
        """);

        Assert.Equal(5, plan.TargetTileX);
        Assert.Equal(1, plan.EstimatedToolSwings);
        Assert.Equal("lowest_reachable_movement_and_swing_cost", plan.Reason);
        Assert.Equal(plan.EstimatedMovementTiles + 1, plan.Path.Length);
    }

    [Fact]
    public void NoReachableStoneFallsBackToCombatWithoutDangerPenalty()
    {
        var plan = Plan(
            rows: new[] { "111111", "100111", "100011", "100101", "111111" },
            objects: "[{\"tile_x\":4,\"tile_y\":1,\"is_breakable_stone\":true,\"best_pickaxe_hits_remaining\":1,\"ladder_preview\":{\"creates_ladder\":false}}]",
            monsters: "[{\"tile_x\":3,\"tile_y\":3}]");

        Assert.Equal(MiningFloorStepKinds.CombatMonster, plan.StepKind);
        Assert.Equal("no_reachable_stone_clear_dynamic_monster", plan.Reason);
    }

    [Fact]
    public void MissingCollisionFailsClosed()
    {
        var snapshot = Snapshot("""
        {"mining":{"tiles":{"status":"unavailable","value":null},"objects":{"status":"available","value":[]},"monsters":{"status":"available","value":[]},"floor_objectives":{"status":"available","value":{}}}}
        """);

        var plan = new MiningFloorStepPlanner().Plan(snapshot);

        Assert.Equal("blocked", plan.Status);
        Assert.Equal("mining_required_group_unavailable", plan.Reason);
    }

    [Fact]
    public void RuntimeMineStoneUsesNativeToolLifecycleWithoutDirectToolFunction()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var start = source.IndexOf("private void StartMineStone", StringComparison.Ordinal);
        var end = source.IndexOf("private void StartSetupMiningFloor", start, StringComparison.Ordinal);
        var mineStoneSource = source[start..end];

        Assert.Contains("executor.mine_stone", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.BeginUsingTool()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("Game1.player.EndUsingTool()", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("RecordMineStoneCompletedSwing(active, 0);", mineStoneSource, StringComparison.Ordinal);
        Assert.Contains("native_pickaxe_lifecycle_removed_breakable_stone", mineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".DoFunction(", mineStoneSource, StringComparison.Ordinal);
        Assert.DoesNotContain("objects.Remove", mineStoneSource, StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "Invoke-RuntimeMiningSnapshotSmoke.ps1"));
        Assert.Contains("[switch] $MineOneStone", smoke, StringComparison.Ordinal);
        Assert.Contains("option_id = \"executor.mine_stone\"", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_native_swing_count", smoke, StringComparison.Ordinal);
        Assert.Contains("terminal zero-health observation", smoke, StringComparison.Ordinal);
        Assert.Contains("mine_stone_removed", smoke, StringComparison.Ordinal);
    }

    private static MiningFloorStepPlan Plan(
        string ladders = "[]",
        string objects = "[]",
        string monsters = "[]",
        bool mustKillAll = false,
        string[]? rows = null)
    {
        rows ??= new[] { "111111", "100001", "100001", "100001", "111111" };
        var rowsJson = JsonSerializer.Serialize(rows);
        var json = """
        {
          "mining": {
            "tiles": {"status":"available","value":{"player_tile":{"tile_x":1,"tile_y":2},"ladders":LADDERS,"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":ROWS}}},
            "objects": {"status":"available","value":OBJECTS},
            "monsters": {"status":"available","value":MONSTERS},
            "floor_objectives": {"status":"available","value":{"must_kill_all_monsters_to_advance":MUST_KILL_ALL}}
          }
        }
        """
            .Replace("LADDERS", ladders, StringComparison.Ordinal)
            .Replace("ROWS", rowsJson, StringComparison.Ordinal)
            .Replace("OBJECTS", objects, StringComparison.Ordinal)
            .Replace("MONSTERS", monsters, StringComparison.Ordinal)
            .Replace("MUST_KILL_ALL", mustKillAll.ToString().ToLowerInvariant(), StringComparison.Ordinal);
        return new MiningFloorStepPlanner().Plan(Snapshot(json));
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = "test",
            GameTick = 1,
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
