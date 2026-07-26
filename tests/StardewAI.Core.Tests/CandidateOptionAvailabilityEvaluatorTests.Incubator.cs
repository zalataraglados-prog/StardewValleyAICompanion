using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void IncubatorDoesNotEmitOrdinaryCollectOrLoadActions()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Coop","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"176","qualified_item_id":"(O)176","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[
              {
                "location_id":"Coop","machine_is_incubator":true,"machine_completion_interaction_kind":"animal_house_hatch_naming_event","ordinary_output_collection_supported":false,
                "tile_x":5,"tile_y":5,"qualified_item_id":"(BC)101","display_name":"Incubator","ready_for_harvest":true,"minutes_until_ready":0,
                "harvest_experience_raw":"","harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience",
                "machine_data":{"status":"available","is_incubator":true,"has_output":true,"output_rule_count":1},
                "held_item":{"item_id":"176","qualified_item_id":"(O)176","stack":1,"quality":0,"sale_price":50}
              },
              {
                "location_id":"Coop","machine_is_incubator":true,"machine_completion_interaction_kind":"animal_house_hatch_naming_event","ordinary_output_collection_supported":false,
                "machine_has_input":true,"tile_x":7,"tile_y":5,"qualified_item_id":"(BC)101","display_name":"Incubator","ready_for_harvest":false,"minutes_until_ready":-1,
                "machine_execution_semantics":{"status":"available","execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven","prediction_training_status":"exact_current_snapshot_probe_supported"},
                "machine_data":{"status":"available","is_incubator":true,"has_output":true,"output_rule_count":1},
                "held_item":null,
                "loadable_inputs":[{"slot_index":0,"item_id":"176","qualified_item_id":"(O)176","stack":1,"quality":0,"sale_price":50,"load_executor_status":"covered_for_runtime_load","predicted_output":{"status":"available","training_eligibility_status":"exact_current_snapshot_probe_supported","matched_rule_id":"Default","effective_minutes_until_ready":9000,"item":{"item_id":"176","qualified_item_id":"(O)176","stack":1,"sale_price":50},"sale_price":50,"stack":1}}]
              }
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Coop","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[] { "farm.process_machines" },
                includeExecutorCalibrationOptions: true)
            .Options[0];

        var collect = Assert.Single(
            option.EventCandidates.Where(candidate =>
                candidate.Kind == "collect_machine_output_tile" &&
                candidate.TileX == 5));
        Assert.False(collect.Available);
        Assert.Contains(
            "machine_output_requires_incubator_hatch_flow",
            collect.BlockReasons);

        var load = Assert.Single(
            option.EventCandidates.Where(candidate =>
                candidate.Kind == "load_machine_input_tile" &&
                candidate.TileX == 7));
        Assert.False(load.Available);
        Assert.Contains(
            "machine_input_requires_incubator_hatch_value_model",
            load.BlockReasons);
    }
}
