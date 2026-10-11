using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class CandidateRouteConnectorCacheTests
{
    [Fact]
    public void AutonomousRuntimeDefaultsKeepExplicitTargetOptionsOut()
    {
        var optionIds = new CandidateOptionAvailabilityEvaluator()
            .DefaultAutonomousRuntimeOptionIds();

        Assert.Contains("recovery.stabilize_day", optionIds);
        Assert.DoesNotContain("exploration.visit_location", optionIds);
        Assert.DoesNotContain("executor.wait_ticks", optionIds);
    }

    [Fact]
    public void RouteConnectorCandidatesAreBuiltOncePerSnapshotInstance()
    {
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var snapshot = RouteConnectorSnapshot();

        var first = evaluator.Evaluate(
            snapshot,
            new[] { "exploration.visit_location" });
        var second = evaluator.Evaluate(
            snapshot,
            new[] { "exploration.visit_location" });

        Assert.Equal(1, evaluator.RouteConnectorCandidateBuildCount);
        Assert.Equal(
            first.Options[0].EventCandidates.Select(candidate => candidate.CandidateId),
            second.Options[0].EventCandidates.Select(candidate => candidate.CandidateId));

        evaluator.Evaluate(
            RouteConnectorSnapshot(),
            new[] { "exploration.visit_location" });
        Assert.Equal(2, evaluator.RouteConnectorCandidateBuildCount);
    }

    [Fact]
    public void RouteRepairDoesNotReuseOldCollisionGridAfterStateHashChanges()
    {
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var snapshot = RouteConnectorSnapshot(blocked: true);

        var blocked = evaluator.Evaluate(
            snapshot,
            new[] { "exploration.visit_location" });
        var blockedRoute = Assert.Single(
            blocked.Options[0].EventCandidates,
            candidate => candidate.Kind == "route_connector_tile");
        Assert.Contains(
            "route_path_target_blocked_by_collision_grid",
            blockedRoute.BlockReasons);

        var replacement = RouteConnectorSnapshot(blocked: false);
        snapshot.State = replacement.State;
        snapshot.StateHash = replacement.StateHash;
        var unblocked = evaluator.Evaluate(
            snapshot,
            new[] { "exploration.visit_location" });
        var unblockedRoute = Assert.Single(
            unblocked.Options[0].EventCandidates,
            candidate => candidate.Kind == "route_connector_tile");

        Assert.True(
            unblockedRoute.Available,
            string.Join(";", unblockedRoute.BlockReasons));
        Assert.DoesNotContain(
            unblockedRoute.BlockReasons,
            reason => reason.Contains(
                "collision_grid",
                StringComparison.Ordinal));
        Assert.Equal(2, evaluator.RouteConnectorCandidateBuildCount);
    }

    private static SnapshotEnvelope RouteConnectorSnapshot(
        int connectorX = 2,
        bool blocked = false)
    {
        var terrainFeatures = blocked
            ? $$"""[{"tile_x":{{connectorX}},"tile_y":1,"type":"Grass"}]"""
            : "[]";
        var notableTiles = blocked
            ? $$"""[{"tile_x":{{connectorX}},"tile_y":1,"collision_blocked":true}]"""
            : "[]";
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            $$"""
            {
              "time": {
                "time": {"value":900,"status":"available"}
              },
              "player": {
                "location_id": {"value":"Farm","status":"available"},
                "tile_x": {"value":1,"status":"available"},
                "tile_y": {"value":1,"status":"available"},
                "energy": {"value":270,"status":"available"},
                "inventory": {"value":[],"status":"available"}
              },
              "current_location": {
                "objects": {"value":[],"status":"available"},
                "terrain_features": {"value":{{terrainFeatures}},"status":"available"},
                "map": {"value":{"id":"Farm"},"status":"available"}
              },
              "menus": {
                "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available"}
              },
              "locations": {
                "collision_grid": {"value":{"location_id":"Farm","width":5,"height":5,"notable_tiles":{{notableTiles}}},"status":"available"},
                "route_connectors": {"value":{"location_id":"Farm","connectors":[{"kind":"warp","tile_x":{{connectorX}},"tile_y":1,"target_location":"Town","target_x":1,"target_y":1,"resolved":true}]},"status":"available"},
                "route_action_branch_coverage": {"value":{"rows":[{"tile_x":{{connectorX}},"tile_y":1,"branch":"Warp","route_training_blocked":false}]},"status":"available"}
              }
            }
            """)!;
        return new SnapshotEnvelope
        {
            StateHash = "route-cache-test-" + connectorX + "-" + blocked,
            GameTick = 1,
            State = state
        };
    }
}
