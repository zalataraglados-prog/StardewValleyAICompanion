using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
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
    public void CompileAllowsObservedAdjacentSkullKeyRewardChestInteraction()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"UndergroundMine120","status":"available"},
            "tile_x": {"value":10,"status":"available"},
            "tile_y": {"value":10,"status":"available"},
            "facing_direction": {"value":1,"status":"available"}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available"}
          },
          "mining": {
            "floor_objectives": {"value":{"skull_key_reward_chests":[{"tile_x":11,"tile_y":10,"contains_skull_key":true,"special_item_which":4,"interaction_kind":"overlay_object","expected_action_type":"SkullKeyChest"}]},"status":"available"},
            "reward_chests": {"value":[],"status":"available"}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available"}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.interact");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "11" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "10" },
            new SmallModelActionParameter { Name = "interaction_kind", Value = "overlay_object" },
            new SmallModelActionParameter { Name = "expected_action_type", Value = "SkullKeyChest" },
            new SmallModelActionParameter { Name = "required_postcondition", Value = "player.has_skull_key=true" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Empty(queue.Items[0].BlockingReasons);
    }

    [Fact]
    public void CompileBlocksSkullKeyChestInteractionWithoutExactTransparentRewardEvidence()
    {
        var snapshot = Snapshot("""
        {
          "player": {
            "location_id": {"value":"UndergroundMine120","status":"available"},
            "tile_x": {"value":10,"status":"available"},
            "tile_y": {"value":10,"status":"available"},
            "facing_direction": {"value":1,"status":"available"}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available"}
          },
          "mining": {
            "floor_objectives": {"value":{"skull_key_reward_chests":[]},"status":"available"},
            "reward_chests": {"value":[],"status":"available"}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available"}
          }
        }
        """);
        var request = Request(snapshot.StateHash, "executor.interact");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_tile_x", Value = "11" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "10" },
            new SmallModelActionParameter { Name = "interaction_kind", Value = "overlay_object" },
            new SmallModelActionParameter { Name = "expected_action_type", Value = "SkullKeyChest" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("skull_key_reward_chest_not_observed_at_target", queue.Items[0].BlockingReasons);
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
        Assert.Contains(queue.Items[0].NormalizedCommand.Parameters, parameter => parameter.Name == "compiler_context.route_executor_enabled" && parameter.Value == "true");
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
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":true,"minutes_until_ready":0,"harvest_experience_raw":"","harvest_experience_entries":[],"harvest_experience_deltas":[],"harvest_experience_deltas_json":"[]","harvest_mastery_experience_delta":0,"harvest_experience_projection_status":"exact_no_configured_experience","held_item":{"item_id":"388","qualified_item_id":"(O)388","stack":1,"quality":0,"maximum_stack_size":999}}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);}
