using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FlooringPlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFloorPathItem)->terrainFeatures.Add(Flooring)";
    private const string LayoutBasis = "native_legal_range+collision_grid_passable_target_bfs";

    [Theory]
    [InlineData("328", "0", "Default", 2, 0, 0)]
    [InlineData("329", "1", "Path", 2, 0, 0)]
    [InlineData("331", "4", "CornerDecorated", 2, 0, 0)]
    [InlineData("333", "5", "Random", 2, 0, 15)]
    public void EveryNativeConnectTypeCompilesToOnePassableNativePlacement(
        string itemId, string floorDataKey, string connectType, int neighborMask, int viewMin, int viewMax)
    {
        var snapshot = Snapshot(itemId, floorDataKey, connectType, neighborMask, viewMin, viewMax);
        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, floorDataKey, connectType, neighborMask, viewMin, viewMax), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_flooring", step.StepType);
        Assert.Equal("Farm(12,10):slot2:(O)" + itemId, step.Target);
        Assert.Contains("runtime_type=StardewValley.TerrainFeatures.Flooring", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("derived_neighbor_mask=2", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("placement_projection_fingerprint", "drifted", "place_flooring_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_before", "1", "place_flooring_inventory_or_data_identity_drifted")]
    [InlineData("expected_neighbor_mask_after", "4", "place_flooring_neighbor_topology_drifted")]
    [InlineData("reachable_tile_count_after_placement", "399", "place_flooring_passable_layout_drifted")]
    public void StaleFloorDataTopologyAndPassableLayoutBindingsFailClosed(
        string parameterName, string value, string expectedReason)
    {
        var snapshot = Snapshot("328", "0", "Default", 2, 0, 0);
        var request = Request(snapshot, "328", "0", "Default", 2, 0, 0);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameterName)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void FlooringPlacementUsesSharedMovementNativePlacementAndPassableLayoutSystems()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_flooring");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_flooring").PrimaryEngineId);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.place_flooring", out _));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FlooringPlacement.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var sharedPlacement = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectPlacement.cs"));
        var route = File.ReadAllText(Path.Combine(root, "src", "StardewAI.Core", "Infrastructure", "StoragePlacementLayoutProjection.Passable.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.FlooringPlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("CanPlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("PlacedTerrainFeature", sharedPlacement, StringComparison.Ordinal);
        Assert.Contains("\"place_flooring\" => ExecutePlaceFlooring", dispatcher, StringComparison.Ordinal);
        Assert.Contains("ReachableTileCountAfterPlacement = baseline.Distances.Count", route, StringComparison.Ordinal);
        Assert.Contains("Game1.floorPathData", projection, StringComparison.Ordinal);
        Assert.Contains("floor_path_catalog", projection, StringComparison.Ordinal);
        Assert.DoesNotContain(".terrainFeatures.Add(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("whichView.Value =", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void PurposeNativeContractAndPassabilityAreMandatory()
    {
        var snapshot = Snapshot("328", "0", "Default", 2, 0, 0);
        var request = Request(snapshot, "328", "0", "Default", 2, 0, 0);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "flooring_layout_reason" and not "native_contract" and not "expected_passable")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains("place_flooring_typed_target_fields_required", item.BlockingReasons);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string itemId, string floorDataKey, string connectType,
        int neighborMask, int viewMin, int viewMax) => new()
    {
        ModelOutputId = "flooring-placement-test",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "test",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.test",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "place-flooring",
                OptionId = "executor.place_flooring",
                Rationale = "construct one purpose-bound passable path tile",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("item_id", itemId), P("qualified_item_id", "(O)" + itemId),
                    P("inventory_runtime_type", "StardewValley.Object"), P("target_runtime_type", "StardewValley.TerrainFeatures.Flooring"),
                    P("floor_data_key", floorDataKey), P("connect_type", connectType), P("expected_passable", "true"),
                    P("expected_neighbor_mask_after", neighborMask.ToString()),
                    P("expected_which_view_min", viewMin.ToString()), P("expected_which_view_max", viewMax.ToString()),
                    P("placement_projection_fingerprint", "flooring-fingerprint"),
                    P("baseline_reachable_tile_count", "400"), P("reachable_tile_count_after_placement", "400"),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "1"),
                    P("layout_projection_basis", LayoutBasis), P("flooring_layout_reason", "farm_path_speed_and_layout"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId, string floorDataKey, string connectType, int neighborMask, int viewMin, int viewMax)
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(O){{{itemId}}}","stack":2}],"status":"available"},
            "flooring_placement":{"value":{
              "static_projection_fingerprint":"flooring-fingerprint",
              "rows":[{
                "inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"(O){{{itemId}}}","stack":2,
                "inventory_runtime_type":"StardewValley.Object","placed_runtime_type":"StardewValley.TerrainFeatures.Flooring",
                "floor_data_key":"{{{floorDataKey}}}","floor_data_item_id":"{{{itemId}}}","connect_type":"{{{connectType}}}",
                "expected_passable":true,"expected_which_view_min":{{{viewMin}}},"expected_which_view_max":{{{viewMax}}},
                "locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_ranges":[{"y":10,"start_x":12,"end_x":12,"expected_neighbor_mask_after":{{{neighborMask}}}}]}]
              }]
            },"status":"available"}
          },
          "current_location":{"terrain_features":{"value":[],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
