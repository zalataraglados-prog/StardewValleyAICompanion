using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CaskMachineMainlineTests
{
    [Fact]
    public void CaskVettedPredictionFlowsThroughPlanAndQueue()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Cellar","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"348","qualified_item_id":"(O)348","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{
              "location_id":"Cellar",
              "location_kind":"cellar",
              "machine_has_input":true,
              "tile_x":5,
              "tile_y":5,
              "qualified_item_id":"(BC)163",
              "display_name":"Cask",
              "ready_for_harvest":false,
              "minutes_until_ready":-1,
              "machine_execution_semantics":{
                "status":"available",
                "execution_status":"available_native_runtime_override",
                "input_dispatch_kind":"base_object_data_driven",
                "prediction_training_status":"exact_current_snapshot_probe_supported",
                "vetted_special_prediction_model_ids":["cask_quality_aging.v1"]
              },
              "machine_data":{"status":"available","has_output":true,"output_rule_count":6,"additional_consumed_item_count":0,"output_rules":[]},
              "held_item":null,
              "loadable_inputs":[{
                "slot_index":0,
                "item_id":"348",
                "qualified_item_id":"(O)348",
                "stack":1,
                "quality":0,
                "sale_price":400,
                "probe_source":"Object.performObjectDropInAction(probe:true)",
                "load_executor_status":"covered_for_runtime_load",
                "predicted_output":{
                  "status":"available",
                  "training_eligibility_status":"exact_current_snapshot_probe_supported",
                  "source":"decompiled_Cask.OutputCask_static_model",
                  "special_prediction_model_id":"cask_quality_aging.v1",
                  "matched_rule_id":"Wine",
                  "required_item_id":"(O)348",
                  "effective_days_to_next_quality":14,
                  "effective_days_until_ready":56,
                  "aging_rate_per_day":1,
                  "initial_quality":0,
                  "projected_final_quality":4,
                  "item":{"item_id":"348","qualified_item_id":"(O)348","stack":1,"quality":4,"sale_price":1600},
                  "sale_price":1600,
                  "stack":1,
                  "quality":4
                }
              }]
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"location_id":"Cellar","width":40,"height":30},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Cellar","width":40,"height":30,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true);
        var candidate = new EventCandidateRanker()
            .Rank(
                new BaselineTrainingReport(),
                availability)
            .Single(row => row.Kind == "load_machine_input_tile");

        Assert.True(candidate.Available);
        Assert.Contains(
            "machine_special_prediction_model_id=cask_quality_aging.v1",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_days_until_ready=56",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_days_to_next_quality=14",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_initial_quality=0",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_final_quality=4",
            candidate.ExpectedEffect);
        Assert.Contains(
            "predicted_output_total_value=1600",
            candidate.ExpectedEffect);
        Assert.DoesNotContain(
            "predicted_minutes_until_ready=",
            candidate.ExpectedEffect);

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash);
        var loadPlan = plan.Steps.Single(
            step => step.Kind == "load_machine_input");
        Assert.Contains(
            loadPlan.Parameters,
            parameter =>
                parameter.Name == "predicted_days_until_ready" &&
                parameter.Value == "56");
        Assert.Contains(
            loadPlan.Parameters,
            parameter =>
                parameter.Name == "machine_special_prediction_model_id" &&
                parameter.Value == "cask_quality_aging.v1");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.True(
            queue.Status == "pending",
            string.Join(
                ";",
                queue.Items.SelectMany(item =>
                    item.BlockingReasons)));
        var step = Assert.Single(
            queue.Items.Single(item =>
                    item.OptionId == "executor.load_machine_input")
                .NormalizedCommand.Steps);
        Assert.Contains(
            "predicted_days_until_ready=56",
            step.ExpectedEffect);
        Assert.Contains(
            "predicted_final_quality=4",
            step.ExpectedEffect);
        Assert.DoesNotContain(
            "predicted_minutes_until_ready=",
            step.ExpectedEffect);
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
                stateJson,
                JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
