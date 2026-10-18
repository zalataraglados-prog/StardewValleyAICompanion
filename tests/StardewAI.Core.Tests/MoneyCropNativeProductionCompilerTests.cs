using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class MoneyCropNativeProductionCompilerTests
{
    [Fact]
    public void CropBatchCandidateAndCommitmentSurviveExistingCompiler()
    {
        var snapshot = Snapshot();
        var request = new SmallModelActionEnvelope
        {
            ModelOutputId = "model-output:crop:112:crop.test:1",
            SourceModel = "authoritative_crop_native_run_plan.v1",
            StateHash = snapshot.StateHash,
            GoalId = "daily.closed_loop",
            ExecutionMode = "training_singleplayer",
            Actor = ExecutionTargetProfiles.CreateActor(
                ExecutionTargetProfiles.TrainingSingleplayer),
            Actions =
            [
                new SmallModelAction
                {
                    ActionId = "crop:112:crop.test:1:64:15",
                    OptionId = "executor.plant_seed",
                    Rationale = "authoritative_crop_native_run_plan",
                    Parameters =
                    [
                        Parameter("target_tile_x", "64"),
                        Parameter("target_tile_y", "15"),
                        Parameter("target_location", "Farm"),
                        Parameter("seed_id", "472"),
                        Parameter("action_commitment_id", "crop.test"),
                        Parameter(
                            "precondition",
                            "candidate_id:crop:112:crop.test:1")
                    ]
                }
            ]
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.plant_seed", item.OptionId);
        Assert.Single(item.NormalizedCommand.Steps);
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "action_commitment_id" &&
            value.Value == "crop.test");
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "precondition" &&
            value.Value == "candidate_id:crop:112:crop.test:1");
    }

    [Fact]
    public void CrossLocationCropRoutePreservesCandidateIdentity()
    {
        var snapshot = RouteSnapshot();
        var request = new SmallModelPlanEnvelope
        {
            PlanId = "plan:route:112:crop-ingress:1",
            SourceModel = "authoritative_crop_native_route.v1",
            StateHash = snapshot.StateHash,
            GoalId = "daily.closed_loop",
            ExecutionMode = "training_singleplayer",
            Actor = ExecutionTargetProfiles.CreateActor(
                ExecutionTargetProfiles.TrainingSingleplayer),
            PlanType = "mechanical_plan",
            Steps =
            [
                new SmallModelPlanStep
                {
                    StepId = "route:112:crop-ingress:1:move",
                    Kind = "move_to_tile",
                    TargetLocation = "Farm",
                    TargetTileX = 0,
                    TargetTileY = 0,
                    Preconditions =
                    [
                        "candidate_id:route:112:crop-ingress:1",
                        "world_ready"
                    ]
                }
            ]
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "precondition" &&
            value.Value == "candidate_id:route:112:crop-ingress:1");
        Assert.DoesNotContain(item.NormalizedCommand.Parameters, value =>
            value.Name == "precondition" && value.Value == "world_ready");
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "player": {
                "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "seed_inventory": {"value":[{"slot_index":0,"item_id":"472","qualified_item_id":"(O)472","stack":2,"seed_id":"472"}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus": {
                "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "current_location": {
                "planting_context": {"value":{"location_id":"Farm","hoe_dirt_tiles":[{"tile_x":64,"tile_y":15,"has_crop":false,"seed_results":[{"seed_id":"472","hard_rule_allows_planting":true}]}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "locations": {
                "collision_grid": {"value":{"width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SnapshotEnvelope RouteSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
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
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
