using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FencePlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFenceItem)->Fence(tile,item_id,is_gate)";

    [Theory]
    [InlineData("322", false, 100, false, 198d, 202d, 198d, 202d)]
    [InlineData("323", false, 100, false, 398d, 402d, 398d, 402d)]
    [InlineData("324", false, 100, false, 498d, 502d, 498d, 502d)]
    [InlineData("298", false, 100, false, 558d, 562d, 558d, 562d)]
    [InlineData("325", true, 110, true, 400d, 400d, 200d, 200d)]
    public void EveryVanillaFenceIdentityCompilesToOneRouteSafeNativePlacement(
        string itemId,
        bool isGate,
        int drawSum,
        bool gateFunctional,
        double healthMin,
        double healthMax,
        double maxHealthMin,
        double maxHealthMax)
    {
        var snapshot = Snapshot(itemId, isGate, drawSum, gateFunctional, healthMin, healthMax, maxHealthMin, maxHealthMax);
        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, isGate, drawSum, gateFunctional, healthMin, healthMax, maxHealthMin, maxHealthMax),
            snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_fence", step.StepType);
        Assert.Equal("Farm(12,10):slot2:(O)" + itemId, step.Target);
        Assert.Contains("runtime_type=StardewValley.Fence", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void NativePlaceableGateWithoutFunctionalNeighborsFailsClosed()
    {
        var snapshot = Snapshot("325", true, 0, false, 400, 400, 200, 200);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, "325", true, 0, false, 400, 400, 200, 200), snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains("place_fence_gate_requires_functional_neighbor_topology", item.BlockingReasons);
    }

    [Theory]
    [InlineData("placement_projection_fingerprint", "drifted", "place_fence_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_before", "1", "place_fence_inventory_or_data_identity_drifted")]
    [InlineData("expected_draw_sum_after", "500", "place_fence_neighbor_topology_drifted")]
    [InlineData("baseline_reachable_tile_count", "399", "place_fence_route_safe_layout_drifted")]
    public void StaleFenceAndRouteBindingsFailClosed(string parameterName, string value, string expectedReason)
    {
        var snapshot = Snapshot("322", false, 100, false, 198, 202, 198, 202);
        var request = Request(snapshot, "322", false, 100, false, 198, 202, 198, 202);
        var parameter = Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameterName));
        parameter.Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void PurposeAndNativeContractAreMandatory()
    {
        var snapshot = Snapshot("322", false, 100, false, 198, 202, 198, 202);
        var request = Request(snapshot, "322", false, 100, false, 198, 202, 198, 202);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "fence_layout_reason" and not "native_contract")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains("place_fence_layout_reason_required", item.BlockingReasons);
        Assert.Contains("place_fence_native_contract_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void FencePlacementUsesOneSharedMovementPlacementAndRouteSafetySystem()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_fence");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_fence").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FencePlacement.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var route = File.ReadAllText(Path.Combine(root, "src", "StardewAI.Core", "Infrastructure", "StoragePlacementLayoutProjection.Exact.cs"));
        var mapper = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.FencePlacement.cs"));
        var contract = File.ReadAllText(Path.Combine(root, "src", "StardewAI.Contracts", "Training", "TrainingExecutionContracts.FencePlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("CanPlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("\"place_fence\" => ExecutePlaceFence", dispatcher, StringComparison.Ordinal);
        Assert.Contains("Search(start, width, height, blocked, target)", route, StringComparison.Ordinal);
        Assert.Contains("ExpectedFenceDrawSum = ReadQueueParameterInt", mapper, StringComparison.Ordinal);
        Assert.Contains("ExpectedFenceHealthMin = ReadQueueParameterDouble", mapper, StringComparison.Ordinal);
        Assert.Contains("JsonPropertyName(\"expected_fence_draw_sum\")", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("location.objects.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("health.Value =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("isGate.Value = true", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("isGate.Value = false", runtime, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot,
        string itemId,
        bool isGate,
        int drawSum,
        bool gateFunctional,
        double healthMin,
        double healthMax,
        double maxHealthMin,
        double maxHealthMax) => new()
    {
        ModelOutputId = "fence-placement-test",
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
                ActionId = "place-fence",
                OptionId = "executor.place_fence",
                Rationale = "construct one purpose-bound route-safe enclosure segment",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("item_id", itemId), P("qualified_item_id", "(O)" + itemId),
                    P("inventory_runtime_type", "StardewValley.Object"), P("target_runtime_type", "StardewValley.Fence"),
                    P("fence_data_key", itemId), P("expected_is_gate", isGate.ToString()),
                    P("expected_draw_sum_after", drawSum.ToString()), P("expected_gate_functional", gateFunctional.ToString()),
                    P("expected_health_min", F(healthMin)), P("expected_health_max", F(healthMax)),
                    P("expected_max_health_min", F(maxHealthMin)), P("expected_max_health_max", F(maxHealthMax)),
                    P("placement_projection_fingerprint", "fence-fingerprint"),
                    P("baseline_reachable_tile_count", "400"), P("reachable_tile_count_after_placement", "399"),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "1"),
                    P("layout_projection_basis", "native_legal_range+collision_grid_virtual_occupancy_bfs+protected_endpoint_and_storage_access"),
                    P("fence_layout_reason", "livestock_enclosure_layout"), P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId,
        bool isGate,
        int drawSum,
        bool gateFunctional,
        double healthMin,
        double healthMax,
        double maxHealthMin,
        double maxHealthMax)
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(O){{{itemId}}}","stack":2}],"status":"available"},
            "fence_placement":{"value":{
              "static_projection_fingerprint":"fence-fingerprint",
              "rows":[{
                "inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"(O){{{itemId}}}","stack":2,
                "inventory_runtime_type":"StardewValley.Object","placed_runtime_type":"StardewValley.Fence",
                "is_gate":{{{isGate.ToString().ToLowerInvariant()}}},"fence_data_key":"{{{itemId}}}",
                "expected_health_min":{{{F(healthMin)}}},"expected_health_max":{{{F(healthMax)}}},
                "expected_max_health_min":{{{F(maxHealthMin)}}},"expected_max_health_max":{{{F(maxHealthMax)}}},
                "locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_ranges":[{"y":10,"start_x":12,"end_x":12,"expected_draw_sum_after":{{{drawSum}}},"expected_gate_functional":{{{gateFunctional.ToString().ToLowerInvariant()}}}}]}]
              }]
            },"status":"available"}
          },
          "current_location":{
            "objects":{"value":[],"status":"available"},
            "chests":{"value":{"schema_version":"storage_infrastructure.v1","status":"available","scope_location_id":"Farm","access_points":[]},"status":"available"}
          },
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

    private static string F(double value) => value.ToString(CultureInfo.InvariantCulture);
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
