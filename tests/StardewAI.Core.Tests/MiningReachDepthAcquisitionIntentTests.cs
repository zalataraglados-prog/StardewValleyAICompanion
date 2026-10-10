using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class MiningReachDepthAcquisitionIntentTests
{
    [Fact]
    public void ExactMonsterSourcePreemptsOrdinaryStone()
    {
        var candidate = Assert.Single(
            MiningReachDepthCandidateBuilder.Build(
                Snapshot(),
                Parameters("monster:Green Slime")));

        Assert.True(candidate.Available);
        Assert.Equal("mining_reach_depth_plan_envelope", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "execution_option_id" &&
            parameter.Value == "executor.combat_monster");
        var sourceParameter = Assert.Single(candidate.Parameters.Where(
            parameter => parameter.Name ==
                "authoritative_route_sources_json"));
        using var document = JsonDocument.Parse(sourceParameter.Value);
        var source = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(
            "native_monster_drop_table",
            source.GetProperty("route_kind").GetString());
        Assert.Equal(
            "monster:Green Slime",
            source.GetProperty("source_id").GetString());
        Assert.Equal(
            "(O)766",
            source.GetProperty("qualified_item_id").GetString());
    }

    [Fact]
    public void MissingExactMonsterSourceFailsClosed()
    {
        var candidate = Assert.Single(
            MiningReachDepthCandidateBuilder.Build(
                Snapshot(),
                Parameters("monster:Bat")));

        Assert.False(candidate.Available);
        Assert.Contains(
            "no_reachable_monster_with_possible_target_drop",
            candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "authoritative_route_sources_json" &&
            parameter.Value == "[]");
    }

    private static SmallModelActionParameter[] Parameters(string sourceId) =>
        new[]
        {
            Parameter("target_depth", "46"),
            Parameter("target_location_family", "ordinary_mines"),
            Parameter(
                "acquisition_target_route_kind",
                "native_monster_drop_table"),
            Parameter("acquisition_target_source_id", sourceId),
            Parameter("acquisition_target_qualified_item_id", "(O)766")
        };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            """
            {
              "mining": {
                "current_mine": {"value":{"location_id":"UndergroundMine45","mine_level":45,"mine_kind":"ordinary_mines"},"status":"available"},
                "tiles": {"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":6,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100001","100001","111111"]},"exits":[],"ladders":[],"shafts":[],"elevators":[],"staircase_placement":{}},"status":"available"},
                "objects": {"value":[{"tile_x":3,"tile_y":2,"qualified_item_id":"(O)32","is_breakable_stone":true,"best_pickaxe_hits_remaining":2}],"status":"available"},
                "resource_clumps": {"value":[],"status":"available"},
                "monsters": {"value":[{"runtime_identity":"slime-1","runtime_type":"StardewValley.Monsters.GreenSlime","name":"Green Slime","tile_x":4,"tile_y":2,"health":20,"possible_drop_qualified_item_ids":["(O)766"],"authoritative_route_sources":[{"route_kind":"native_monster_drop_table","source_id":"monster:Green Slime","qualified_item_id":"(O)766"}],"melee_attack_projections":[{"slot_index":1,"expected_attacks_to_defeat":2.0,"expected_active_damage_duration_ms":600.0,"terminal_effect":"defeat"}]}],"status":"available"},
                "floor_objectives": {"value":{"must_kill_all_monsters_to_advance":false},"status":"available"},
                "reward_chests": {"value":[],"status":"available"},
                "player_resources": {"value":{"health":100,"max_health":100,"energy":220,"max_energy":270,"current_time":1200,"deepest_mine_level":45,"selected_slot_index":1,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}},"status":"available"},
                "completeness": {"value":{"status":"complete"},"status":"available"}
              }
            }
            """)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-11T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
