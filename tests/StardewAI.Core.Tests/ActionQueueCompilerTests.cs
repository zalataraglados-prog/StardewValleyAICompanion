using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueCompilerTests
{
    [Fact]
    public void CompileTurnsRegisteredSmallModelActionIntoPendingQueueItem()
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

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("action_queue.v1", queue.SchemaVersion);
        Assert.Equal("pending", queue.Status);
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
        Assert.Single(queue.Items[0].NormalizedCommand.Steps);
        Assert.Equal("crop_maintenance_noop", queue.Items[0].NormalizedCommand.Steps[0].StepType);
        Assert.Equal("training_farmer.main", queue.Items[0].NormalizedCommand.Actor.ActorId);
    }

    [Fact]
    public void CompileExpandsCropMaintenanceIntoPerCropStepsFromTransparentState()
    {
        var snapshot = Snapshot("""
        {
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
