using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Theory]
    [InlineData("farm.process_machines")]
    [InlineData("economy.buy_supplies")]
    [InlineData("exploration.visit_location")]
    [InlineData("executor.interact")]
    [InlineData("executor.sleep")]
    [InlineData("executor.pickup_debris")]
    [InlineData("executor.collect_machine_output")]
    [InlineData("executor.load_machine_input")]
    [InlineData("mining.reach_depth")]
    [InlineData("economy.sell_items")]
    public void ExecutorEnabledTrueForReconciledIds(string optionId)
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("{}"), new[] { optionId }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
    }

    [Theory]
    [InlineData("social.talk_npc")]
    [InlineData("social.gift_npc")]
    [InlineData("quest.advance")]
    public void ExecutorEnabledFalseForIncompleteHighLevelIds(string optionId)
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("{}"), new[] { optionId }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.ExecutorEnabled);
    }

    [Fact]
    public void ExecutorSocialInteractIsNotAdvertisedAsExecutorEnabled()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("{}"), new[] { "executor.social_interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
    }

    [Fact]
    public void EvaluateMarksMaintainCropsAvailableWhenRequiredFieldsExist()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true);

        var option = Assert.Single(availability.Options);
        Assert.Equal("farm.maintain_crops", option.OptionId);
        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.True(option.ExecutorEnabled);
        Assert.False(option.PreviewOnly);
        Assert.Empty(option.MissingStateFactors);
        Assert.Empty(option.HardBlockReasons);
    }

    [Fact]
    public void MaintainCropsEmitsWateringEventCandidatesFromTransparentCropState()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[{"tile_x":1,"tile_y":2,"needs_watering":true},{"tile_x":3,"tile_y":4,"needs_watering":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("water:Farm:1,2", candidate.CandidateId);
        Assert.Equal("water_crop_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(1, candidate.TileX);
        Assert.Equal(2, candidate.TileY);
        Assert.Equal("farm.crops[1,2].needs_watering=false", candidate.ExpectedEffect);
        Assert.Equal(60, candidate.EstimatedTicks);
        Assert.Equal(2, candidate.EnergyCost);
    }

    [Fact]
    public void MaintainCropsEmitsHarvestCandidatesFromTransparentCropState()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[{"tile_x":7,"tile_y":8,"harvest_item_id":"24","harvest_method":"Grab","ready_for_harvest":true,"needs_watering":false},{"tile_x":3,"tile_y":4,"ready_for_harvest":false,"needs_watering":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("harvest:Farm:7,8", candidate.CandidateId);
        Assert.Equal("harvest_crop_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(7, candidate.TileX);
        Assert.Equal(8, candidate.TileY);
        Assert.Contains("farm.crops[7,8].ready_for_harvest=false", candidate.ExpectedEffect);
        Assert.Contains("harvest_item_id=24", candidate.ExpectedEffect);
        Assert.Contains("harvest_method=Grab", candidate.ExpectedEffect);
        Assert.Contains("harvest_executor_status=runtime_verified", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void MaintainCropsEmitsGiantCropHarvestCandidatesFromTransparentResourceClumps()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"fall","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "resource_clumps": {"value":[{"tile_x":7,"tile_y":8,"width":3,"height":3,"health":3,"is_giant_crop":true,"giant_crop_id":"276","required_tool":"axe","executor_status":"blocked_requires_giant_crop_executor"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("harvest-giant-crop:Farm:7,8", candidate.CandidateId);
        Assert.Equal("harvest_giant_crop_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(7, candidate.TileX);
        Assert.Equal(8, candidate.TileY);
        Assert.Contains("farm.resource_clumps[7,8].is_giant_crop=false", candidate.ExpectedEffect);
        Assert.Contains("giant_crop_id=276", candidate.ExpectedEffect);
        Assert.Contains("required_tool=axe", candidate.ExpectedEffect);
        Assert.Contains("harvest_giant_crop_executor_status=runtime_verified", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void MaintainCropsEmitsPickupDebrisCandidatesFromTransparentDebrisState()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"fall","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":64,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":10,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "debris": {"value":[{"debris_index":0,"debris_type":"OBJECT","chunk_type":0,"item_id":"(O)388","qualified_item_id":"(O)388","item_quality":0,"chunk_count":1,"chunks":[{"chunk_index":0,"tile_x":65,"tile_y":15,"pixel_x":4160,"pixel_y":960}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("pickup_debris_item", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(65, candidate.TileX);
        Assert.Equal(15, candidate.TileY);
        Assert.Equal("(O)388", candidate.QualifiedItemId);
        Assert.Contains("debris_index=0", candidate.ExpectedEffect);
        Assert.Contains("pickup_executor_status=runtime_collect", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ProcessMachinesEmitsCollectOutputCandidatesFromTransparentMachineState()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":10,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"location_id":"Farm","location_kind":"farm_outdoor","tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("collect_machine_output_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(64, candidate.TileX);
        Assert.Equal(15, candidate.TileY);
        Assert.Equal("(O)388", candidate.QualifiedItemId);
        Assert.Contains("move_to_adjacent=63,15", candidate.ExpectedEffect);
        Assert.Contains("farm.machines[Farm:64,15].held_item=null", candidate.ExpectedEffect);
        Assert.Contains("output_sale_price=20", candidate.ExpectedEffect);
        Assert.Contains("output_total_value=20", candidate.ExpectedEffect);
        Assert.Contains("machine_value_basis=held_item_sale_price_times_stack", candidate.ExpectedEffect);
        Assert.Contains("machine_output_executor_status=runtime_collect", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ProcessMachinesEmitsLoadInputCandidatesFromTransparentMachineProbeState()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"location_id":"Farm","location_kind":"farm_outdoor","machine_has_input":true,"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":3,"output_rules":[{"id":"keg_wheat","required_item_id":"(O)262","minutes_until_ready":1750,"output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(64, candidate.TileX);
        Assert.Equal(15, candidate.TileY);
        Assert.Equal("(O)262", candidate.QualifiedItemId);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.Equal(2, candidate.Quantity);
        Assert.Contains("move_to_adjacent=63,15", candidate.ExpectedEffect);
        Assert.Contains("input_slot_index=0", candidate.ExpectedEffect);
        Assert.Contains("input_stack_available=2", candidate.ExpectedEffect);
        Assert.Contains("input_sale_price=15", candidate.ExpectedEffect);
        Assert.Contains("machine_input_opportunity_cost=15", candidate.ExpectedEffect);
        Assert.Contains("machine_input_value_basis=predicted_output_total_value_minus_transparent_input_sale_price", candidate.ExpectedEffect);
        Assert.Contains("machine_output_rule_count=3", candidate.ExpectedEffect);
        Assert.Contains("machine_has_output_rule=true", candidate.ExpectedEffect);
        Assert.Contains("machine_output_prediction_status=machine_data_exact_required_item_match", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_qualified_item_id=(O)346", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_total_value=200", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_rule_required_item_id=(O)262", candidate.ExpectedEffect);
        Assert.Contains("predicted_minutes_until_ready=1750", candidate.ExpectedEffect);
        Assert.Contains("machine_input_probe_source=Object.performObjectDropInAction(probe:true)", candidate.ExpectedEffect);
        Assert.Contains("machine_input_executor_status=runtime_load", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ProcessMachinesSkipsMachineOccupiedStandTilesForAdjacentRows()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":22,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":10,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":63,"tile_y":22,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":63,"tile_y":23,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var candidate = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:63,23", StringComparison.Ordinal));

        Assert.True(candidate.Available);
        Assert.DoesNotContain("move_to_adjacent=63,22", candidate.ExpectedEffect);
        Assert.Contains("move_to_adjacent=", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ProcessMachinesBlocksOutputAndInputWhenAllAdjacentStandTilesAreMachineOccupied()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":64,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"keg_wheat","required_item_id":"(O)262","minutes_until_ready":1750,"output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999},"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]},{"tile_x":63,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":65,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":64,"tile_y":14,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":64,"tile_y":16,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var candidates = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates;
        var output = candidates.Single(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:64,15", StringComparison.Ordinal));
        var input = candidates.Single(candidate => candidate.CandidateId.StartsWith("machine-input:Farm:64,15", StringComparison.Ordinal));

        Assert.False(output.Available);
        Assert.Contains("machine_adjacent_stand_tile_occupied_by_machine", output.BlockReasons);
        Assert.DoesNotContain("move_to_adjacent=", output.ExpectedEffect);
        Assert.False(input.Available);
        Assert.Contains("machine_adjacent_stand_tile_occupied_by_machine", input.BlockReasons);
        Assert.DoesNotContain("move_to_adjacent=", input.ExpectedEffect);
    }

    [Fact]
    public void ProcessMachinesUsesRoutePreviewToSkipNearestUnreachableStandTile()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":100,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":10,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":5,"tile_y":5,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":10,"height":10,"notable_tiles":[{"tile_x":3,"tile_y":5,"collision_blocked":true},{"tile_x":4,"tile_y":4,"collision_blocked":true},{"tile_x":4,"tile_y":6,"collision_blocked":true},{"tile_x":5,"tile_y":5,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var candidate = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(candidate => candidate.CandidateId.StartsWith("machine-output:Farm:5,5", StringComparison.Ordinal));

        Assert.True(candidate.Available);
        Assert.DoesNotContain("move_to_adjacent=4,5", candidate.ExpectedEffect);
        Assert.Contains("move_to_adjacent=5,4", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void ProcessMachinesDoesNotPredictConditionalMachineRuleValue()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"conditional","required_item_id":"(O)262","condition":"PLAYER_HAS_MAIL Current QiChallenge","output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.Contains("machine_output_prediction_status=machine_data_exact_required_item_match_condition_not_evaluated", candidate.ExpectedEffect);
        Assert.Contains("machine_input_value_basis=transparent_input_sale_price_output_unknown", candidate.ExpectedEffect);
        Assert.DoesNotContain("predicted_output_total_value=", candidate.ExpectedEffect);
    }

    [Fact]
    public void ProcessMachinesPricesAdditionalConsumedItemsWhenTransparent()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":2,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"with_extra","required_item_id":"(O)262","output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200},"additional_consumed_item_count":1,"additional_consumed_items":[{"item_id":"388","qualified_item_id":"(O)388","amount":1,"sale_price":50,"total_value":50}]}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.Contains("machine_output_prediction_status=machine_data_exact_required_item_match", candidate.ExpectedEffect);
        Assert.Contains("machine_input_value_basis=predicted_output_total_value_minus_transparent_input_and_additional_consumed_sale_price", candidate.ExpectedEffect);
        Assert.Contains("machine_additional_consumed_total_value=50", candidate.ExpectedEffect);
        Assert.Contains("machine_additional_consumed_items=(O)388:1", candidate.ExpectedEffect);
        Assert.Contains("machine_additional_consumed_available=(O)388:1", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_net_value=135", candidate.ExpectedEffect);
    }

    [Fact]
    public void ProcessMachinesUsesTransparentInputSalePriceForCopyPriceOutput()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"copy_price","required_item_id":"(O)262","output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":0,"copy_price":true}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.Contains("machine_output_prediction_status=machine_data_exact_required_item_match", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_sale_price=15", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_price_source=copy_price_from_transparent_input_sale_price", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_total_value=15", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_net_value=0", candidate.ExpectedEffect);
    }

    [Theory]
    [InlineData(",\"copy_quality\":true", "machine_data_exact_required_item_match_copy_quality_not_priced")]
    [InlineData(",\"copy_color\":true", "machine_data_exact_required_item_match_copy_color_not_priced")]
    [InlineData(",\"preserve_type\":\"Wine\"", "machine_data_exact_required_item_match_preserve_not_priced")]
    public void ProcessMachinesReportsSpecificDerivedPricingBlocks(string outputItemFields, string expectedStatus)
    {
        var snapshot = MachinePredictionSnapshot(outputItemFields);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.Contains("machine_output_prediction_status=" + expectedStatus, candidate.ExpectedEffect);
        Assert.DoesNotContain("predicted_output_total_value=", candidate.ExpectedEffect);
    }

    [Fact]
    public void ProcessMachinesUsesTransparentNativeProbeForPreserveOutputValue()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"wine","required_item_id":"(O)262","output_item":{"item_id":"348","qualified_item_id":"(O)348","stack":1,"sale_price":0,"preserve_type":"Wine","preserve_id":"DROP_IN"}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"predicted_output":{"status":"available","source":"MachineDataUtility.GetOutputItem(probe:true)","matched_rule_id":"wine","required_item_id":"(O)262","effective_minutes_until_ready":1750,"item":{"item_id":"348","qualified_item_id":"(O)348","stack":1,"quality":0,"sale_price":200},"sale_price":200,"stack":1,"quality":0,"preserve_type":"Wine","preserved_item_id":"262"},"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.process_machines" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates.Where(candidate => candidate.Kind == "load_machine_input_tile"));
        Assert.Contains("machine_output_prediction_status=machine_native_probe_available", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_price_source=machine_native_probe_sale_price", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_total_value=200", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_net_value=185", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_preserve_type=Wine", candidate.ExpectedEffect);
        Assert.Contains("predicted_output_preserved_item_id=262", candidate.ExpectedEffect);
        Assert.Contains("predicted_minutes_until_ready=1750", candidate.ExpectedEffect);
    }

    [Fact]
    public void PlantSeedEmitsCandidateFromTransparentPlantingContext()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[{"slot_index":0,"item_id":"472","qualified_item_id":"(O)472","stack":3}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "planting_context": {"value":{"location_id":"Farm","hoe_dirt_tiles":[{"tile_x":5,"tile_y":6,"has_crop":false,"seed_results":[{"slot_index":0,"seed_id":"472","hard_rule_allows_planting":true,"can_mature_before_season_end_with_paddy_if_eligible":true,"adjusted_grow_days_with_paddy_if_eligible":4,"days_remaining_in_season":20}]}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crop_catalog": {"value":[{"seed_id":"472","harvest_item_id":"24","harvest_item_qualified_id":"(O)24","harvest_unit_sale_price":35,"harvest_min_stack":1,"harvest_max_stack":3,"harvest_max_increase_per_farming_level":0,"extra_harvest_chance":0.25,"harvest_min_quality":0,"harvest_max_quality":4,"harvest_method":"Grab","regrow_days":4}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":{"shops":[{"shop_id":"SeedShop","stock_preview":{"entries":[{"item_id":"472","qualified_item_id":"(O)472","price":20}]}}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.plant_seed" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("plant:Farm:5,6:472", candidate.CandidateId);
        Assert.Equal("plant_seed_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(5, candidate.TileX);
        Assert.Equal(6, candidate.TileY);
        Assert.Equal("472", candidate.ItemId);
        Assert.Equal("(O)472", candidate.QualifiedItemId);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.Equal(3, candidate.Quantity);
        Assert.Contains("seed_id=472", candidate.ExpectedEffect);
        Assert.Contains("adjusted_grow_days=4", candidate.ExpectedEffect);
        Assert.Contains("days_remaining_in_season=20", candidate.ExpectedEffect);
        Assert.Contains("harvest_item_id=24", candidate.ExpectedEffect);
        Assert.Contains("harvest_unit_sale_price=35", candidate.ExpectedEffect);
        Assert.Contains("harvest_min_stack=1", candidate.ExpectedEffect);
        Assert.Contains("harvest_max_stack=3", candidate.ExpectedEffect);
        Assert.Contains("extra_harvest_chance=0.25", candidate.ExpectedEffect);
        Assert.Contains("harvest_method=Grab", candidate.ExpectedEffect);
        Assert.Contains("regrow_days=4", candidate.ExpectedEffect);
        Assert.Contains("expected_first_harvest_value=35", candidate.ExpectedEffect);
        Assert.Contains("expected_first_harvest_value_basis=conservative_min_stack_only", candidate.ExpectedEffect);
        Assert.Contains("estimated_first_harvest_quantity=2.3333", candidate.ExpectedEffect);
        Assert.Contains("estimated_first_harvest_value=81.6667", candidate.ExpectedEffect);
        Assert.Contains("estimated_first_harvest_value_basis=mean_stack_plus_extra_chance_quality0_no_farming_scaling", candidate.ExpectedEffect);
        Assert.Contains("estimated_regrow_harvest_count=4", candidate.ExpectedEffect);
        Assert.Contains("estimated_total_harvest_count=5", candidate.ExpectedEffect);
        Assert.Contains("expected_season_harvest_value=175", candidate.ExpectedEffect);
        Assert.Contains("estimated_season_harvest_value=408.3333", candidate.ExpectedEffect);
        Assert.Contains("seed_unit_cost=20", candidate.ExpectedEffect);
        Assert.Contains("expected_first_harvest_net_value=15", candidate.ExpectedEffect);
        Assert.Contains("estimated_first_harvest_net_value=61.6667", candidate.ExpectedEffect);
        Assert.Contains("expected_season_harvest_net_value=155", candidate.ExpectedEffect);
        Assert.Contains("estimated_season_harvest_net_value=388.3333", candidate.ExpectedEffect);
        Assert.Contains("season_harvest_value_basis=first_harvest_value_times_transparent_regrow_count_seed_cost_once", candidate.ExpectedEffect);
        Assert.Contains("regrow_estimate_basis=adjusted_grow_days_days_remaining_regrow_days", candidate.ExpectedEffect);
        Assert.Contains("net_value_basis=transparent_seed_unit_cost_subtracted", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void PlantSeedCandidateBlocksWhenSeedCannotMatureBeforeSeasonEnd()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[{"slot_index":0,"item_id":"472","qualified_item_id":"(O)472","stack":3}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "planting_context": {"value":{"location_id":"Farm","hoe_dirt_tiles":[{"tile_x":5,"tile_y":6,"has_crop":false,"seed_results":[{"slot_index":0,"seed_id":"472","hard_rule_allows_planting":true,"can_mature_before_season_end_with_paddy_if_eligible":false}]}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.plant_seed" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("seed_would_not_mature_before_season_end", candidate.BlockReasons);
        Assert.Contains("no_available_plant_seed_candidates", option.BlockingReasons);
    }

    [Fact]
    public void MaintainCropsIncludesFarmMaintenanceClearObstacleCandidatesWhenTransparentLocationStateExists()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[{"tile_x":1,"tile_y":2,"needs_watering":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":11,"tile_y":10,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal(2, option.EventCandidates.Length);
        Assert.Contains(option.EventCandidates, candidate =>
            candidate.CandidateId == "water:Farm:1,2" &&
            candidate.Kind == "water_crop_tile");
        var clear = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
        Assert.True(clear.Available);
        Assert.Equal("farm-maintenance:clear:Farm:11,10:grass", clear.CandidateId);
        Assert.StartsWith("farm_maintenance_clear_obstacle=true;move_to_adjacent=10,10;", clear.ExpectedEffect);
    }

    [Fact]
    public void ClearObstacleEmitsAdjacentTransparentTerrainFeatureCandidate()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":11,"tile_y":10,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("clear:Farm:11,10:grass", candidate.CandidateId);
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("move_to_adjacent=10,10;current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass;max_tool_swings=8", candidate.ExpectedEffect);
        Assert.Equal(60, candidate.EstimatedTicks);
        Assert.Equal(0, candidate.EnergyCost);
    }

    [Fact]
    public void ClearObstacleEmitsMoveToAdjacentForNonAdjacentTransparentTerrainFeatureCandidate()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":13,"tile_y":10,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Equal("move_to_adjacent=12,10;current_location.obstacle[13,10]=clear;clear_kind=grass;source=Grass;max_tool_swings=8", candidate.ExpectedEffect);
        Assert.Equal(180, candidate.EstimatedTicks);
    }

    [Fact]
    public void EvaluateMapsMissingRequiredFieldsToCandidateBlock()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":"none","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("missing_required_state", option.BlockingReasons);
        Assert.Contains("farm.crop_catalog", option.MissingStateFactors);
        Assert.Contains("player.seed_inventory", option.MissingStateFactors);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
    }

    [Fact]
    public void EvaluateBlocksUnknownOptionBeforeModelScoring()
    {
        var snapshot = Snapshot("{}");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "unknown.option" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("unknown_option_id", option.BlockingReasons);
    }

    [Fact]
    public void DefaultCandidatesExcludeExecutorCalibrationOptions()
    {
        var snapshot = Snapshot("{}");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, Array.Empty<string>());

        Assert.DoesNotContain(availability.Options, option => option.OptionId == "farm.maintain_crops");
        Assert.DoesNotContain(availability.Options, option => option.OptionId == "executor.move_to_tile");
        Assert.DoesNotContain(availability.Options, option => option.OptionId == "executor.face_direction");
        Assert.DoesNotContain(availability.Options, option => option.OptionId == "executor.wait_ticks");
        Assert.Contains(availability.Options, option => option.OptionId == "social.gift_npc");
    }

    [Fact]
    public void PreviewOnlyOptionsAreNotAvailableForDefaultModelScoring()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuySnapshot(entryOverride: """
              {
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":false
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.True(option.ReadEligible);
        Assert.Equal("unbound", option.BindingStatus);
        Assert.Equal("not_evaluated", option.CompileStatus);
        Assert.False(option.PreviewOnly);
        Assert.True(option.ExecutorEnabled);
        Assert.Empty(option.MissingStateFactors);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
    }

}
