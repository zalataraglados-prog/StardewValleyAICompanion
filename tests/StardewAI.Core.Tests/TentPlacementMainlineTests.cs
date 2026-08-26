using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class TentPlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)TentKit)->largeTerrainFeatures.Add(Tent(rectangle.X+1,rectangle.Y+1))";
    private const string LayoutBasis =
        "native_directional_3x2_area_clear+collision_grid_passable_rectangular_footprint_bfs+protected_endpoint_exclusion";

    [Fact]
    public void ExactDirectionalRectangleCompilesToOneNativeTentPlacementStep()
    {
        var snapshot = Snapshot(festivalBlocked: false, legalStand: true);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_tent", step.StepType);
        Assert.Equal("Farm:probe(12,10):anchor(13,10):slot2", step.Target);
        Assert.Contains("StardewValley.TerrainFeatures.Tent", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, true, "place_tent_exact_directional_stand_not_native_legal")]
    [InlineData(false, false, "place_tent_exact_directional_stand_not_native_legal")]
    public void FestivalOrIllegalStandFailsClosed(bool festivalBlocked, bool legalStand, string expectedReason)
    {
        var snapshot = Snapshot(festivalBlocked, legalStand);
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void DirectionDerivedRectangleCannotBeOverriddenByModel()
    {
        var snapshot = Snapshot(festivalBlocked: false, legalStand: true);
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Select(row => row.Name == "anchor_tile_x" ? P(row.Name, "99") : row)
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("place_tent_directional_geometry_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void PlacementReusesSharedMovementNativeKernelAndKeepsSleepSeparate()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_tent");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_tent").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.TentPlacement.cs"));
        var sharedMovement = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var sharedPlacement = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectPlacement.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.TentPlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("BuildRequestedAdjacentPath", sharedMovement, StringComparison.Ordinal);
        Assert.Contains("Utility.tryToPlaceItem", sharedPlacement, StringComparison.Ordinal);
        Assert.DoesNotContain("location.largeTerrainFeatures.Add", runtime, StringComparison.Ordinal);
        Assert.Contains("recovery.sleep_in_tent remains a separate semantic action", projection, StringComparison.Ordinal);
        Assert.True(PendingSemanticActionCatalog.TryGet("recovery.sleep_in_tent", out _));
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.place_tent", out _));
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot) => new()
    {
        ModelOutputId = "tent-placement-test",
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
                ActionId = "place-tent",
                OptionId = "executor.place_tent",
                Rationale = "establish a verified temporary sleep endpoint for the day plan",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"), P("direction", "1"),
                    P("rectangle_x", "12"), P("rectangle_y", "9"), P("rectangle_width", "3"), P("rectangle_height", "2"),
                    P("anchor_tile_x", "13"), P("anchor_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "1"),
                    P("qualified_item_id", "(O)TentKit"), P("inventory_runtime_type", "StardewValley.Object"),
                    P("placed_runtime_type", "StardewValley.TerrainFeatures.Tent"),
                    P("placement_projection_fingerprint", "tent-fingerprint"),
                    P("tomorrow_season", "Spring"), P("tomorrow_day", "2"), P("tomorrow_festival_id", ""),
                    P("baseline_reachable_tile_count", "10000"), P("reachable_tile_count_after_placement", "10000"),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "1"),
                    P("layout_projection_basis", LayoutBasis),
                    P("tent_placement_reason", "temporary_sleep_endpoint_for_committed_route"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot(bool festivalBlocked, bool legalStand)
    {
        var ranges = legalStand ? "[{\"y\":10,\"start_x\":11,\"end_x\":11}]" : "[]";
        var status = festivalBlocked ? "native_tomorrow_festival_blocked" : "native_legal_directional_stands_available";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "tent_placement":{"value":{
              "static_projection_fingerprint":"tent-fingerprint",
              "rows":[{"inventory_slot_index":2,"qualified_item_id":"(O)TentKit","stack":1,"exact_base_object":true,
                "inventory_runtime_type":"StardewValley.Object","placed_runtime_type":"StardewValley.TerrainFeatures.Tent",
                "locations":[{"location_id":"Farm","location_is_outdoors":true,"tomorrow_season":"Spring","tomorrow_day":2,
                  "tomorrow_festival_blocked":{{{festivalBlocked.ToString().ToLowerInvariant()}}},"tomorrow_festival_id":"",
                  "placement_probe_status":"{{{status}}}","direction_rows":[{"direction":1,"static_legal_stand_tile_ranges":{{{ranges}}}}]}]}]
            },"status":"available"}
          },
          "current_location":{"large_terrain_features":{"value":[],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
