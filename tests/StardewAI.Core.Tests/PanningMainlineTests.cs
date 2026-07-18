using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PanningMainlineTests
{
    private const string OutputHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ExactPanningProjectionFlowsThroughNativeQueue()
    {
        var snapshot = Snapshot(StateJson(miningDelta: 11));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "foraging.pan_ore_spot" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("pan_ore_spot", candidate.Kind);
        Assert.Equal(5, candidate.Quantity);
        Assert.Contains(candidate.Parameters, value => value.Name == "expected_mining_experience_delta" && value.Value == "11");
        Assert.Contains(candidate.Parameters, value => value.Name == "expected_foraging_experience_delta" && value.Value == "7");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("pan_ore_spot", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.pan_ore_spot", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("pan_ore_spot", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsRewardOrExperienceDrift()
    {
        var initial = Snapshot(StateJson(miningDelta: 11));
        var candidate = Assert.Single(new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "foraging.pan_ore_spot" }, true)));
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, initial.StateHash);
        var drifted = Snapshot(StateJson(miningDelta: 12));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("pan_ore_spot_side_effect_projection_drifted", queue.Items.Single().BlockingReasons);
    }

    private static string StateJson(int miningDelta)
    {
        var outputs = JsonSerializer.Serialize(new[]
        {
            new { RuntimeType = "StardewValley.Object", QualifiedItemId = "(O)380", Quality = 0, UnitStateSha256 = OutputHash, Quantity = 5 }
        });
        return """
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "panning":{"value":{"status":"exact","location_id":"Farm","ore_pan_point_active":true,"ore_pan_point_x":12,"ore_pan_point_y":10,
              "pan_tool_slot_index":4,"pan_runtime_type":"StardewValley.Tools.Pan","pan_upgrade_level":1,"pan_enchantments_json":"[]",
              "click_pixel_x":800,"click_pixel_y":672,"times_panned_before":3,"times_panned_after":4,
              "mining_experience_before":1000,"mining_experience_delta":MINING_DELTA,"mining_experience_after":MINING_AFTER,
              "foraging_experience_before":2000,"foraging_experience_delta":7,"foraging_experience_after":2007,
              "inventory_accepts_all_outputs":true,"expected_output_items_json":OUTPUTS,
              "expected_receipt_stat_increments_json":"[]","native_receipt_callbacks_status":"runtime_observed:native_item_receive_callbacks",
              "post_use_ore_pan_point_status":"cleared","post_use_respawn_attempts":0},
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map":{"value":{"location_id":"Farm","width":100,"height":100},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MINING_DELTA", miningDelta.ToString())
        .Replace("MINING_AFTER", (1000 + miningDelta).ToString())
        .Replace("OUTPUTS", JsonSerializer.Serialize(outputs));
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
