using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FishPondMainlineTests
{
    private const string OutputHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [Fact]
    public void ReadyOutputFlowsThroughNativeQueue()
    {
        var snapshot = Snapshot(StateJson(outputReady: true, requestMaxAfter: 3));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "fishing.service_fish_ponds" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates.Where(row => row.Kind == "collect_fish_pond_output"));

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("(O)812", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, value => value.Name == "expected_skill_experience_delta" && value.Value == "11");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.Kind == "collect_fish_pond_output")
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("collect_fish_pond_output", Assert.Single(plan.Steps).Kind);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.collect_fish_pond_output", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("collect_fish_pond_output", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void ReadyRequestFlowsThroughNativeQueueAndRejectsGateDrift()
    {
        var initial = Snapshot(StateJson(outputReady: false, requestMaxAfter: 3));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(initial, new[] { "fishing.service_fish_ponds" }, true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates.Where(row => row.Kind == "complete_fish_pond_request"));
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, value => value.Name == "request_item_toolbar_slots_json");
        Assert.Contains(candidate.Parameters, value => value.Name == "expected_skill_experience_delta" && value.Value == "40");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability)
            .Where(row => row.Kind == "complete_fish_pond_request")
            .ToArray();
        var plan = new DailyPlanCompiler().Compile(ranked, initial.StateHash);
        var validQueue = new ActionQueueCompiler().Compile(plan, initial);
        Assert.Equal("pending", validQueue.Status);
        Assert.Equal("executor.complete_fish_pond_request", Assert.Single(validQueue.Items).OptionId);

        var drifted = Snapshot(StateJson(outputReady: false, requestMaxAfter: 5));
        plan.StateHash = drifted.StateHash;
        var blocked = new ActionQueueCompiler().Compile(plan, drifted);
        Assert.Equal("blocked", blocked.Status);
        Assert.Contains("fish_pond_request_projection_drifted", blocked.Items.Single().BlockingReasons);
    }

    [Fact]
    public void FarmReadProjectionDoesNotInvokeMutatingPondCacheAccessors()
    {
        var source = FarmReadAdapterSources.All;
        Assert.Contains("FishPond.GetRawData", source);
        Assert.DoesNotContain("pond.GetFishPondData()", source);
        Assert.DoesNotContain("pond.HasUnresolvedNeeds()", source);
        Assert.Contains("Game1.random = liveRandom", source);
    }

    private static string StateJson(bool outputReady, int requestMaxAfter)
    {
        var outputs = JsonSerializer.Serialize(new[]
        {
            new { RuntimeType = "StardewValley.Object", QualifiedItemId = "(O)812", Quality = 0, UnitStateSha256 = OutputHash, Quantity = 1 }
        });
        var slots = JsonSerializer.Serialize(new[]
        {
            new { slot_index = 0, stack = 1, runtime_type = "StardewValley.Object", qualified_item_id = "(O)72" }
        });
        return """
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"qualified_item_id":"(O)72","stack":1,"runtime_type":"StardewValley.Object","is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "safe_item_context":{"value":{"safe_slot_available":true,"safe_slot_index":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "farm_identity":{"value":{"location_id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "buildings":{"value":[{"type":"Fish Pond","runtime_type":"StardewValley.Buildings.FishPond","tile_x":10,"tile_y":10,"fish_pond":{
              "status":"exact","runtime_type":"StardewValley.Buildings.FishPond","fish_type_item_id":"698","fish_count":1,"maximum_occupants":1,
              "last_unlocked_population_gate":0,"days_since_spawn":5,"preferred_target_tile_x":10,"preferred_target_tile_y":10,"preferred_stand_tile_x":9,"preferred_stand_tile_y":10,
              "output_status":"OUTPUT_STATUS","output_runtime_type":"StardewValley.Object","output_qualified_item_id":"(O)812","output_quality":0,"output_stack":1,
              "output_unit_state_sha256":"OUTPUT_HASH","output_items_json":OUTPUTS,"output_state_context":"post_inventory_receive","output_safe_slot_index":1,
              "output_fishing_experience_delta":11,"output_receipt_callbacks_status":"runtime_observed",
              "request_status":"REQUEST_STATUS","request_unresolved":true,"request_item_runtime_type":"StardewValley.Object","request_item_qualified_item_id":"(O)72",
              "request_item_count_remaining":1,"request_item_inventory_count":1,"request_item_toolbar_count":1,"request_item_toolbar_slots_json":SLOTS,
              "request_fishing_experience_delta":40,"request_expected_maximum_occupants_after":REQUEST_MAX_AFTER,
              "request_expected_last_unlocked_population_gate_after":2,"request_expected_days_since_spawn_after":0,
              "request_expected_needed_item_count_after":-1,"request_expected_has_completed_request_after":true
            }}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("OUTPUT_STATUS", outputReady ? "ready" : "fish_pond_output_not_ready")
        .Replace("REQUEST_STATUS", outputReady ? "fish_pond_output_precedes_request" : "ready")
        .Replace("OUTPUT_HASH", OutputHash)
        .Replace("OUTPUTS", JsonSerializer.Serialize(outputs))
        .Replace("SLOTS", JsonSerializer.Serialize(slots))
        .Replace("REQUEST_MAX_AFTER", requestMaxAfter.ToString());
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
