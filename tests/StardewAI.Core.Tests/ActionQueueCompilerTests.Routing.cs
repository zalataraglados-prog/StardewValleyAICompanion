using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
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
    public void CompilePlanMoveToTileSkipsCloserSideObstacleThatDoesNotReduceRequiredClears()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "objects": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "terrain_features": {"value":[{"tile_x":1,"tile_y":0,"type":"Grass"},{"tile_x":3,"tile_y":1,"type":"Grass"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "map": {"value":{"id":"Farm","width":6,"height":2},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":6,"height":2,"notable_tiles":[{"tile_x":1,"tile_y":0,"collision_blocked":true},{"tile_x":2,"tile_y":0,"collision_blocked":true},{"tile_x":3,"tile_y":0,"collision_blocked":true},{"tile_x":4,"tile_y":0,"collision_blocked":true},{"tile_x":5,"tile_y":0,"collision_blocked":true},{"tile_x":3,"tile_y":1,"collision_blocked":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(
            snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.move.skip.side.clear",
                Kind = "move_to_tile",
                TargetLocation = "Farm",
                TargetTileX = 5,
                TargetTileY = 1,
                EstimatedMinutes = 2
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal(3, queue.Items.Length);
        Assert.Equal("executor.clear_obstacle", queue.Items[1].OptionId);
        Assert.Contains(
            queue.Items[1].NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "target_tile_x" &&
                parameter.Value == "3");
        Assert.Contains(
            queue.Items[1].NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "target_tile_y" &&
                parameter.Value == "1");
        Assert.DoesNotContain(
            queue.Items,
            item => item.NormalizedCommand.Parameters.Any(
                parameter =>
                    parameter.Name == "target_tile_x" &&
                    parameter.Value == "1") &&
                item.NormalizedCommand.Parameters.Any(
                    parameter =>
                        parameter.Name == "target_tile_y" &&
                        parameter.Value == "0"));
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
            "collision_grid": {"value":{"location_id":"FarmHouse","width":70,"height":46,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"FarmHouse","connectors":[{"kind":"warp","tile_x":27,"tile_y":31,"target_location":"Farm","target_x":64,"target_y":15,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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
    public void CompileReplacesCrossLocationMoveWithFirstTransparentConnectorAndReplanBoundary()
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
            "collision_grid": {"value":{"location_id":"FarmHouse","width":70,"height":46,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"FarmHouse","connectors":[{"kind":"warp","tile_x":27,"tile_y":31,"target_location":"Farm","target_x":64,"target_y":15,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"kind":"warp","from_location":"FarmHouse","from_x":27,"from_y":31,"target_location":"Farm","target_x":64,"target_y":15,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(snapshot.StateHash, new SmallModelPlanStep
        {
            StepId = "ship.move",
            Kind = "move_to_tile",
            TargetLocation = "Farm",
            TargetTileX = 70,
            TargetTileY = 13
        });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.True(item.Status == "pending", string.Join("|", item.BlockingReasons.Concat(item.MissingStateFactors)));
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "cross_location_move_target" && parameter.Value == "Farm");
    }

    [Fact]
    public void CompileBlocksConnectorPlanWhenTransparentConnectorChanged()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":26,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":31,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"FarmHouse","width":70,"height":46,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"FarmHouse","connectors":[{"kind":"warp","tile_x":27,"tile_y":31,"target_location":"Beach","target_x":20,"target_y":4,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);
        var plan = Plan(
            snapshot.StateHash,
            new SmallModelPlanStep
            {
                StepId = "plan.step.stale.connector",
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
            });

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("connector_not_transparently_confirmed", Assert.Single(queue.Items).BlockingReasons);
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

}
