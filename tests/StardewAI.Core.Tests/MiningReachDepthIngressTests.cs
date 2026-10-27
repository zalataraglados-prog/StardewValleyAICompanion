using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MiningReachDepthIngressTests
{
    [Fact]
    public void MineLobbyCompilesNativeFloorOneConnectorThenPreservesDepthIntent()
    {
        var snapshot = Snapshot(
            "Mine",
            "UndergroundMine1",
            "UndergroundMine1");
        var option = Evaluate(snapshot, "100", "ordinary_mines");

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join("|", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("Mine", candidate.LocationId);
        Assert.Equal("ordinary_mine_ingress_route_step", candidate.AvailabilityClass);
        AssertParameter(candidate, "connector_kind", "action_warp");
        AssertParameter(candidate, "expected_target_location", "UndergroundMine1");
        AssertParameter(candidate, "continuation.option_id", "mining.reach_depth");
        AssertParameter(candidate, "continuation.target_depth", "100");
        AssertParameter(candidate, "continuation.target_location_family", "ordinary_mines");

        var ranked = Assert.Single(new EventCandidateRanker().Rank(
            new(),
            new OptionAvailabilityEnvelope
            {
                StateHash = snapshot.StateHash,
                CurrentTime = 900,
                Options = new[] { option }
            }));
        var plan = new DailyPlanCompiler().Compile(
            new[] { ranked },
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", step.Kind);
        AssertParameter(step, "continuation.target_depth", "100");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        AssertParameter(
            item.NormalizedCommand,
            "expected_target_location",
            "UndergroundMine1");
    }

    [Fact]
    public void RemoteLocationUsesExistingRouteToMineBeforeFloorOneEntry()
    {
        var snapshot = Snapshot("Mountain", "Mine", "Mine");
        var candidate = Assert.Single(
            Evaluate(snapshot, "60", "ordinary_mines").EventCandidates);

        Assert.True(candidate.Available, string.Join("|", candidate.BlockReasons));
        Assert.Equal("Mountain", candidate.LocationId);
        AssertParameter(candidate, "expected_target_location", "Mine");
        AssertParameter(candidate, "continuation.target_depth", "60");
    }

    [Fact]
    public void MineIngressRejectsWrongMineFamily()
    {
        var candidate = Assert.Single(
            Evaluate(
                Snapshot("Mine", "UndergroundMine1", "UndergroundMine1"),
                "100",
                "skull_cavern")
            .EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "target_location_family_mismatch_current_mine",
            candidate.BlockReasons);
    }

    [Fact]
    public void MineIngressFailsClosedWhenTransparentEdgeTargetsWrongFloor()
    {
        var candidate = Assert.Single(
            Evaluate(
                Snapshot("Mine", "UndergroundMine2", "UndergroundMine2"),
                "100",
                "ordinary_mines")
            .EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains(
            "ordinary_mine_floor_one_entry_unavailable",
            candidate.BlockReasons);
    }

    private static OptionAvailability Evaluate(
        SnapshotEnvelope snapshot,
        string targetDepth,
        string targetFamily) =>
        new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "mining.reach_depth",
                        Parameters = new[]
                        {
                            Parameter("target_depth", targetDepth),
                            Parameter("target_location_family", targetFamily),
                            Parameter("latest_exit_time", "2400")
                        }
                    }
                })
            .Options[0];

    private static SnapshotEnvelope Snapshot(
        string currentLocation,
        string connectorTarget,
        string graphTarget)
    {
        var connectorKind = currentLocation == "Mine"
            ? "action_warp"
            : "warp";
        var stateJson = $$"""
        {
          "player": {
            "location_id": {"value":"{{currentLocation}}","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"{{currentLocation}}","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":5,"tile_y":5,"route_training_blocked":false}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"{{currentLocation}}","connectors":[{"kind":"{{connectorKind}}","tile_x":5,"tile_y":5,"target_location":"{{connectorTarget}}","target_x":null,"target_y":null,"source_property":"Buildings.Action","resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_gate_context": {"value":{"action_gates":[{"tile_x":5,"tile_y":5,"allowed_now":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[{"kind":"{{connectorKind}}","from_location":"{{currentLocation}}","from_x":5,"from_y":5,"target_location":"{{graphTarget}}","target_x":null,"target_y":null,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            stateJson,
            JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-21T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static void AssertParameter(
        EventCandidate candidate,
        string name,
        string value) =>
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == name && parameter.Value == value);

    private static void AssertParameter(
        SmallModelPlanStep step,
        string name,
        string value) =>
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == name && parameter.Value == value);

    private static void AssertParameter(
        NormalizedCommand command,
        string name,
        string value) =>
        Assert.Contains(command.Parameters, parameter =>
            parameter.Name == name && parameter.Value == value);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
