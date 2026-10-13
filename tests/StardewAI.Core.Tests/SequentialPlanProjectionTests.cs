using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class SequentialPlanProjectionTests
{
    [Fact]
    public void MoveThenInteractUsesCompilerProjectedPlayerTile()
    {
        var snapshot = Snapshot();
        var plan = Plan(
            snapshot.StateHash,
            MoveToCounter(),
            InteractWithCounter());

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(
                "|",
                queue.Items.Select(item =>
                    item.OptionId + ":" +
                    string.Join(",", item.MissingStateFactors) + ":" +
                    string.Join(",", item.BlockingReasons))));
        Assert.Equal(2, queue.Items.Length);
        var interact = queue.Items[1];
        Assert.Equal("executor.interact", interact.OptionId);
        Assert.DoesNotContain(
            "interact_target_not_adjacent",
            interact.BlockingReasons);
        Assert.Contains(
            interact.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name ==
                    "compiler_context.player_tile_x_before_step" &&
                parameter.Value == "3");
        Assert.Contains(
            interact.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name ==
                    "compiler_context.player_tile_y_before_step" &&
                parameter.Value == "15");
    }

    [Fact]
    public void DistantInteractWithoutPrecedingMoveStillFailsClosed()
    {
        var snapshot = Snapshot();
        var plan = Plan(snapshot.StateHash, InteractWithCounter());

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var interact = Assert.Single(queue.Items);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "interact_target_not_adjacent",
            interact.BlockingReasons);
    }

    private static SmallModelPlanStep MoveToCounter() => new()
    {
        StepId = "shop.move_to_counter",
        Kind = "move_to_tile",
        TargetLocation = "Blacksmith",
        TargetTileX = 3,
        TargetTileY = 15,
        EstimatedMinutes = 10
    };

    private static SmallModelPlanStep InteractWithCounter() => new()
    {
        StepId = "shop.interact_with_counter",
        Kind = "interact",
        TargetLocation = "Blacksmith",
        TargetTileX = 3,
        TargetTileY = 14,
        ExpectedEffects = new[]
        {
            "menus.active_menu.is_open=true",
            "DialogueBox",
            "interact_map_action_Blacksmith"
        },
        Parameters = new[]
        {
            new SmallModelActionParameter
            {
                Name = "interaction_kind",
                Value = "map_action"
            },
            new SmallModelActionParameter
            {
                Name = "expected_action_type",
                Value = "Blacksmith"
            }
        }
    };

    private static SmallModelPlanEnvelope Plan(
        string stateHash,
        params SmallModelPlanStep[] steps) => new()
        {
            PlanId = "shop.interaction.projection.test",
            SourceModel = "deterministic.test",
            StateHash = stateHash,
            GoalId = "grandpa.maximum_21",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.main",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            PlanType = "daily_candidate_plan",
            Steps = steps
        };

    private static SnapshotEnvelope Snapshot()
    {
        const string stateJson = """
        {
          "player": {
            "location_id": {"value":"Blacksmith","status":"available"},
            "tile_x": {"value":5,"status":"available"},
            "tile_y": {"value":19,"status":"available"},
            "facing_direction": {"value":0,"status":"available"}
          },
          "current_location": {
            "map": {"value":{"id":"Blacksmith","width":20,"height":25},"status":"available"},
            "objects": {"value":[],"status":"available"},
            "terrain_features": {"value":[],"status":"available"},
            "route_context": {"value":{"probes":[]},"status":"available"}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Blacksmith","width":20,"height":25,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":3,"tile_y":14,"branch":"Blacksmith","route_training_blocked":false}]},"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available"}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            stateJson)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-12T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
