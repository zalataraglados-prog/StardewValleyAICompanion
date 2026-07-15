using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueCompilerTests
{
    [Fact]
    public void CompileTurnsRegisteredSmallModelActionIntoPendingQueueItem()
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
        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal("training_singleplayer", queue.ExecutionMode);
        Assert.Equal("training_farmer.main", queue.Actor.ActorId);
        Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Items[0].Status);
        Assert.Equal("mechanical", queue.Items[0].BehaviorCategory);
        Assert.Equal("full_action_expansion", queue.Items[0].CompilerResponsibility);
        Assert.Equal("executor_calibration", queue.Items[0].TrainingRole);
        Assert.Equal("farm.maintain_crops", queue.Items[0].NormalizedCommand.OptionId);
        Assert.Equal("compiled_action_steps", queue.Items[0].NormalizedCommand.CommandType);
        Assert.Equal("executor_calibration", queue.Items[0].NormalizedCommand.TrainingRole);
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.season" && parameter.Value == "spring");
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.weather" && parameter.Value == "sun");
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.watering_candidate_count" && parameter.Value == "0");
        Assert.Single(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("crop_maintenance_noop", queue.Items[0].NormalizedCommand.Steps[0].StepType);
        Assert.Equal("training_farmer.main", queue.Items[0].NormalizedCommand.Actor.ActorId);
    }

    [Fact]
    public void CompileExpandsCropMaintenanceIntoPerCropStepsFromTransparentState()
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
            "crops": {"value":[
              {"tile_x":1,"tile_y":2,"needs_watering":true},
              {"tile_x":3,"tile_y":4,"needs_watering":false},
              {"tile_x":5,"tile_y":6,"needs_watering":true}
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);

        var steps = queue.Items[0].NormalizedCommand.Steps;
        Assert.Equal(2, steps.Length);
        Assert.All(steps, step => Assert.Equal("water_crop", step.StepType));
        Assert.Contains(steps, step => step.Target == "Farm(1,2)");
        Assert.Contains(steps, step => step.Target == "Farm(5,6)");
        Assert.All(steps, step => Assert.Contains("native_tool=WateringCan", step.ExpectedEffect));
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.crop_count" && parameter.Value == "3");
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.watering_candidate_count" && parameter.Value == "2");
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

        Assert.Equal("pending", queue.Status);
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
        Assert.Equal("Farm(7,8):Grab", step.Target);
        Assert.Equal("farm.crops[7,8].ready_for_harvest=false_or_blocked", step.ExpectedEffect);
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
            new SmallModelActionParameter { Name = "target_tile_x", Value = "8" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "9" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.harvest_giant_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("harvest_giant_crop", step.StepType);
        Assert.Equal("Farm(8,9):axe", step.Target);
        Assert.Equal("farm.resource_clumps[8,9].is_giant_crop=false_or_blocked", step.ExpectedEffect);
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
        Assert.Contains("farm.debris[0].chunk_count_decreases_or_removed=true", step.ExpectedEffect);
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
    public void CompileCollectMachineOutputBuildsVerifiedMachineStep()
    {
        var snapshot = MachineOutputSnapshot(inventoryHasEmptySlot: true);
        var request = Request(snapshot.StateHash, "executor.collect_machine_output");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "64" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "15" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)388" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.collect_machine_output", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("collect_machine_output", step.StepType);
        Assert.Equal("Farm(64,15):(O)388", step.Target);
        Assert.Contains("farm.machines[64,15].held_item=null", step.ExpectedEffect);
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
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)388" }
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
            new SmallModelActionParameter { Name = "input_slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)262" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("load_machine_input_not_verified_by_transparent_probe", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompilePlanCropMaintenanceStepOnlyTargetsRequestedCropTile()
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
            "crops": {"value":[
              {"tile_x":1,"tile_y":2,"needs_watering":true},
              {"tile_x":5,"tile_y":6,"needs_watering":true}
            ],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
                    Kind = "maintain_crops",
                    TargetLocation = "Farm",
                    TargetTileX = 5,
                    TargetTileY = 6,
                    Parameters = new[] { new SmallModelActionParameter { Name = "max_crops", Value = "1" } }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("farm.maintain_crops", item.OptionId);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("water_crop", step.StepType);
        Assert.Equal("Farm(5,6)", step.Target);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "5");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_y" && parameter.Value == "6");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "max_crops" && parameter.Value == "1");
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

    [Fact]
    public void CompileTurnsSmallModelMovePlanIntoExecutorMoveQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sun","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":42,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":23,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"width":70,"height":46},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.move",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            PlanType = "mechanical_plan",
            CandidateAudit = new[]
            {
                new SmallModelPlanCandidateAudit
                {
                    CandidateId = "move:test",
                    Kind = "route_connector_tile",
                    Decision = "accepted",
                    Reasons = new[] { "fits_aggregate_budget" },
                    CandidateMinutes = 1,
                    CandidateEnergyCost = 0
                }
            },
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "plan.step.move.left",
                    Kind = "move_to_tile",
                    TargetLocation = "FarmHouse",
                    TargetTileX = 41,
                    TargetTileY = 23,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "world_ready" },
                    ExpectedEffects = new[] { "player_reaches_target_tile_or_blocked" },
                    SafetyConstraints = new[] { "collision_safe_step_required" },
                    FailurePolicy = new[] { "record_executor_calibration" }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal("plan.test.move", queue.SourceModelOutputId);
        var audit = Assert.Single(queue.CandidateAudit);
        Assert.Equal("move:test", audit.CandidateId);
        Assert.Equal("accepted", audit.Decision);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.move_to_tile", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Equal("compiled_action_steps", item.NormalizedCommand.CommandType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_x" && parameter.Value == "41");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "target_tile_y" && parameter.Value == "23");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "estimated_minutes" && parameter.Value == "1");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "precondition" && parameter.Value == "world_ready");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "expected_effect" && parameter.Value == "player_reaches_target_tile_or_blocked");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safety_constraint" && parameter.Value == "collision_safe_step_required");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "failure_policy" && parameter.Value == "record_executor_calibration");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("move_to_tile", step.StepType);
        Assert.Equal("FarmHouse(41,23)", step.Target);
        Assert.Equal(60, step.EstimatedTicks);
    }

    [Fact]
    public void CompilePlanMoveToTileInsertsClearObstacleRepairWhenPathSegmentIsClearable()
    {
        var snapshot = Snapshot("""
        {
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
            "map": {"value":{"id":"Farm","width":4,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.blocked",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 3,
                TargetTileY = 0,
                EstimatedMinutes = 2
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal(3, queue.Items.Length);
        Assert.Equal("executor.move_to_tile", queue.Items[0].OptionId);
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "0");
        Assert.Equal("executor.clear_obstacle", queue.Items[1].OptionId);
        Assert.Contains(queue.Items[1].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "1");
        Assert.Contains(queue.Items[1].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_y" && parameter.Value == "0");
        Assert.Equal("executor.move_to_tile", queue.Items[2].OptionId);
        Assert.Contains(queue.Items[2].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "3");
    }

    [Fact]
    public void CompilePlanMoveToTileUsesFractionalPlayerEnergyForRepairBudget()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":2.1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":4,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.fractional.energy",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 3,
                TargetTileY = 0,
                EstimatedMinutes = 2
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal(3, queue.Items.Length);
        Assert.Equal("executor.clear_obstacle", queue.Items[1].OptionId);
        Assert.Contains(queue.Items[1].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
    }

    [Fact]
    public void CompilePlanMoveToTileKeepsTileReadsIntegerStrictWithFractionalTile()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0.5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":4,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.fractional.tile",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 3,
                TargetTileY = 0,
                EstimatedMinutes = 2
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Single(queue.Items);
        Assert.Equal("executor.move_to_tile", queue.Items[0].OptionId);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
    }

    [Fact]
    public void CompilePlanMoveToTileCanInsertMultipleClearObstacleRepairsWithinBudget()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"},{"tile_x":2,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":5,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":5,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true},{"tile_x":2,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.two.blocked",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 4,
                TargetTileY = 0,
                EstimatedMinutes = 4
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal(5, queue.Items.Length);
        Assert.Equal("executor.clear_obstacle", queue.Items[1].OptionId);
        Assert.Contains(queue.Items[1].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "1");
        Assert.Equal("executor.clear_obstacle", queue.Items[3].OptionId);
        Assert.Contains(queue.Items[3].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "2");
        Assert.Equal("executor.move_to_tile", queue.Items[4].OptionId);
        Assert.Contains(queue.Items[4].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "target_tile_x" && parameter.Value == "4");
    }

    [Fact]
    public void CompilePlanMoveToTileDoesNotInsertPartialRepairWhenBudgetCannotReachTarget()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"},{"tile_x":2,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":5,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":5,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true},{"tile_x":2,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.two.blocked",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 4,
                TargetTileY = 0,
                EstimatedMinutes = 1,
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "max_route_repair_clears", Value = "1" }
                }
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Single(queue.Items);
        Assert.Equal("executor.move_to_tile", queue.Items[0].OptionId);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
    }

    [Fact]
    public void CompilePlanMoveToTileDoesNotInsertRepairWhenRouteRepairTimeBudgetIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":4,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.time.budget",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 3,
                TargetTileY = 0,
                EstimatedMinutes = 1,
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "max_route_repair_minutes", Value = "1" }
                }
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Single(queue.Items);
        Assert.Equal("executor.move_to_tile", queue.Items[0].OptionId);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
    }

    [Fact]
    public void CompilePlanMoveToTileDoesNotInsertRepairWhenPlayerEnergyIsInsufficient()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":1,"tile_y":0,"qualified_item_id":"(O)343","name":"Stone"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":4,"height":1},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":4,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.energy.budget",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 3,
                TargetTileY = 0,
                EstimatedMinutes = 2
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Single(queue.Items);
        Assert.Equal("executor.move_to_tile", queue.Items[0].OptionId);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Parameters, parameter =>
            parameter.Name == "precondition" && parameter.Value == "compiler_inserted_move_route_repair=true");
    }

    [Fact]
    public void CompileTurnsConnectorPlanIntoTraverseConnectorQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":26,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":31,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"width":70,"height":46},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "warps": {"value":[{"x":27,"y":31,"target_location":"Farm","target_x":64,"target_y":15}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_connectors": {"value":[{"from_location":"FarmHouse","from_x":27,"from_y":31,"to_location":"Farm","to_x":64,"to_y":15,"kind":"warp"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.connector",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            PlanType = "mechanical_plan",
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "plan.step.farmhouse.to.farm",
                    Kind = "traverse_connector",
                    TargetTileX = 27,
                    TargetTileY = 31,
                    EstimatedMinutes = 1,
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "connector_kind", Value = "warp" },
                        new SmallModelActionParameter { Name = "expected_target_location", Value = "Farm" },
                        new SmallModelActionParameter { Name = "expected_arrival_tile_x", Value = "64" },
                        new SmallModelActionParameter { Name = "expected_arrival_tile_y", Value = "15" }
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Farm");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("traverse_connector", step.StepType);
        Assert.Equal("current_location(27,31)", step.Target);
        Assert.Equal("location=Farm;player.tile=64,15", step.ExpectedEffect);
    }

    [Fact]
    public void CompileTurnsInteractPlanIntoExecutorInteractQueueItem()
    {
        var snapshot = Snapshot("""
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
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"OpenShop","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.interact",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
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
                    StepId = "plan.step.interact.shop",
                    Kind = "interact",
                    TargetTileX = 11,
                    TargetTileY = 10,
                    Parameters = new[]
                    {
                        new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
                        new SmallModelActionParameter { Name = "expected_action_type", Value = "OpenShop" }
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.interact", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Equal("compiled_action_steps", item.NormalizedCommand.CommandType);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("interact", step.StepType);
        Assert.Equal("current_location(11,10)", step.Target);
    }

    [Fact]
    public void CompileAllowsPlanPurchaseAfterPreviousShopOpeningStep()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"is_open":false,"prompt_text":"","yes_response_key":""},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{
              "shop_id":"SeedShop",
              "read_only":false,
              "safety_timer":0,
              "held_item_present":false,
              "executor_purchase_enabled":true,
              "entries":[{
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":true
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"OpenShop","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.open.seedshop",
                Kind = "interact",
                TargetTileX = 11,
                TargetTileY = 10,
                ExpectedEffects = new[] { "menus.active_menu.is_open=true", "interact_map_action_OpenShop" },
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
                    new SmallModelActionParameter { Name = "expected_action_type", Value = "OpenShop" }
                }
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.buy.parsnip_seeds",
                Kind = "buy_shop_item",
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)472" },
                    new SmallModelActionParameter { Name = "quantity", Value = "1" },
                    new SmallModelActionParameter { Name = "expected_shop_id", Value = "SeedShop" }
                }
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal(2, queue.Items.Length);
        var purchase = queue.Items[1];
        Assert.Equal("executor.buy_shop_item", purchase.OptionId);
        Assert.DoesNotContain("shop_menu_not_open", purchase.BlockingReasons);
        Assert.Contains(purchase.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "compiler_context.active_menu_type_before_step" && parameter.Value == "ShopMenu");
        Assert.Contains(purchase.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "expected_shop_id" && parameter.Value == "SeedShop");
    }

    [Fact]
    public void CompileAllowsPreviewPurchaseAfterPreviousShopOpeningStepWithRuntimeRecheck()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"OpenShop","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.open.seedshop",
                Kind = "interact",
                TargetTileX = 11,
                TargetTileY = 10,
                ExpectedEffects = new[] { "menus.active_menu.is_open=true", "interact_map_action_OpenShop" },
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
                    new SmallModelActionParameter { Name = "expected_action_type", Value = "OpenShop" }
                }
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.buy.parsnip_seeds",
                Kind = "buy_shop_item",
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)472" },
                    new SmallModelActionParameter { Name = "shop_item_id", Value = "472" },
                    new SmallModelActionParameter { Name = "quantity", Value = "1" },
                    new SmallModelActionParameter { Name = "max_unit_price", Value = "20" },
                    new SmallModelActionParameter { Name = "expected_shop_id", Value = "SeedShop" }
                }
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        var purchase = queue.Items[1];
        Assert.Equal("executor.buy_shop_item", purchase.OptionId);
        Assert.DoesNotContain("menus_shop_stock_unavailable", purchase.BlockingReasons);
        Assert.Contains(purchase.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "compiler_context.runtime_shop_stock_recheck_required" && parameter.Value == "true");
    }

    [Fact]
    public void CompileAllowsCloseMenuAfterPreviewPurchaseStep()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"is_open":false,"prompt_text":"","yes_response_key":""},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{
              "shop_id":"SeedShop",
              "read_only":false,
              "safety_timer":0,
              "held_item_present":false,
              "executor_purchase_enabled":true,
              "entries":[{
                "item_id":"472",
                "qualified_item_id":"(O)472",
                "price":20,
                "stock":2147483647,
                "infinite_stock":true,
                "can_buy_item":true,
                "can_afford_one_with_currency":true,
                "can_afford_one_with_trade_item":true,
                "could_inventory_accept":true,
                "executor_purchase_enabled":true
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"OpenShop","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.open.seedshop",
                Kind = "interact",
                TargetTileX = 11,
                TargetTileY = 10,
                ExpectedEffects = new[] { "menus.active_menu.is_open=true", "interact_map_action_OpenShop" },
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
                    new SmallModelActionParameter { Name = "expected_action_type", Value = "OpenShop" }
                }
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.buy.parsnip_seeds",
                Kind = "buy_shop_item",
                Parameters = new[]
                {
                    new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)472" },
                    new SmallModelActionParameter { Name = "quantity", Value = "1" },
                    new SmallModelActionParameter { Name = "expected_shop_id", Value = "SeedShop" }
                }
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.close.shop",
                Kind = "close_menu"
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(queue.Status == "pending", string.Join("|", queue.Items.SelectMany(item => item.BlockingReasons)));
        Assert.Equal(3, queue.Items.Length);
        var close = queue.Items[2];
        Assert.Equal("executor.close_menu", close.OptionId);
        Assert.DoesNotContain("close_menu_type_unknown", close.BlockingReasons);
        Assert.DoesNotContain("close_menu_type_not_whitelisted", close.BlockingReasons);
        Assert.Contains(close.NormalizedCommand.Parameters, parameter =>
            parameter.Name == "compiler_context.active_menu_type_before_step" && parameter.Value == "ShopMenu");
    }

    [Fact]
    public void CompileTurnsTerminalSleepPlanStepIntoCompilerOwnedSleepMacro()
    {
        var snapshot = SleepSnapshot();
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.sleep", item.OptionId);
        Assert.Equal("compiled_action_steps", item.NormalizedCommand.CommandType);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.is_terminal_step" && parameter.Value == "true");
        var steps = item.NormalizedCommand.Steps;
        Assert.Equal(3, steps.Length);
        Assert.Equal("move_to_bed_adjacent", steps[0].StepType);
        Assert.Equal("FarmHouse(42,23)", steps[0].Target);
        Assert.Equal("step_onto_sleep_touch_tile", steps[1].StepType);
        Assert.Equal("FarmHouse(43,23)", steps[1].Target);
        Assert.Equal("TouchAction=Sleep;menus.sleep_prompt_context.prompt_open=true", steps[1].ExpectedEffect);
        Assert.Equal("confirm_sleep_yes", steps[2].StepType);
        Assert.Equal("menus.sleep_prompt_context", steps[2].Target);
        Assert.Equal("day_safely_ended", steps[2].ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksSleepPlanStepWhenItIsNotTerminal()
    {
        var snapshot = SleepSnapshot();
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Equal("executor.sleep", queue.Items[0].OptionId);
        Assert.Contains("sleep_action_must_be_terminal", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksDirectSleepActionWithoutTerminalPlanContext()
    {
        var snapshot = SleepSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.sleep"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_action_must_be_terminal", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileRecoveryLateNightAutoAppendsCompilerOwnedSleepMacro()
    {
        var snapshot = SleepSnapshot();

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("recovery.stabilize_day", item.OptionId);
        var steps = item.NormalizedCommand.Steps;
        Assert.Equal(3, steps.Length);
        Assert.Equal("move_to_bed_adjacent", steps[0].StepType);
        Assert.Equal("FarmHouse(42,23)", steps[0].Target);
        Assert.Equal("step_onto_sleep_touch_tile", steps[1].StepType);
        Assert.Equal("FarmHouse(43,23)", steps[1].Target);
        Assert.Equal("confirm_sleep_yes", steps[2].StepType);
    }

    [Fact]
    public void CompileRecoveryLateNightBlocksWhenSleepTargetIsUnavailable()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":42,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":23,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"entry_tile_x":27,"entry_tile_y":30,"bed_tile_x":43,"bed_tile_y":23,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":70,"height":46,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_target_unavailable", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileRecoveryLateNightDoesNotUseActiveObjectAsSleepGate()
    {
        var snapshot = SleepSnapshot("(O)472", sleepPromptOpen: false);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("sleep_interact_active_object_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.Contains(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileRecoveryLateNightBlocksWhenSleepPromptIsAlreadyOpen()
    {
        var snapshot = SleepSnapshot("", sleepPromptOpen: true);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_confirm_executor_requires_compiler_terminal_macro", queue.Items[0].BlockingReasons);
    }

    [Theory]
    [InlineData("GameMenu")]
    [InlineData("InventoryMenu")]
    [InlineData("OptionsPage")]
    [InlineData("DialogueBox")]
    public void CompileRecoveryLateNightBlocksSleepMacroWhenAnyMenuIsOpen(string menuType)
    {
        var snapshot = SleepSnapshot(activeMenuOpen: true, activeMenuType: menuType);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "recovery.stabilize_day"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_prompt_menu_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "move_to_bed_adjacent");
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileTerminalSleepBlocksWhenInventoryMenuIsOpen()
    {
        var snapshot = SleepSnapshot(activeMenuOpen: true, activeMenuType: "InventoryMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.sleep",
                Kind = "sleep"
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("sleep_prompt_menu_must_be_clear", queue.Items[0].BlockingReasons);
        Assert.DoesNotContain(queue.Items[0].NormalizedCommand.Steps, step => step.StepType == "step_onto_sleep_touch_tile");
    }

    [Fact]
    public void CompileTurnsFaceDirectionPlanIntoExecutorPrimitiveQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.face",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
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
                    StepId = "plan.step.face.down",
                    Kind = "face_direction",
                    Direction = 2,
                    EstimatedMinutes = 1,
                    ExpectedEffects = new[] { "player_faces_down_or_blocked" }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.face_direction", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "direction" && parameter.Value == "2");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("face_direction", step.StepType);
        Assert.Equal("2", step.Target);
        Assert.Equal("player_facing_direction_changed", step.ExpectedEffect);
    }

    [Fact]
    public void CompileTurnsWaitTicksPlanIntoExecutorPrimitiveQueueItem()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = "plan.test.wait",
            SourceModel = "small-model.test",
            StateHash = snapshot.StateHash,
            GoalId = "goal.autonomous.singleplayer",
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
                    StepId = "plan.step.wait.30",
                    Kind = "wait_ticks",
                    WaitTicks = 30,
                    EstimatedMinutes = 1,
                    ExpectedEffects = new[] { "ticks_elapsed_or_blocked" }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        Assert.Equal("executor_calibration", item.TrainingRole);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "wait_ticks" && parameter.Value == "30");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("wait_ticks", step.StepType);
        Assert.Equal("30", step.Target);
        Assert.Equal("ticks_elapsed_without_mutation", step.ExpectedEffect);
    }

    [Fact]
    public void CompileSelectSafeItemSlotUsesTransparentSafeSlotContext()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "current_tool_index": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"(O)472","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "safe_item_context": {"value":{"active_object_selected":true,"safe_slot_available":true,"safe_slot_index":10,"safe_slot_kind":"empty"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.select_safe_item_slot"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.select_safe_item_slot", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "safe_slot_index" && parameter.Value == "10");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("select_safe_item_slot", step.StepType);
        Assert.Equal("10", step.Target);
        Assert.Equal("player.current_tool_index=10;player.active_object_qualified_id=null", step.ExpectedEffect);
    }

    [Fact]
    public void CompileBlocksSelectSafeItemSlotWhenSafeSlotUnavailable()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "current_tool_index": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"(O)472","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "safe_item_context": {"value":{"active_object_selected":true,"safe_slot_available":false,"safe_slot_index":null,"safe_slot_kind":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.select_safe_item_slot"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("safe_item_slot_unavailable", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuAllowsWhitelistedActiveMenu()
    {
        var snapshot = CloseMenuSnapshot(true, "GameMenu");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.close_menu", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.active_menu_type" && parameter.Value == "GameMenu");
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("close_menu", step.StepType);
        Assert.Equal("active_menu:GameMenu", step.Target);
        Assert.Equal("menus.active_menu.is_open=false", step.ExpectedEffect);
    }

    [Fact]
    public void CompileCloseMenuNoOpsWhenNoMenuIsOpen()
    {
        var snapshot = CloseMenuSnapshot(false, "none");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("pending", queue.Status);
        var step = Assert.Single(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("close_menu", step.StepType);
        Assert.Equal("active_menu:none", step.Target);
    }

    [Fact]
    public void CompileCloseMenuBlocksSleepPrompt()
    {
        var snapshot = CloseMenuSnapshot(true, "DialogueBox", sleepPromptOpen: true);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("close_menu_sleep_prompt_unsupported", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileCloseMenuBlocksUnknownMenuType()
    {
        var snapshot = CloseMenuSnapshot(true, "DialogueBox");

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "executor.close_menu"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("close_menu_type_not_whitelisted", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksNonMenuPlanStepWhenSnapshotMenuIsOpen()
    {
        var snapshot = MenuAndTimeSnapshot(true, "GameMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("active_menu_must_be_closed_before_action", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAllowsPlanStepAfterBalancedCloseMenuStep()
    {
        var snapshot = MenuAndTimeSnapshot(true, "GameMenu");
        var plan = Plan(snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.close",
                Kind = "close_menu"
            },
            new SmallModelPlanStep
            {
                StepId = "plan.step.wait",
                Kind = "wait_ticks",
                WaitTicks = 30
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.close_menu", queue.Items[0].OptionId);
        Assert.Equal("executor.wait_ticks", queue.Items[1].OptionId);
        Assert.Empty(queue.Items[1].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksInteractWithoutTargetTile()
    {
        var snapshot = Snapshot("""
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
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.interact");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
            new SmallModelActionParameter { Name = "expected_action_type", Value = "OpenShop" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("interact_target_tile_required", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksInteractUnsupportedActionBranchAtTarget()
    {
        var snapshot = Snapshot("""
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
            "active_menu": {"value":{"is_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.interact");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "11" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "10" },
            new SmallModelActionParameter { Name = "interaction_kind", Value = "map_action" },
            new SmallModelActionParameter { Name = "expected_action_type", Value = "SkullDoor" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("interact_unsupported_action_branch_at_target", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksVisitLocationWhenTargetTileHasUnsupportedRouteActionBranch()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":12,"tile_y":34,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "12" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "34" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("unsupported_route_action_branch_at_target", queue.Items[0].BlockingReasons);
        Assert.Contains("locations.route_action_branch_coverage", queue.Items[0].RequiredStateFactors);
    }

    [Fact]
    public void CompileAllowsVisitLocationWhenUnsupportedRouteActionBranchIsUnrelatedToTargetTile()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":12,"tile_y":34,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "10" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "10" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("unsupported_route_action_branch_at_target", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksVisitLocationWhenOnlyPathCrossesUnsupportedRouteActionBranch()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":3,"height":1,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":1,"tile_y":0,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "2" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "0" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("unsupported_route_action_branch_on_path", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAllowsVisitLocationWhenPathCanAvoidUnsupportedRouteActionBranch()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":3,"height":2,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":1,"rows":[{"tile_x":1,"tile_y":0,"branch":"SkullDoor","route_training_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "2" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "0" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("unsupported_route_action_branch_on_path", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAllowsVisitLocationWhenRouteGraphHasResolvedCrossMapPath()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"from_location":"Farm","target_location":"BusStop","resolved":true},{"from_location":"BusStop","target_location":"Town","resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Town" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("route_graph_no_resolved_path", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksVisitLocationWhenRouteGraphHasNoResolvedCrossMapPath()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"from_location":"Farm","target_location":"BusStop","resolved":true},{"from_location":"BusStop","target_location":"Town","resolved":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Town" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("route_graph_no_resolved_path", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksVisitLocationWhenCrossMapStartSegmentCannotReachConnector()
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
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":3,"height":1,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"from_location":"Farm","from_x":2,"from_y":0,"target_location":"Town","resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Town" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("route_graph_start_segment_blocked_by_collision_grid", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAllowsVisitLocationWhenCrossMapStartSegmentCanReachConnector()
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
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"width":3,"height":1,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"from_location":"Farm","from_x":2,"from_y":0,"target_location":"Town","resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Town" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.DoesNotContain("route_graph_start_segment_blocked_by_collision_grid", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileAddsRouteMapSummaryContextToVisitLocationPreview()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"from_location":"Farm","target_location":"Town","resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_map_summaries": {"value":{"locations":[{"location_id":"Town","collision_grid_available":false,"segment_validation_status":"pending_per_location_collision_grid"}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "exploration.visit_location");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Town" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.target_location_segment_validation_status" && parameter.Value == "pending_per_location_collision_grid");
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.route_executor_enabled" && parameter.Value == "false");
    }

    [Fact]
    public void CompileAllowsVisitLocationWhenRouteActionCoverageHasNoUnsupportedBranches()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"unsupported_for_route_training_count":0,"rows":[{"branch":"Warp","route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "exploration.visit_location"), snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksUnknownOptionBeforeExecutor()
    {
        var snapshot = Snapshot("{}");
        var request = Request(snapshot.StateHash, "raw.keyboard.click");

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("unknown_option_id", queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksHumanActorBeforeExecutor()
    {
        var snapshot = Snapshot("{}");
        var request = Request(snapshot.StateHash, "farm.maintain_crops");
        request.Actor = new ActionActorRef
        {
            ActorId = "human.local_player",
            ActorType = "human_player",
            ControlSurface = "keyboard_mouse"
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("actor_type_human_player_forbidden", queue.CompilerDiagnostics);
        Assert.Contains("control_surface_keyboard_mouse_forbidden", queue.CompilerDiagnostics);
    }

    [Fact]
    public void CompileAllowsCoopCompanionModeForFutureCompanionActor()
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
        request.ExecutionMode = "coop_companion";
        request.Actor = new ActionActorRef
        {
            ActorId = "ai_companion.main",
            ActorType = "ai_companion",
            ControlSurface = "companion_actor"
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Equal("coop_companion", queue.ExecutionMode);
        Assert.Equal("ai_companion.main", queue.Actor.ActorId);
    }

    [Fact]
    public void DryRunExecutorDoesNotMutateButReturnsExecutionShape()
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
        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);

        var result = new DryRunExecutorPort().Execute(queue);

        Assert.False(new DryRunExecutorPort().ExecutionEnabled);
        Assert.Equal("execution_batch_result.v1", result.SchemaVersion);
        Assert.Equal("dry_run_ready", result.Status);
        Assert.Equal("dry_run_ready", result.Results[0].Status);
    }

    [Fact]
    public void TrainingSandboxExecutorAppliesOnlyTrainingSingleplayerQueue()
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
        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "farm.maintain_crops"), snapshot);

        var result = new TrainingSandboxExecutorPort().Execute(queue);

        Assert.True(new TrainingSandboxExecutorPort().ExecutionEnabled);
        Assert.Equal("training_sandbox", result.ExecutorMode);
        Assert.Equal("applied", result.Status);
        Assert.True(result.FeedbackAvailable);
        Assert.NotEmpty(result.AfterStateHash);
        Assert.Contains("farm.maintain_crops", result.CompletedOptionIds);
    }

    [Fact]
    public void TrainingSandboxExecutorRejectsCoopCompanionQueue()
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
        var request = Request(snapshot.StateHash, "farm.maintain_crops");
        request.ExecutionMode = "coop_companion";
        request.Actor = new ActionActorRef
        {
            ActorId = "ai_companion.main",
            ActorType = "ai_companion",
            ControlSurface = "companion_actor"
        };
        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        var result = new TrainingSandboxExecutorPort().Execute(queue);

        Assert.Equal("blocked", result.Status);
        Assert.Contains(result.Results, item => item.Reason == "training_sandbox_rejected_execution_target");
    }

    [Fact]
    public void StrategyGrandpaProgressRequiresDirectionId()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "total_money_earned": {"value":100000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "grandpa_score": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var queue = new ActionQueueCompiler().Compile(Request(snapshot.StateHash, "strategy.grandpa_progress"), snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("strategy_direction_id_required", queue.Items[0].BlockingReasons);
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    private static SmallModelActionEnvelope Request(string stateHash, string optionId)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output.test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.test",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "action.test",
                    OptionId = optionId,
                    Rationale = "test"
                }
            }
        };
    }

    private static SmallModelPlanEnvelope Plan(string stateHash, params SmallModelPlanStep[] steps)
    {
        return new SmallModelPlanEnvelope
        {
            PlanId = "plan.test",
            SourceModel = "small-model.test",
            StateHash = stateHash,
            GoalId = "goal.autonomous.singleplayer",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Steps = steps
        };
    }

    private static SnapshotEnvelope ClearObstacleSnapshot()
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[{"tile_x":11,"tile_y":10,"qualified_item_id":"(O)343"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"width":80,"height":65},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
    }

    private static SnapshotEnvelope PlantingSnapshot(bool allowPlanting)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[{"slot_index":0,"item_id":"472","qualified_item_id":"(O)472","stack":2,"seed_id":"472"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "planting_context": {"value":{
              "location_id":"Farm",
              "hoe_dirt_tiles":[{
                "tile_x":64,
                "tile_y":15,
                "has_crop":false,
                "seed_results":[{
                  "seed_id":"472",
                  "hard_rule_allows_planting":ALLOW_PLANTING
                }]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("ALLOW_PLANTING", allowPlanting ? "true" : "false"));
    }

    private static SnapshotEnvelope HarvestSnapshot(bool readyForHarvest, string harvestMethod = "Grab", bool inventoryHasEmptySlot = true)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"max_items":2,"occupied_item_stacks":OCCUPIED_STACKS,"empty_slots":EMPTY_SLOTS,"has_empty_slot":HAS_EMPTY_SLOT},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY_VALUE,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[{"tile_x":7,"tile_y":8,"harvest_item_id":"24","harvest_method":"HARVEST_METHOD","ready_for_harvest":READY_FOR_HARVEST,"needs_watering":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("READY_FOR_HARVEST", readyForHarvest ? "true" : "false")
        .Replace("HARVEST_METHOD", harvestMethod)
        .Replace("OCCUPIED_STACKS", inventoryHasEmptySlot ? "1" : "2")
        .Replace("EMPTY_SLOTS", inventoryHasEmptySlot ? "1" : "0")
        .Replace("HAS_EMPTY_SLOT", inventoryHasEmptySlot ? "true" : "false")
        .Replace("INVENTORY_VALUE", inventoryHasEmptySlot
            ? """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}]"""
            : """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"388","qualified_item_id":"(O)388","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false}]"""));
    }

    private static SnapshotEnvelope GiantCropSnapshot(bool isGiantCrop)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "resource_clumps": {"value":[{"tile_x":7,"tile_y":8,"width":3,"height":3,"health":3,"is_giant_crop":IS_GIANT_CROP,"giant_crop_id":"276","required_tool":"axe","executor_status":"blocked_requires_giant_crop_executor"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("IS_GIANT_CROP", isGiantCrop ? "true" : "false"));
    }

    private static SnapshotEnvelope DebrisSnapshot(bool inventoryHasEmptySlot)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":OCCUPIED_STACKS,"empty_slots":EMPTY_SLOTS,"has_empty_slot":HAS_EMPTY_SLOT},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY_VALUE,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "debris": {"value":[{"debris_index":0,"debris_type":"OBJECT","chunk_type":0,"item_id":"(O)388","qualified_item_id":"(O)388","item_quality":0,"chunk_count":1,"chunks":[{"chunk_index":0,"tile_x":65,"tile_y":15,"pixel_x":4160,"pixel_y":960}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("OCCUPIED_STACKS", inventoryHasEmptySlot ? "1" : "2")
        .Replace("EMPTY_SLOTS", inventoryHasEmptySlot ? "1" : "0")
        .Replace("HAS_EMPTY_SLOT", inventoryHasEmptySlot ? "true" : "false")
        .Replace("INVENTORY_VALUE", inventoryHasEmptySlot
            ? """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}]"""
            : """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"382","qualified_item_id":"(O)382","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false}]"""));
    }

    private static SnapshotEnvelope MachineOutputSnapshot(bool inventoryHasEmptySlot)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":OCCUPIED_STACKS,"empty_slots":EMPTY_SLOTS,"has_empty_slot":HAS_EMPTY_SLOT},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY_VALUE,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("OCCUPIED_STACKS", inventoryHasEmptySlot ? "1" : "2")
        .Replace("EMPTY_SLOTS", inventoryHasEmptySlot ? "1" : "0")
        .Replace("HAS_EMPTY_SLOT", inventoryHasEmptySlot ? "true" : "false")
        .Replace("INVENTORY_VALUE", inventoryHasEmptySlot
            ? """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"is_empty":true}]"""
            : """[{"slot_index":0,"item_id":"390","qualified_item_id":"(O)390","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false},{"slot_index":1,"item_id":"382","qualified_item_id":"(O)382","stack":999,"quality":0,"maximum_stack_size":999,"is_empty":false}]"""));
    }

    private static SnapshotEnvelope MachineInputSnapshot(bool includeInputProbe)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"held_item":null,"loadable_inputs":LOADABLE_INPUTS}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("LOADABLE_INPUTS", includeInputProbe
            ? """[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"probe_source":"Object.performObjectDropInAction(probe:true)"}]"""
            : "[]"));
    }

    private static SnapshotEnvelope SleepSnapshot(string activeObjectQualifiedId = "", bool sleepPromptOpen = false, bool activeMenuOpen = false, string activeMenuType = "none")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":42,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":23,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"ACTIVE_OBJECT","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"FarmHouse","current_location_is_home":true,"entry_tile_x":27,"entry_tile_y":30,"bed_tile_x":43,"bed_tile_y":23,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":ACTIVE_MENU_OPEN,"type":"ACTIVE_MENU_TYPE"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":SLEEP_PROMPT_OPEN,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"FarmHouse","width":70,"height":46,"notable_tiles":[{"tile_x":43,"tile_y":23,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("ACTIVE_OBJECT", activeObjectQualifiedId)
        .Replace("ACTIVE_MENU_OPEN", activeMenuOpen ? "true" : "false")
        .Replace("ACTIVE_MENU_TYPE", activeMenuType)
        .Replace("SLEEP_PROMPT_OPEN", sleepPromptOpen ? "true" : "false"));
    }

    private static SnapshotEnvelope CloseMenuSnapshot(bool menuOpen, string menuType, bool sleepPromptOpen = false)
    {
        return Snapshot("""
        {
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN,"type":"MENU_TYPE"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":SLEEP_PROMPT_OPEN,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("MENU_TYPE", menuType)
        .Replace("SLEEP_PROMPT_OPEN", sleepPromptOpen ? "true" : "false"));
    }

    private static SnapshotEnvelope MenuAndTimeSnapshot(bool menuOpen, string menuType)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN,"type":"MENU_TYPE"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false,"can_confirm_sleep":false,"confirm_executor_enabled":false,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("MENU_TYPE", menuType));
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
}
