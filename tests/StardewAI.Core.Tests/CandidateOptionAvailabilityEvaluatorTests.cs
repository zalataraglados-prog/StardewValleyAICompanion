using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class CandidateOptionAvailabilityEvaluatorTests
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
    public void ExecutorEnabledTrueForReconciledIds(string optionId)
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(Snapshot("{}"), new[] { optionId }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
    }

    [Theory]
    [InlineData("economy.sell_items")]
    [InlineData("social.talk_npc")]
    [InlineData("social.gift_npc")]
    [InlineData("quest.advance")]
    [InlineData("mining.reach_depth")]
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
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
        Assert.Contains("farm.machines[64,15].held_item=null", candidate.ExpectedEffect);
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
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":3,"output_rules":[{"id":"keg_wheat","required_item_id":"(O)262","minutes_until_ready":1750,"output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
            "machines": {"value":[{"tile_x":63,"tile_y":22,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":63,"tile_y":23,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"keg_wheat","required_item_id":"(O)262","minutes_until_ready":1750,"output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999},"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]},{"tile_x":63,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":65,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":64,"tile_y":14,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null},{"tile_x":64,"tile_y":16,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":10,"held_item":null}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
            "machines": {"value":[{"tile_x":5,"tile_y":5,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"sale_price":20,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
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

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("clear:Farm:11,10:grass", candidate.CandidateId);
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("move_to_adjacent=10,10;current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass", candidate.ExpectedEffect);
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

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Equal("move_to_adjacent=12,10;current_location.obstacle[13,10]=clear;clear_kind=grass;source=Grass", candidate.ExpectedEffect);
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

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.False(option.PreviewOnly);
        Assert.True(option.ExecutorEnabled);
        Assert.Empty(option.MissingStateFactors);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
    }

    [Fact]
    public void BuySuppliesAvailableWhenShopHasValueCandidate()
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

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("no_value_available_purchase_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("(O)472", candidate.QualifiedItemId);
        Assert.Equal(20, candidate.UnitPrice);
    }

    [Fact]
    public void BuySuppliesUsesLocationsShopStockPreviewBeforeMenuOpens()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuyPreviewSnapshot("""
              {
                "item_id":"378",
                "qualified_item_id":"(O)378",
                "display_name":"Copper Ore",
                "price":150,
                "stock":2147483647,
                "infinite_stock":true,
                "currency_balance":500,
                "executor_purchase_preview_enabled":true,
                "executor_block_reasons":[]
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("menus.shop_stock", option.MissingStateFactors);
        Assert.DoesNotContain("no_value_available_purchase_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("buy-preview:Blacksmith:0", candidate.CandidateId);
        Assert.Equal("buy_shop_item", candidate.Kind);
        Assert.Equal("Blacksmith", candidate.ShopId);
        Assert.Equal("(O)378", candidate.QualifiedItemId);
        Assert.Equal(150, candidate.UnitPrice);
    }

    [Fact]
    public void BuySuppliesBlocksWhenNoShopStockEntryPassesValueGates()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(BuySnapshot(entryOverride: """
              {
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":0,
                "infinite_stock":false,
                "can_buy_item":true,
                "can_afford_one_with_currency":false,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":false
              }
            """), new[] { "economy.buy_supplies" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_value_available_purchase_candidates", option.BlockingReasons);
        Assert.Contains("shop_item_out_of_stock", option.BlockingReasons);
        Assert.Contains("insufficient_currency_for_purchase", option.BlockingReasons);
        Assert.DoesNotContain("purchase_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("shop_item_out_of_stock", candidate.BlockReasons);
    }

    [Fact]
    public void SellItemsPreviewAvailableWhenUnprotectedInventoryCandidateExistsButExecutorDisabled()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(inventoryItemOverride: """
              {
                "slot_index":0,
                "qualified_item_id":"(O)24",
                "stack":3,
                "category":-75,
                "can_be_shipped":true,
                "sell_to_store_price":35,
                "sale_price":35,
                "protected_from_auto_sell":false,
                "auto_sell_protection_reasons":[],
                "is_empty":false
              }
            """), new[] { "economy.sell_items" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("preview_available", option.Status);
        Assert.True(option.PreviewOnly);
        Assert.Contains("sell_shipping_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("no_value_available_sell_candidates", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("sell_shop_item", candidate.Kind);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.False(candidate.CanShip);
        Assert.True(candidate.CanShopSell);
    }

    [Fact]
    public void SellItemsBlocksWhenInventoryCandidatesAreProtected()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(SellSnapshot(inventoryItemOverride: """
              {
                "slot_index":0,
                "qualified_item_id":"(O)24",
                "stack":3,
                "category":-75,
                "can_be_shipped":true,
                "sell_to_store_price":35,
                "sale_price":35,
                "protected_from_auto_sell":true,
                "auto_sell_protection_reasons":["special_item"],
                "is_empty":false
              }
            """), new[] { "economy.sell_items" })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_value_available_sell_candidates", option.BlockingReasons);
        Assert.Contains("inventory_item_protected_from_auto_sell", option.BlockingReasons);
        Assert.Contains("sell_shipping_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EconomicCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("inventory_item_protected_from_auto_sell", candidate.BlockReasons);
    }

    [Fact]
    public void BoundRouteCandidateReusesCompilerTargetBranchGate()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":20,"height":40,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":12,"tile_y":34,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[]
            {
                Candidate("exploration.visit_location",
                    Parameter("target_tile_x", "12"),
                    Parameter("target_tile_y", "34"))
            })
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("unsupported_route_action_branch_at_target", option.BlockingReasons);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationEmitsRouteConnectorEventCandidatesFromTransparentConnectors()
    {
        var snapshot = RouteConnectorSnapshot(routeTrainingBlocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.False(option.PreviewOnly);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("route:Farm:12,10:warp", candidate.CandidateId);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("Farm", candidate.LocationId);
        Assert.Equal(12, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("player.tile=12,10;route_connector=warp", candidate.ExpectedEffect);
        Assert.Equal(120, candidate.EstimatedTicks);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RouteConnectorEventCandidateKeepsCompilerBlockReasonsAtCandidateLevel()
    {
        var snapshot = RouteConnectorSnapshot(routeTrainingBlocked: true);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("unsupported_route_action_branch_at_target", candidate.BlockReasons);
        Assert.DoesNotContain("queue_global_compiler_block", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_available_route_connector_candidates", option.BlockingReasons);
        Assert.Contains("unsupported_route_action_branch_at_target", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationEmitsRouteRepairClearObstacleCandidateWhenConnectorTileIsClearable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":12,"tile_y":10,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[{"tile_x":12,"tile_y":10,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":12,"tile_y":10,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":12,"tile_y":10,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.DoesNotContain("route_executor_disabled", option.BlockingReasons);
        var route = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.False(route.Available);
        Assert.Contains("route_path_target_blocked_by_collision_grid", route.BlockReasons);
        var repair = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
        Assert.True(repair.Available);
        Assert.Equal("route_repair_clearable_obstacle", repair.AvailabilityClass);
        Assert.StartsWith("route-repair:route:Farm:12,10:warp:clear:Farm:12,10:grass", repair.CandidateId);
        Assert.Equal("route_repair_for=route:Farm:12,10:warp;move_to_adjacent=11,10;current_location.obstacle[12,10]=clear;clear_kind=grass;source=Grass", repair.ExpectedEffect);
    }

    [Fact]
    public void VisitLocationEmitsRouteRepairClearObstacleCandidateWhenPathSegmentIsClearable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":3,"tile_y":0,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":3,"tile_y":0,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        var route = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.False(route.Available);
        Assert.Contains("route_path_blocked_by_collision_grid", route.BlockReasons);
        var repair = Assert.Single(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
        Assert.True(repair.Available);
        Assert.StartsWith("route-repair:route:Farm:3,0:warp:clear:Farm:1,0:grass", repair.CandidateId);
        Assert.Equal("route_repair_for=route:Farm:3,0:warp;move_to_adjacent=0,0;current_location.obstacle[1,0]=clear;clear_kind=grass;source=Grass", repair.ExpectedEffect);
    }

    [Fact]
    public void ClearObstacleCandidateIsBlockedWhenTransparentEnergyIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Equal(2, candidate.EnergyCost);
        Assert.Contains("insufficient_energy_for_clear_obstacle", candidate.BlockReasons);
        Assert.Contains("no_available_clear_obstacle_candidates", option.BlockingReasons);
    }

    [Fact]
    public void VisitLocationDoesNotEmitRouteRepairCandidateWhenClearEnergyBudgetIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":3,"tile_y":0,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":3,"tile_y":0,"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "exploration.visit_location" })
            .Options[0];

        Assert.Single(option.EventCandidates, candidate => candidate.Kind == "route_connector_tile");
        Assert.DoesNotContain(option.EventCandidates, candidate => candidate.Kind == "clear_obstacle_tile");
    }

    [Fact]
    public void ClearObstacleCandidateIsBlockedWhenItWouldExceedDayTimeBudget()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2559,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.clear_obstacle" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("clear_obstacle_would_exceed_day_time_budget", candidate.BlockReasons);
        Assert.Contains("no_available_clear_obstacle_candidates", option.BlockingReasons);
    }

    [Fact]
    public void BoundInteractCandidateReusesCompilerMissingTargetGate()
    {
        var snapshot = InteractSnapshot(menuOpen: false, branch: "OpenShop", blocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { Candidate("executor.interact") }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("interact_target_tile_required", option.BlockingReasons);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void ValidBoundInteractCandidateIsAvailable()
    {
        var snapshot = InteractSnapshot(menuOpen: false, branch: "OpenShop", blocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[]
            {
                Candidate("executor.interact",
                    Parameter("target_tile_x", "11"),
                    Parameter("target_tile_y", "10"),
                    Parameter("interaction_kind", "map_action"),
                    Parameter("expected_action_type", "OpenShop"))
            }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.False(option.PreviewOnly);
        Assert.True(option.ExecutorEnabled);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", option.BlockingReasons);
        Assert.DoesNotContain("queue_global_compiler_block", option.BlockingReasons);
    }

    [Fact]
    public void InteractOptionEmitsEndpointCandidatesWithMoveToAdjacentPreview()
    {
        var snapshot = InteractEndpointSnapshot(menuOpen: false, branch: "OpenShop", routeTrainingBlocked: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.False(option.Available);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("interact_target_tile_required", option.BlockingReasons);
        Assert.DoesNotContain("interact_executor_disabled", option.BlockingReasons);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("interact:Town:11,10:OpenShop:SeedShop", candidate.CandidateId);
        Assert.Equal("interact_endpoint", candidate.Kind);
        Assert.Empty(candidate.BlockReasons);
        Assert.True(candidate.Available);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=OpenShop", candidate.ExpectedEffect);
        Assert.Equal(30, candidate.EstimatedTicks);
    }

    [Fact]
    public void InteractOptionEmitsJojaShopEndpointCandidate()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "JojaShop",
            routeTrainingBlocked: false,
            action: "JojaShop",
            parsed: "\"parsed\":{\"kind\":\"joja_shop\",\"shop_id\":\"Joja\"}");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("interact:Town:11,10:JojaShop:Joja", candidate.CandidateId);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=JojaShop", candidate.ExpectedEffect);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
    }

    [Fact]
    public void InteractOptionEmitsDialogueShopEndpointCandidateWithOwnerPresent()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "AnimalShop",
            routeTrainingBlocked: false,
            action: "AnimalShop",
            parsed: "\"parsed\":{\"kind\":\"dialogue_shop\",\"shop_id\":\"AnimalShop\",\"owner_npc\":\"Marnie\",\"owner_service_area\":{\"x\":9,\"y\":8,\"width\":5,\"height\":3},\"dialogue_key\":\"Marnie\",\"shop_response_key\":\"Supplies\"}",
            ownerServiceStatus: "\"owner_service_status\":{\"owner_required\":true,\"owner_npc\":\"Marnie\",\"owner_found\":true,\"owner_tile_x\":11,\"owner_tile_y\":9,\"in_service_area\":true,\"block_reason\":null}",
            npcPositions: "[{\"name\":\"Marnie\",\"location_id\":\"AnimalShop\",\"tile_x\":11,\"tile_y\":9}]");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("interact:Town:11,10:AnimalShop:AnimalShop", candidate.CandidateId);
        Assert.Equal("move_to_adjacent=10,10;preview_interact=AnimalShop", candidate.ExpectedEffect);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
        Assert.DoesNotContain("interact_shop_owner_npc_not_at_service_counter", candidate.BlockReasons);
    }

    [Fact]
    public void InteractOptionBlocksDialogueShopEndpointWhenOwnerNpcAbsent()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "Carpenter",
            routeTrainingBlocked: false,
            action: "Carpenter",
            parsed: "\"parsed\":{\"kind\":\"dialogue_shop\",\"shop_id\":\"Carpenter\",\"owner_npc\":\"Robin\",\"owner_service_area\":{\"x\":6,\"y\":17,\"width\":5,\"height\":3},\"dialogue_key\":\"carpenter\",\"shop_response_key\":\"Shop\"}",
            ownerServiceStatus: "\"owner_service_status\":{\"owner_required\":true,\"owner_npc\":\"Robin\",\"owner_found\":true,\"owner_tile_x\":21,\"owner_tile_y\":4,\"in_service_area\":false,\"block_reason\":\"owner_npc_not_at_service_counter\"}",
            npcPositions: "[{\"name\":\"Robin\",\"location_id\":\"ScienceHouse\",\"tile_x\":21,\"tile_y\":4}]");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_shop_owner_npc_not_at_service_counter", candidate.BlockReasons);
        Assert.DoesNotContain("interact_expected_action_type_mismatch", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("no_available_interact_endpoint_candidates", option.BlockingReasons);
        Assert.Contains("interact_shop_owner_npc_not_at_service_counter", option.BlockingReasons);
    }

    [Fact]
    public void InteractOptionBlocksShopEndpointWhenServiceTimeStatusDisallows()
    {
        var snapshot = InteractEndpointSnapshot(
            menuOpen: false,
            branch: "OpenShop",
            routeTrainingBlocked: false,
            serviceTimeStatus: "\"service_time_status\":{\"current_time\":800,\"time_gate_known\":true,\"open_time\":900,\"close_time\":1700,\"time_allowed\":false,\"allowed_now\":false,\"block_reasons\":[\"shop_not_open_yet\"]}");

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_shop_service_time_blocked", candidate.BlockReasons);
        Assert.Equal("blocked", option.Status);
        Assert.Equal("windowed_available", candidate.AvailabilityClass);
        Assert.False(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal(900, candidate.NextOpenTime);
        Assert.Equal(900, candidate.EffectiveOpenTime);
        Assert.Equal(1700, candidate.ClosesAt);
        Assert.Equal(3600, candidate.WaitCost);
        Assert.Contains("shop_not_open_yet", candidate.GateReasons);
        Assert.Contains("interact_shop_service_time_blocked", candidate.GateReasons);
        Assert.Contains("no_available_interact_endpoint_candidates", option.BlockingReasons);
    }


    [Fact]
    public void InteractEndpointCandidateBlocksWhenMenuIsOpenAndBranchBlocked()
    {
        var snapshot = InteractEndpointSnapshot(menuOpen: true, branch: "SkullDoor", routeTrainingBlocked: true);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "executor.interact" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("interact_menu_must_be_clear", candidate.BlockReasons);
        Assert.Contains("interact_unsupported_action_branch_at_target", candidate.BlockReasons);
        Assert.Contains("interact_expected_action_type_mismatch", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryEmitsExecutableRefreshPlanCandidateBeforeLateNight()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 1800, menuOpen: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.Available);
        Assert.Equal("available", option.Status);
        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:refresh_plan_after_stabilization", candidate.CandidateId);
        Assert.Equal("recovery_refresh_plan", candidate.Kind);
        Assert.True(candidate.Available);
        Assert.Equal("executor.wait_ticks=30;urgent_risks_rechecked", candidate.ExpectedEffect);
        Assert.Equal(30, candidate.EstimatedTicks);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryKeepsLateNightHomeAndSleepCandidatesBlockedUntilExecutorsExist()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.Equal(3, option.EventCandidates.Length);
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_close_menu" && candidate.BlockReasons.Contains("close_menu_type_unknown"));
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_return_home" && candidate.BlockReasons.Contains("recovery_cross_map_home_route_unverified"));
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_sleep_before_collapse" && candidate.BlockReasons.Contains("recovery_terminal_sleep_already_covered_by_return_home"));
    }

    [Fact]
    public void RecoveryReturnHomeCandidateUsesTransparentHomeBedTargetWhenAlreadyHome()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Equal("move_to_bed_adjacent=3,9;step_onto_sleep_touch_tile=3,8;touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateAllowsBlockedBedTileWhenAdjacentStandTileIsReachable()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Equal("move_to_bed_adjacent=3,9;step_onto_sleep_touch_tile=3,8;touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksWhenNoAdjacentBedStandTileIsReachable()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: true, adjacentBedTilesBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("recovery_bed_adjacent_stand_tile_unavailable", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateDoesNotUseActiveObjectAsSleepGate()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, activeObjectQualifiedId: "(O)472"), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.DoesNotContain("sleep_interact_active_object_must_be_clear", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksBedInteractionWhenMenuIsOpen()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("sleep_prompt_menu_must_be_clear", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksConfirmWhenSleepPromptIsAlreadyOpen()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: true, sleepPromptOpen: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("sleep_prompt_menu_must_be_clear", candidate.BlockReasons);
        Assert.Contains("recovery_sleep_prompt_already_open", candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyAvailableAtOrPast2400WhenHomeWithBed()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: true, bedBlocked: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyBlocksWhenOutsideHomeAtOrPast2400()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.False(candidate.Available);
        Assert.Contains("recovery_cross_map_home_route_unverified", candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyBlocksWhenBedMissingAtOrPast2400()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: true, bedBlocked: true, adjacentBedTilesBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.False(candidate.Available);
        Assert.Contains("recovery_bed_adjacent_stand_tile_unavailable", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryHighLevelEnabledAfterCandidateChainComplete()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 1800, menuOpen: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
        Assert.DoesNotContain("executor_disabled", option.BlockingReasons);
    }

    private static SnapshotEnvelope RecoverySnapshot(int time, bool menuOpen, bool currentLocationIsHome = true, bool bedBlocked = false, bool adjacentBedTilesBlocked = false, string activeObjectQualifiedId = "", bool sleepPromptOpen = false)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":TIME,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"CURRENT_LOCATION","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "current_item_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"ACTIVE_OBJECT","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"CURRENT_LOCATION","current_location_is_home":CURRENT_HOME,"entry_tile_x":3,"entry_tile_y":9,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":SLEEP_PROMPT_OPEN,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"CURRENT_LOCATION","width":12,"height":12,"notable_tiles":[BLOCKED_TILES]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("TIME", time.ToString())
        .Replace("CURRENT_LOCATION", currentLocationIsHome ? "FarmHouse" : "Town")
        .Replace("CURRENT_HOME", currentLocationIsHome ? "true" : "false")
        .Replace("BLOCKED_TILES", RecoveryBlockedTiles(bedBlocked, adjacentBedTilesBlocked))
        .Replace("ACTIVE_OBJECT", activeObjectQualifiedId)
        .Replace("SLEEP_PROMPT_OPEN", sleepPromptOpen ? "true" : "false")
        .Replace("MENU_OPEN", menuOpen ? "true" : "false"));
    }

    private static string RecoveryBlockedTiles(bool bedBlocked, bool adjacentBedTilesBlocked)
    {
        var tiles = new List<string>();
        if (bedBlocked)
        {
            tiles.Add("{\"tile_x\":3,\"tile_y\":8,\"collision_blocked\":true}");
        }

        if (adjacentBedTilesBlocked)
        {
            tiles.Add("{\"tile_x\":4,\"tile_y\":8,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":2,\"tile_y\":8,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":3,\"tile_y\":9,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":3,\"tile_y\":7,\"collision_blocked\":true}");
        }

        return string.Join(",", tiles);
    }

    private static SnapshotEnvelope RouteConnectorSnapshot(bool routeTrainingBlocked)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":12,"tile_y":10,"target_location":"Town","target_x":1,"target_y":2,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":12,"tile_y":10,"branch":"Warp","route_training_blocked":ROUTE_BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("ROUTE_BLOCKED", routeTrainingBlocked ? "true" : "false"));
    }

    private static SnapshotEnvelope InteractEndpointSnapshot(
        bool menuOpen,
        string branch,
        bool routeTrainingBlocked,
        string action = "OpenShop SeedShop Down 900 1700",
        string parsed = "\"parsed\":{\"kind\":\"open_shop\",\"shop_id\":\"SeedShop\",\"required_direction\":\"Down\",\"open_time\":900,\"close_time\":1700}",
        string ownerServiceStatus = "\"owner_service_status\":{\"owner_required\":false,\"owner_npc\":null,\"owner_found\":null,\"in_service_area\":null,\"block_reason\":null}",
        string serviceTimeStatus = "\"service_time_status\":{\"current_time\":900,\"time_gate_known\":false,\"allowed_now\":true,\"block_reasons\":[]}",
        string npcPositions = "[]")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_action_tiles": {"value":[{"tile_x":11,"tile_y":10,"action":"ACTION",PARSED,OWNER_SERVICE_STATUS,SERVICE_TIME_STATUS}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"BRANCH","route_training_blocked":ROUTE_BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "positions": {"value":NPC_POSITIONS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("ACTION", action)
        .Replace("PARSED", parsed)
        .Replace("OWNER_SERVICE_STATUS", ownerServiceStatus)
        .Replace("SERVICE_TIME_STATUS", serviceTimeStatus)
        .Replace("BRANCH", branch)
        .Replace("NPC_POSITIONS", npcPositions)
        .Replace("ROUTE_BLOCKED", routeTrainingBlocked ? "true" : "false"));
    }

    private static SnapshotEnvelope InteractSnapshot(bool menuOpen, string branch, bool blocked)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"BRANCH","route_training_blocked":BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("BRANCH", branch)
        .Replace("BLOCKED", blocked ? "true" : "false"));
    }

    private static SnapshotEnvelope BuySnapshot(string entryOverride)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crop_catalog": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{"kind":"shop_stock","read_only":false,"entry_count":1,"entries":[ENTRY]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("ENTRY", entryOverride));
    }

    private static SnapshotEnvelope BuyPreviewSnapshot(string entryOverride)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crop_catalog": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":{"shops":[{"shop_id":"Blacksmith","stock_preview":{"kind":"shop_stock_preview","shop_id":"Blacksmith","entries":[ENTRY]}}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("ENTRY", entryOverride));
    }

    private static SnapshotEnvelope SellSnapshot(string inventoryItemOverride)
    {
        return Snapshot("""
        {
          "player": {
            "inventory": {"value":[ITEM],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":{"kind":"shop_sell_context","read_only":false,"safety_timer":0,"held_item_present":false,"categories_to_sell":[-75],"tag_groups_to_sell":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("ITEM", inventoryItemOverride));
    }

    private static OptionAvailabilityCandidate Candidate(string optionId, params SmallModelActionParameter[] parameters)
    {
        return new OptionAvailabilityCandidate
        {
            OptionId = optionId,
            Parameters = parameters
        };
    }

    private static SnapshotEnvelope MachinePredictionSnapshot(string outputItemFields)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"derived_price","required_item_id":"(O)262","output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200OUTPUT_ITEM_FIELDS}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("OUTPUT_ITEM_FIELDS", outputItemFields));
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CloseMenuSafeOrdinaryDialogueAvailableWhenAllProofsPresent()
    {
        var snapshot = DialogueMenuRecoverySnapshot(
            eventUp: false, isQuestion: false, responseCount: 0, characterPresent: true,
            speakerName: "Lewis", lastQuestionKey: null, isSleepPrompt: false, transitioning: false);

        var candidate = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(c => c.Kind == "recovery_close_menu");

        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenEventUpTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(eventUp: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_event_up_true", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenIsQuestionTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(isQuestion: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_is_question_true", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenResponseCountGreaterThanZero()
    {
        var snapshot = DialogueMenuRecoverySnapshot(responseCount: 3);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_responses_present:3", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenCharacterPresentFalse()
    {
        var snapshot = DialogueMenuRecoverySnapshot(characterPresent: false);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_character_present_false", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenSpeakerNameEmpty()
    {
        var snapshot = DialogueMenuRecoverySnapshot(speakerName: "");
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_speaker_name_empty", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenSpeakerNameNull()
    {
        var snapshot = DialogueMenuRecoverySnapshot(speakerName: null);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_speaker_name_field_missing", candidate.BlockReasons);
    }

    [Fact]
    public void CloseMenuBlocksWhenLastQuestionKeyPresent()
    {
        var snapshot = DialogueMenuRecoverySnapshot(lastQuestionKey: "Sleep");
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains(candidate.BlockReasons, reason => reason.Contains("dialogue_close_last_question_key_present"));
    }

    [Fact]
    public void CloseMenuBlocksWhenSleepPromptTrue()
    {
        var snapshot = DialogueMenuRecoverySnapshot(isSleepPrompt: true);
        var candidate = GetCloseMenuCandidate(snapshot);
        Assert.False(candidate.Available);
        Assert.Contains("dialogue_close_is_sleep_prompt", candidate.BlockReasons);
    }

    private static EventCandidate GetCloseMenuCandidate(SnapshotEnvelope snapshot)
    {
        return new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0]
            .EventCandidates
            .Single(c => c.Kind == "recovery_close_menu");
    }

    private static SnapshotEnvelope DialogueMenuRecoverySnapshot(
        bool eventUp = false,
        bool isQuestion = false,
        int responseCount = 0,
        bool characterPresent = true,
        string? speakerName = "Lewis",
        string? lastQuestionKey = null,
        bool isSleepPrompt = false,
        bool transitioning = false)
    {
        var speakerField = speakerName is null
            ? ""
            : ",\"dialogue_speaker_name\":\"" + speakerName.Replace("\"", "\\\"") + "\"";
        var lastQkField = string.IsNullOrWhiteSpace(lastQuestionKey)
            ? ""
            : ",\"last_question_key\":\"" + lastQuestionKey.Replace("\"", "\\\"") + "\"";

        var json = """
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "current_item_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"entry_tile_x":3,"entry_tile_y":9,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{
              "is_open":true,
              "type":"DialogueBox",
              "full_type":"StardewValley.Menus.DialogueBox",
              "is_sleep_prompt":IS_SLEEP,
              "event_up":EVENT_UP,
              "dialogue_is_question":IS_QUESTION,
              "dialogue_response_count":RESPONSE_COUNT,
              "dialogue_transitioning":TRANSITIONING,
              "dialogue_safety_timer":0,
              "dialogue_character_present":CHAR_PRESENT,
              "dialogue_typing":true,
              "dialogue_finished":false
              SPEAKER
              LAST_QK
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("EVENT_UP", eventUp ? "true" : "false")
            .Replace("IS_QUESTION", isQuestion ? "true" : "false")
            .Replace("RESPONSE_COUNT", responseCount.ToString())
            .Replace("CHAR_PRESENT", characterPresent ? "true" : "false")
            .Replace("SPEAKER", speakerField)
            .Replace("LAST_QK", lastQkField)
            .Replace("IS_SLEEP", isSleepPrompt ? "true" : "false")
            .Replace("TRANSITIONING", transitioning ? "true" : "false");
        return Snapshot(json);
    }
}
