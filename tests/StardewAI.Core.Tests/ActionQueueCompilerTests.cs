using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileBlocksDirectHighLevelCropMaintenanceWithoutCandidatePlan()
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
        var request = Request(snapshot.StateHash, "farm.maintain_crops");

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("action_queue.v1", queue.SchemaVersion);
        Assert.Equal("blocked", queue.Status);
        Assert.Equal("training_singleplayer", queue.ExecutionMode);
        Assert.Equal("training_farmer.main", queue.Actor.ActorId);
        Assert.Single(queue.Items);
        Assert.Equal("blocked", queue.Items[0].Status);
        Assert.Equal("mechanical", queue.Items[0].BehaviorCategory);
        Assert.Equal("full_action_expansion", queue.Items[0].CompilerResponsibility);
        Assert.Equal("executor_calibration", queue.Items[0].TrainingRole);
        Assert.Equal("farm.maintain_crops", queue.Items[0].NormalizedCommand.OptionId);
        Assert.Equal("compiled_action_steps", queue.Items[0].NormalizedCommand.CommandType);
        Assert.Equal("executor_calibration", queue.Items[0].NormalizedCommand.TrainingRole);
        Assert.Contains("full_action_step_compilation_empty", queue.Items[0].BlockingReasons);
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("training_farmer.main", queue.Items[0].NormalizedCommand.Actor.ActorId);
    }

    [Fact]
    public void CompileTurnsCropMaintenancePlanIntoPerTileWaterPrimitives()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "crops": {"value":[
              {"tile_x":1,"tile_y":2,"needs_watering":true},
              {"tile_x":3,"tile_y":4,"needs_watering":false},
              {"tile_x":5,"tile_y":6,"needs_watering":true}
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":65,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var plan = Plan(
            snapshot.StateHash,
            new SmallModelPlanStep { StepId = "water.1.2", Kind = "water_crop", TargetLocation = "Farm", TargetTileX = 1, TargetTileY = 2 },
            new SmallModelPlanStep { StepId = "water.5.6", Kind = "water_crop", TargetLocation = "Farm", TargetTileX = 5, TargetTileY = 6 });
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors))));
        Assert.Equal(2, queue.Items.Length);
        Assert.All(queue.Items, item => Assert.Equal("executor.water_crop", item.OptionId));
        Assert.Contains(queue.Items, item => Assert.Single(item.NormalizedCommand.Steps).Target == "Farm(1,2)");
        Assert.Contains(queue.Items, item => Assert.Single(item.NormalizedCommand.Steps).Target == "Farm(5,6)");
    }

    [Fact]
    public void CompileExpandsTillSoilIntoNativeHoeStep()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"width":80,"height":65},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":65,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.till_soil");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var step = Assert.Single(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("till_soil", step.StepType);
        Assert.Equal("Farm(7,8)", step.Target);
        Assert.Contains("native_tool=Hoe", step.ExpectedEffect);
        Assert.True(step.EstimatedTicks >= 85);
    }

    [Fact]
    public void CompileExpandsClearObstacleIntoTargetedToolStep()
    {
        var snapshot = ClearObstacleSnapshot();
        var request = Request(snapshot.StateHash, "executor.clear_obstacle");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "11" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "10" },
            new SmallModelActionParameter { Name = "max_tool_swings", Value = "4" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.clear_obstacle", item.OptionId);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("clear_obstacle", step.StepType);
        Assert.Equal("current_location(11,10)", step.Target);
        Assert.Equal("current_location.obstacle[11,10]=clear_or_blocked", step.ExpectedEffect);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "11");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_y" && parameter.Value == "10");
    }

    [Fact]
    public void CompileBlocksClearObstacleWithoutTargetTile()
    {
        var snapshot = ClearObstacleSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.clear_obstacle"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("clear_obstacle_target_tile_required", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileExpandsPlantSeedIntoVerifiedSingleTileStep()
    {
        var snapshot = PlantingSnapshot(allowPlanting: true);
        var request = Request(snapshot.StateHash, "executor.plant_seed");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "seed_id", Value = "472" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(";", queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors))));
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.plant_seed", item.OptionId);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("plant_seed", step.StepType);
        Assert.Equal("current_location(64,15):472", step.Target);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "seed_id" && parameter.Value == "472");
    }

    [Fact]
    public void CompileBlocksPlantSeedWhenTransparentPlantingContextRejectsTile()
    {
        var snapshot = PlantingSnapshot(allowPlanting: false);
        var request = Request(snapshot.StateHash, "executor.plant_seed");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "seed_id", Value = "472" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("plant_seed_not_allowed_by_transparent_context", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileHarvestCropBuildsVerifiedTileStep()
    {
        var snapshot = HarvestSnapshot(readyForHarvest: true);
        var request = Request(snapshot.StateHash, "executor.harvest_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "harvest_method", Value = "Grab" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.DoesNotContain("harvest_crop_not_ready_by_transparent_farm_state", item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("harvest_crop", step.StepType);
        Assert.Equal("current_location(7,8):Grab", step.Target);
        Assert.Equal("current_location.crops[7,8].ready_for_harvest=false_or_blocked", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksHarvestCropWhenTransparentCropIsNotReady()
    {
        var snapshot = HarvestSnapshot(readyForHarvest: false);
        var request = Request(snapshot.StateHash, "executor.harvest_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "harvest_method", Value = "Grab" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_crop_not_ready_by_transparent_farm_state", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksGrabHarvestCropWhenInventoryCannotAcceptYield()
    {
        var snapshot = HarvestSnapshot(readyForHarvest: true, harvestMethod: "Grab", inventoryHasEmptySlot: false);
        var request = Request(snapshot.StateHash, "executor.harvest_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "harvest_method", Value = "Grab" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_crop_inventory_cannot_accept_grab_yield", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileDoesNotBlockScytheHarvestCropWhenInventoryIsFull()
    {
        var snapshot = HarvestSnapshot(readyForHarvest: true, harvestMethod: "Scythe", inventoryHasEmptySlot: false);
        var request = Request(snapshot.StateHash, "executor.harvest_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "harvest_method", Value = "Scythe" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("harvest_crop_inventory_cannot_accept_grab_yield", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileHarvestGiantCropBuildsVerifiedResourceClumpStep()
    {
        var snapshot = GiantCropSnapshot(isGiantCrop: true);
        var request = Request(snapshot.StateHash, "executor.harvest_giant_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "9" },
            new SmallModelActionParameter { Name = "stand_tile_x", Value = "6" },
            new SmallModelActionParameter { Name = "stand_tile_y", Value = "9" },
            new SmallModelActionParameter { Name = "resource_clump_tile_x", Value = "7" },
            new SmallModelActionParameter { Name = "resource_clump_tile_y", Value = "8" },
            new SmallModelActionParameter { Name = "resource_clump_width", Value = "3" },
            new SmallModelActionParameter { Name = "resource_clump_height", Value = "3" },
            new SmallModelActionParameter { Name = "resource_clump_parent_sheet_index", Value = "190" },
            new SmallModelActionParameter { Name = "target_runtime_type", Value = "StardewValley.TerrainFeatures.GiantCrop" },
            new SmallModelActionParameter { Name = "tool_slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "required_tool_kind", Value = "axe" },
            new SmallModelActionParameter { Name = "max_tool_swings", Value = "3" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.harvest_giant_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("harvest_giant_crop", step.StepType);
        Assert.Equal("current_location(7,9):axe", step.Target);
        Assert.Equal("current_location.resource_clumps[7,9].is_giant_crop=false_or_blocked", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksHarvestGiantCropWhenTransparentClumpIsNotGiantCrop()
    {
        var snapshot = GiantCropSnapshot(isGiantCrop: false);
        var request = Request(snapshot.StateHash, "executor.harvest_giant_crop");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "8" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "9" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_giant_crop_not_verified_by_transparent_resource_clump", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompilePickupDebrisBuildsVerifiedDebrisStep()
    {
        var snapshot = DebrisSnapshot(inventoryHasEmptySlot: true);
        var request = Request(snapshot.StateHash, "executor.pickup_debris");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "65" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "debris_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)388" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.pickup_debris", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("pickup_debris", step.StepType);
        Assert.Equal("Farm(65,15):debris_index=0", step.Target);
        Assert.Contains("current_location.debris[0].chunk_count_decreases_or_removed=true", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksPickupDebrisWhenInventoryCannotAcceptItem()
    {
        var snapshot = DebrisSnapshot(inventoryHasEmptySlot: false);
        var request = Request(snapshot.StateHash, "executor.pickup_debris");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "65" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "debris_index", Value = "0" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("pickup_debris_inventory_cannot_accept_item", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksPickupDebrisWhenProjectedItemIdentityDrifts()
    {
        var snapshot = DebrisSnapshot(inventoryHasEmptySlot: true);
        var request = Request(snapshot.StateHash, "executor.pickup_debris");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "65" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "debris_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)390" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "pickup_debris_item_identity_drifted",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCollectMachineOutputBuildsVerifiedMachineStep()
    {
        var snapshot = MachineOutputSnapshot(inventoryHasEmptySlot: true);
        var request = Request(snapshot.StateHash, "executor.collect_machine_output");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" },
            new SmallModelActionParameter { Name = "machine_location_id", Value = "Farm" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)388" },
            new SmallModelActionParameter { Name = "machine_harvest_experience_raw", Value = "" },
            new SmallModelActionParameter { Name = "expected_skill_experience_deltas_json", Value = "[]" },
            new SmallModelActionParameter { Name = "expected_mastery_experience_delta", Value = "0" },
            new SmallModelActionParameter { Name = "skill_experience_projection_status", Value = "exact_no_configured_experience" },
            new SmallModelActionParameter { Name = "skill_experience_condition", Value = "native_machine_output_collection" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.collect_machine_output", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("collect_machine_output", step.StepType);
        Assert.Equal("Farm(64,15):(O)388", step.Target);
        Assert.Contains("farm.machines[Farm:64,15].held_item=null", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksCollectMachineOutputWhenInventoryCannotAcceptItem()
    {
        var snapshot = MachineOutputSnapshot(inventoryHasEmptySlot: false);
        var request = Request(snapshot.StateHash, "executor.collect_machine_output");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" },
            new SmallModelActionParameter { Name = "machine_location_id", Value = "Farm" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)388" },
            new SmallModelActionParameter { Name = "machine_harvest_experience_raw", Value = "" },
            new SmallModelActionParameter { Name = "expected_skill_experience_deltas_json", Value = "[]" },
            new SmallModelActionParameter { Name = "expected_mastery_experience_delta", Value = "0" },
            new SmallModelActionParameter { Name = "skill_experience_projection_status", Value = "exact_no_configured_experience" },
            new SmallModelActionParameter { Name = "skill_experience_condition", Value = "native_machine_output_collection" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("collect_machine_output_inventory_cannot_accept_item", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileLoadMachineInputBuildsVerifiedMachineStep()
    {
        var snapshot = MachineInputSnapshot(includeInputProbe: true);
        var request = Request(snapshot.StateHash, "executor.load_machine_input");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" },
            new SmallModelActionParameter { Name = "machine_location_id", Value = "Farm" },
            new SmallModelActionParameter { Name = "input_slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)262" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.load_machine_input", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("load_machine_input", step.StepType);
        Assert.Equal("Farm(64,15):slot0:(O)262", step.Target);
        Assert.Contains("player.inventory[0].stack_decreases", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksLoadMachineInputWhenProbeCandidateIsMissing()
    {
        var snapshot = MachineInputSnapshot(includeInputProbe: false);
        var request = Request(snapshot.StateHash, "executor.load_machine_input");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" },
            new SmallModelActionParameter { Name = "machine_location_id", Value = "Farm" },
            new SmallModelActionParameter { Name = "input_slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)262" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("load_machine_input_not_verified_by_transparent_probe", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksLoadMachineInputWhenPredictionIsNotExactForTraining()
    {
        var snapshot = MachineInputSnapshot(
            includeInputProbe: true,
            predictionTrainingStatus: "blocked_requires_special_machine_model");
        var request = Request(snapshot.StateHash, "executor.load_machine_input");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "target_location", Value = "Farm" },
            new SmallModelActionParameter { Name = "machine_location_id", Value = "Farm" },
            new SmallModelActionParameter { Name = "input_slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)262" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "load_machine_input_prediction_not_trainable",
            queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompilePlanWaterCropStepOnlyTargetsRequestedCropTile()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":6,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "crops": {"value":[
              {"tile_x":1,"tile_y":2,"needs_watering":true},
              {"tile_x":5,"tile_y":6,"needs_watering":true}
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":80,"height":65,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            StateHash = snapshot.StateHash,
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "water.5.6",
                    Kind = "water_crop",
                    TargetLocation = "Farm",
                    TargetTileX = 5,
                    TargetTileY = 6
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.water_crop", item.OptionId);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("water_crop", step.StepType);
        Assert.Equal("Farm(5,6)", step.Target);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_y" && parameter.Value == "6");
    }

    [Fact]
    public void CompileBlocksCropMaintenanceWithoutSeasonAndWeatherHardRuleContext()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("missing_required_state", queue.Items[0].BlockingReasons);
        Assert.Contains(queue.Items[0].MissingStateFactors, factor => factor == "time.season");
        Assert.Contains(queue.Items[0].MissingStateFactors, factor => factor == "time.weather");
    }

    [Fact]
    public void CompileBlocksBuySuppliesWithoutCropCatalogForSeedSeasonRules()
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
          },
          "farm": {}
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "economy.buy_supplies"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("farm.crop_catalog", queue.Items[0].MissingStateFactors);
        Assert.Contains("player.seed_inventory", queue.Items[0].MissingStateFactors);
        Assert.Contains("missing_required_state", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBuyShopItemPreservesRuntimePurchaseParameters()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{
              "shop_id":"Blacksmith",
              "read_only":false,
              "safety_timer":0,
              "held_item_present":false,
              "executor_purchase_enabled":true,
              "entries":[{
                "item_id":"378",
                "qualified_item_id":"(O)378",
                "price":75,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":true
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.buy_shop_item");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)378" },
            new SmallModelActionParameter { Name = "quantity", Value = "1" },
            new SmallModelActionParameter { Name = "max_unit_price", Value = "75" },
            new SmallModelActionParameter { Name = "expected_shop_id", Value = "Blacksmith" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        var item = Assert.Single(queue.Items);
        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal("executor.buy_shop_item", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "qualified_item_id" && parameter.Value == "(O)378");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "shop_item_id" && parameter.Value == "378");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "quantity" && parameter.Value == "1");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "max_unit_price" && parameter.Value == "75");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "expected_shop_id" && parameter.Value == "Blacksmith");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("buy_shop_item", step.StepType);
        Assert.Equal("(O)378x1", step.Target);
    }

    [Fact]
    public void CompileBuyShopItemBlocksWhenExpectedShopDoesNotMatchOpenMenu()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{
              "shop_id":"Blacksmith",
              "read_only":false,
              "safety_timer":0,
              "held_item_present":false,
              "executor_purchase_enabled":true,
              "entries":[{
                "item_id":"378",
                "qualified_item_id":"(O)378",
                "price":75,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":true
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.buy_shop_item");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)378" },
            new SmallModelActionParameter { Name = "quantity", Value = "1" },
            new SmallModelActionParameter { Name = "max_unit_price", Value = "75" },
            new SmallModelActionParameter { Name = "expected_shop_id", Value = "SeedShop" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("shop_menu_id_mismatch", queue.Items[0].BlockingReasons);
    }

}
