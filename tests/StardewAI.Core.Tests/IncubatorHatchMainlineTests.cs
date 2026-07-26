using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class IncubatorHatchMainlineTests
{
    [Fact]
    public void ExactCapacitySafeEggFlowsToNativeLoadQueue()
    {
        var snapshot = Snapshot(
            activeMenuType: "none",
            menuOpen: false,
            ready: false,
            nativeSelected: false,
            occupantCount: 6,
            occupantLimit: 8,
            unreservedSlots: 2);

        var candidate = new EventCandidateRanker()
            .Rank(
                new BaselineTrainingReport(),
                new CandidateOptionAvailabilityEvaluator()
                    .Evaluate(
                        snapshot,
                        new[] { "farm.process_machines" },
                        includeExecutorCalibrationOptions: true))
            .Single(row =>
                row.Kind == "load_machine_input_tile");

        Assert.True(candidate.Available);
        Assert.Contains(
            "machine_special_prediction_model_id=incubator_animal_hatch.v1",
            candidate.ExpectedEffect);
        Assert.Contains(
            "incubator_hatch_animal_type_id=White Chicken",
            candidate.ExpectedEffect);
        Assert.Contains(
            "incubator_unreserved_hatch_slot_count=2",
            candidate.ExpectedEffect);

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(
                ";",
                queue.Items.SelectMany(item =>
                    item.BlockingReasons.Concat(
                        item.MissingStateFactors))));
        var item = queue.Items.Single(row =>
            row.OptionId == "executor.load_machine_input");
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name ==
                    "incubator_hatch_animal_type_id" &&
                parameter.Value == "White Chicken");
    }

    [Fact]
    public void MatureEggFlowsFromNamingMenuToTypedQueue()
    {
        var snapshot = Snapshot(
            activeMenuType: "NamingMenu",
            menuOpen: true,
            ready: true,
            nativeSelected: true,
            occupantCount: 7,
            occupantLimit: 8,
            unreservedSlots: 0);

        var candidate = new EventCandidateRanker()
            .Rank(
                new BaselineTrainingReport(),
                new CandidateOptionAvailabilityEvaluator()
                    .Evaluate(
                        snapshot,
                        new[] { "farm.process_machines" },
                        includeExecutorCalibrationOptions: true))
            .Single(row =>
                row.Kind == "name_hatched_animal");

        Assert.True(candidate.Available);
        Assert.Equal("Pip", CandidateParameter(
            candidate,
            "target_name"));
        Assert.Equal("White Chicken", CandidateParameter(
            candidate,
            "target_runtime_type"));

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal(
            "executor.name_hatched_animal",
            item.OptionId);
        var step = Assert.Single(
            item.NormalizedCommand.Steps);
        Assert.Equal("name_hatched_animal", step.StepType);
        Assert.Contains(
            "animal.name=Pip",
            step.ExpectedEffect);
    }

    [Fact]
    public void NamingQueueRejectsNativeSelectionDrift()
    {
        var initial = Snapshot(
            activeMenuType: "NamingMenu",
            menuOpen: true,
            ready: true,
            nativeSelected: true,
            occupantCount: 7,
            occupantLimit: 8,
            unreservedSlots: 0);
        var candidate = new EventCandidateRanker()
            .Rank(
                new BaselineTrainingReport(),
                new CandidateOptionAvailabilityEvaluator()
                    .Evaluate(
                        initial,
                        new[] { "farm.process_machines" },
                        includeExecutorCalibrationOptions: true))
            .Single(row =>
                row.Kind == "name_hatched_animal");
        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            initial.StateHash);
        var drifted = Snapshot(
            activeMenuType: "NamingMenu",
            menuOpen: true,
            ready: true,
            nativeSelected: false,
            occupantCount: 7,
            occupantLimit: 8,
            unreservedSlots: 0);

        var queue = new ActionQueueCompiler().Compile(
            plan,
            drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "incubator_hatch_projection_drifted",
            queue.Items[0].BlockingReasons);
    }

    private static string CandidateParameter(
        StardewAI.Contracts.Training.PolicyEventCandidatePrediction
            candidate,
        string name)
    {
        return candidate.Parameters
            .Single(parameter => parameter.Name == name)
            .Value;
    }

    private static SnapshotEnvelope Snapshot(
        string activeMenuType,
        bool menuOpen,
        bool ready,
        bool nativeSelected,
        int occupantCount,
        int occupantLimit,
        int unreservedSlots)
    {
        var minutes = ready ? 0 : -1;
        var heldItem = ready
            ? """
              {"item_id":"176","qualified_item_id":"(O)176","stack":1,"quality":0,"sale_price":50}
              """
            : "null";
        var specialStatus = ready
            ? "ready_requires_native_naming_event"
            : "idle";
        var stateJson = $$"""
        {
          "player": {
            "location_id": {"value":"Coop1","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"176","qualified_item_id":"(O)176","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{
              "location_id":"Coop1",
              "machine_is_incubator":true,
              "machine_has_input":true,
              "tile_x":5,
              "tile_y":5,
              "qualified_item_id":"(BC)101",
              "ready_for_harvest":{{ready.ToString().ToLowerInvariant()}},
              "minutes_until_ready":{{minutes}},
              "machine_execution_semantics":{"execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven"},
              "machine_data":{"status":"available","is_incubator":true,"has_output":true,"output_rule_count":1,"output_rules":[]},
              "machine_special_state":{
                "status":"{{specialStatus}}",
                "special_prediction_model_id":"incubator_animal_hatch.v1",
                "animal_house_occupant_count":{{occupantCount}},
                "animal_house_occupant_limit":{{occupantLimit}},
                "animal_house_has_capacity":{{(occupantCount < occupantLimit).ToString().ToLowerInvariant()}},
                "unreserved_hatch_slot_count":{{unreservedSlots}},
                "native_ready_selection_ordinal":{{(nativeSelected ? 0 : -1)}},
                "native_ready_selected":{{nativeSelected.ToString().ToLowerInvariant()}},
                "held_egg_qualified_item_id":"(O)176",
                "hatch_animal_type_id":"White Chicken",
                "suggested_hatch_name":"Pip"
              },
              "held_item":{{heldItem}},
              "loadable_inputs":[{
                "slot_index":0,
                "item_id":"176",
                "qualified_item_id":"(O)176",
                "stack":1,
                "quality":0,
                "sale_price":50,
                "load_executor_status":"covered_for_runtime_load",
                "predicted_output":{
                  "status":"available",
                  "training_eligibility_status":"exact_current_snapshot_probe_supported",
                  "special_prediction_model_id":"incubator_animal_hatch.v1",
                  "matched_rule_id":"Default",
                  "effective_minutes_until_ready":9000,
                  "hatch_animal_type_id":"White Chicken",
                  "suggested_hatch_name":"Pip",
                  "unreserved_hatch_slot_count":{{unreservedSlots}},
                  "animal_house_occupant_count":{{occupantCount}},
                  "animal_house_occupant_limit":{{occupantLimit}},
                  "animal_purchase_equivalent_value":800,
                  "item":{"item_id":"176","qualified_item_id":"(O)176","stack":1,"sale_price":50},
                  "sale_price":50,
                  "stack":1
                }
              }]
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":{{menuOpen.ToString().ToLowerInvariant()}},"type":"{{activeMenuType}}"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state": {"value":{"kind":"naming","done_callback_present":true,"done_button_present":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"location_id":"Coop1","width":20,"height":20},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Coop1","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
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
