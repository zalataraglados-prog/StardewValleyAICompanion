using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MiningBuriedItemRouteTests
{
    [Fact]
    public void ReachDepthKeepsOrdinaryStepSeparateFromBuriedItemAttempt()
    {
        var snapshot = Snapshot(hasHoe: true, isQuarryMine: false);
        var availability = Availability(snapshot);
        var option = Assert.Single(availability.Options);

        Assert.True(option.Available, string.Join(";", option.BlockingReasons));
        var ordinary = Assert.Single(option.EventCandidates.Where(candidate =>
            candidate.Kind == "mining_reach_depth_plan_envelope"));
        var buried = Assert.Single(option.EventCandidates.Where(candidate =>
            candidate.Kind == "mining_buried_item_plan_envelope"));

        Assert.Contains(ordinary.Parameters, parameter =>
            parameter.Name == "execution_option_id" &&
            parameter.Value == "executor.mine_stone");
        Assert.Equal("(O)585", buried.QualifiedItemId);
        Assert.Equal(2, buried.TileX);
        Assert.Equal(2, buried.TileY);
        Assert.Contains(buried.Parameters, parameter =>
            parameter.Name == "execution_option_id" &&
            parameter.Value == "executor.till_soil");
        Assert.Contains(buried.Parameters, parameter =>
            parameter.Name == "mining_step_kind" &&
            parameter.Value == MiningFloorStepKinds.DigBuriedItem);
        var sourceJson = Assert.Single(buried.Parameters.Where(parameter =>
            parameter.Name == "authoritative_route_sources_json")).Value;
        using var sourceDocument = JsonDocument.Parse(sourceJson);
        var source = Assert.Single(
            sourceDocument.RootElement.EnumerateArray());
        Assert.Equal(
            "native_mine_buried_item",
            source.GetProperty("route_kind").GetString());
        Assert.Equal(
            "MineShaft.checkForBuriedItem",
            source.GetProperty("source_id").GetString());
    }

    [Fact]
    public void BuriedItemAttemptRequiresHoeAndNonQuarryMine()
    {
        var noHoe = Assert.Single(Availability(
            Snapshot(hasHoe: false, isQuarryMine: false)).Options);
        Assert.DoesNotContain(noHoe.EventCandidates, candidate =>
            candidate.Kind == "mining_buried_item_plan_envelope");

        var quarry = Assert.Single(Availability(
            Snapshot(hasHoe: true, isQuarryMine: true)).Options);
        Assert.DoesNotContain(quarry.EventCandidates, candidate =>
            candidate.Kind == "mining_buried_item_plan_envelope");
    }

    [Fact]
    public void SelectedBuriedItemCandidateCompilesToOneNativeHoeCycle()
    {
        var snapshot = Snapshot(hasHoe: true, isQuarryMine: false);
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            Availability(snapshot));
        var selected = Assert.Single(ranked.Where(candidate =>
            candidate.Kind == "mining_buried_item_plan_envelope"));
        var plan = new DailyPlanCompiler().Compile(
            new[] { selected },
            snapshot.StateHash,
            maxCandidates: 1);

        var step = Assert.Single(plan.Steps);
        Assert.Equal("till_soil", step.Kind);
        Assert.Equal("UndergroundMine", step.TargetLocation);
        Assert.Equal(2, step.TargetTileX);
        Assert.Equal(2, step.TargetTileY);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.till_soil", item.OptionId);
        var primitive = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("till_soil", primitive.StepType);
        Assert.Contains(
            "MineShaft.checkForBuriedItem_invoked=true",
            primitive.ExpectedEffect,
            StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Availability(
        SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "mining.reach_depth",
                    Parameters = new[]
                    {
                        Parameter("target_depth", "45"),
                        Parameter(
                            "target_location_family",
                            "ordinary_mines")
                    }
                }
            });

    private static SnapshotEnvelope Snapshot(
        bool hasHoe,
        bool isQuarryMine)
    {
        var stateJson = """
        {
          "player": {
            "location_id": {"value":"UndergroundMine","status":"available"},
            "tile_x": {"value":1,"status":"available"},
            "tile_y": {"value":2,"status":"available"},
            "energy": {"value":220,"status":"available"},
            "inventory": {"value":[],"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available"}
          },
          "current_location": {
            "map": {"value":{"location_id":"UndergroundMine","width":6,"height":5},"status":"available"}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"UndergroundMine","width":6,"height":5,"notable_tiles":[]},"status":"available"}
          },
          "mining": {
            "current_mine": {"value":{"location_id":"UndergroundMine","mine_level":40,"mine_area":40,"mine_kind":"ordinary_mines","is_loaded_current_location":true,"is_skull_cavern":false,"is_quarry_mine":IS_QUARRY_MINE,"is_dangerous":false,"additional_difficulty":0},"status":"available"},
            "tiles": {"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":6,"height":5,"status":"loaded_field_only"},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100001","100001","111111"],"buried_item_diggable_encoding":"row_major_strings_1_native_hoe_hook_eligible_0_ineligible","buried_item_diggable_rows":["000000","000000","001000","000000","000000"],"buried_item_target_qualified_item_id":"(O)585","buried_item_target_probability_per_hoe_cycle":0.001575,"buried_item_probability_status":"exact_native_branch_probability_unrealized_global_rng","buried_item_rng_contract":"Game1.random_not_read_or_replayed_by_transparent_bridge","buried_item_authoritative_route_sources":[{"route_kind":"native_mine_buried_item","source_id":"MineShaft.checkForBuriedItem","qualified_item_id":"(O)585"}]},"exits":[{"tile_x":4,"tile_y":2,"tile_index":115,"expected_destination":{"location_id":"Mine","tile_x":23,"tile_y":8}}],"ladders":[],"shafts":[],"elevators":[]},"status":"available"},
            "objects": {"value":[{"tile_x":3,"tile_y":2,"qualified_item_id":"(O)32","is_breakable_stone":true,"best_pickaxe_hits_remaining":2}],"status":"available"},
            "resource_clumps": {"value":[],"status":"available"},
            "monsters": {"value":[],"status":"available"},
            "debris": {"value":[],"status":"available"},
            "floor_objectives": {"value":{"must_kill_all_monsters_to_advance":false,"enemy_count":0,"ladder_has_spawned":false},"status":"available"},
            "reward_chests": {"value":[],"status":"available"},
            "player_resources": {"value":{"health":100,"max_health":100,"energy":220,"max_energy":270,"current_time":1200,"deepest_mine_level":120,"selected_slot_index":1,"hoe_slots":HOE_SLOTS,"pickaxe_slots":[{"slot_index":1}],"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0,"status":"exact_mine_cardinal_input_without_collision_delay"}},"status":"available"},
            "completeness": {"value":{"status":"complete","unavailable_reasons":[]},"status":"available"}
          }
        }
        """;
        stateJson = stateJson
            .Replace(
                "IS_QUARRY_MINE",
                isQuarryMine.ToString().ToLowerInvariant(),
                StringComparison.Ordinal)
            .Replace(
                "HOE_SLOTS",
                hasHoe
                    ? """[{"slot_index":2,"qualified_item_id":"(T)Hoe"}]"""
                    : "[]",
                StringComparison.Ordinal);
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(stateJson)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
