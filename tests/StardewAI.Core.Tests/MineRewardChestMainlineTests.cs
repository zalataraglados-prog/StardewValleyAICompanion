using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MineRewardChestMainlineTests
{
    [Fact]
    public void ExactMineRewardChestFlowsToSingleNativeOpenExecutor()
    {
        var snapshot = Snapshot(StateJson("ready", false));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "mining.claim_reward_chests" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_mine_reward_chest", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_skill_experience_delta" && parameter.Value == "0");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("claim_mine_reward_chest", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.claim_mine_reward_chest", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("claim_mine_reward_chest", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsConsumedOrDriftedChest()
    {
        var initial = Snapshot(StateJson("ready", false));
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "mining.claim_reward_chests" }, true));
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var drifted = Snapshot(StateJson("blocked_inventory_cannot_accept_exact_reward", false));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("mine_reward_chest_not_ready_by_transparent_state", queue.Items.Single().BlockingReasons);
    }

    [Fact]
    public void ReachDepthClaimsLoadedRewardBeforeLeavingFloor()
    {
        var snapshot = Snapshot(StateJson("ready", false));

        var step = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
        {
            Kind = MiningObjectiveKinds.ReachDepth,
            TargetDepth = 20
        });

        Assert.Equal(MiningFloorStepKinds.ClaimRewardChest, step.StepKind);
        Assert.Equal("(W)11", step.TargetQualifiedItemId);
        Assert.Equal("ordinary_fixed_reward", step.RewardBranch);
        Assert.Equal("executor.claim_mine_reward_chest", MiningFloorStepCompiler.ExecutionOptionId(step));
    }

    [Fact]
    public void SkullKeyTraversalClaimsIntermediateLoadedReward()
    {
        var snapshot = Snapshot(StateJson("ready", false));

        var step = new MiningFloorStepPlanner().Plan(snapshot, new MiningFloorObjective
        {
            Kind = MiningObjectiveKinds.AcquireSkullKey,
            TargetDepth = 120
        });

        Assert.Equal(MiningFloorStepKinds.ClaimRewardChest, step.StepKind);
        Assert.Equal("ordinary_fixed_reward", step.RewardBranch);
    }

    private static string StateJson(string status, bool skullKey)
    {
        return $$$"""
        {
          "player": {
            "location_id":{"value":"UndergroundMine20","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills_detail":{"value":{"luck":{"experience":0}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "mining":{
            "current_mine":{"value":{"location_id":"UndergroundMine20","mine_level":20,"mine_kind":"ordinary_mines"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tiles":{"value":{"player_tile":{"tile_x":1,"tile_y":2},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100101","100001","111111"]},"exits":[],"ladders":[],"shafts":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "objects":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "resource_clumps":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "monsters":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "floor_objectives":{"value":{"must_kill_all_monsters_to_advance":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_resources":{"value":{"health":100,"max_health":100,"energy":200,"max_energy":270,"current_time":1000,"selected_slot_index":0,"inventory_capacity":{"empty_slots":1}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "reward_chests":{"value":[{"tile_x":3,"tile_y":2,"runtime_type":"StardewValley.Objects.Chest","mine_level":20,"mine_kind":"ordinary_mines","reward_branch":"ordinary_fixed_reward","status":"{{{status}}}","contains_skull_key":{{{skullKey.ToString().ToLowerInvariant()}}},"is_stardrop":false,
              "item":{"runtime_type":"StardewValley.Tools.MeleeWeapon","item_id":"11","qualified_item_id":"(W)11","quantity":1,"quality":0,"inventory_accepts":true},
              "expected_output_items_json":"[{\"runtimeType\":\"StardewValley.Tools.MeleeWeapon\",\"qualifiedItemId\":\"(W)11\",\"quality\":0,\"unitStateSha256\":\"test\",\"quantity\":1}]",
              "native_gain_experience_call_amount":45,"expected_luck_experience_delta":0,"expected_stardrop_max_stamina_delta":0,"native_contract":"one_reward_open_then_wait_dumpContents_then_empty_chest_cleanup_checkAction"}],
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"UndergroundMine20","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
