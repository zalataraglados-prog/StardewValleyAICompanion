using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FurniturePlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Furniture.placementAction->Object.placementAction->location.furniture_or_table.heldObject";
    private const string LayoutBasis =
        "native_furniture_range+rotation_adjusted_rectangular_footprint+remote_or_cardinal_reach+protected_endpoint_and_furniture_storage_access";

    [Theory]
    [InlineData("StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 2, 1, false, 398)]
    [InlineData("StardewValley.Objects.BedFurniture", "StardewValley.Objects.BedFurniture", 2, 1, 1, 2, false, 398)]
    [InlineData("StardewValley.Objects.StorageFurniture", "StardewValley.Objects.StorageFurniture", 0, 0, 1, 1, false, 399)]
    [InlineData("StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 2, 2, true, 400)]
    public void VanillaRuntimeRotationFootprintAndPassabilityCompileToOneNativePlacement(
        string inventoryType, string placedType, int desiredRotation, int rotationSteps,
        int width, int height, bool passable, int reachableAfter)
    {
        var snapshot = Snapshot(inventoryType, placedType, desiredRotation, rotationSteps, width, height, passable);
        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot, inventoryType, placedType, desiredRotation, rotationSteps, width, height, passable, reachableAfter), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var step = Assert.Single(Assert.Single(queue.Items).NormalizedCommand.Steps);
        Assert.Equal("place_furniture", step.StepType);
        Assert.Equal("FarmHouse(12,10):slot2:rotation" + desiredRotation, step.Target);
        Assert.Contains("current_location.furniture.add", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("placement_projection_fingerprint", "stale", "place_furniture_projection_fingerprint_drifted")]
    [InlineData("inventory_current_rotation", "2", "place_furniture_inventory_or_factory_identity_drifted")]
    [InlineData("desired_current_rotation", "3", "place_furniture_rotation_or_location_projection_drifted")]
    [InlineData("footprint_width", "3", "place_furniture_exact_target_footprint_or_endpoint_drifted")]
    [InlineData("reachable_tile_count_after_placement", "397", "place_furniture_route_or_access_layout_drifted")]
    public void StaleIdentityRotationEndpointAndRouteBindingsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot("StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 2, 1, false);
        var request = Request(snapshot, "StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 2, 1, false, 398);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(reason, item.BlockingReasons);
    }

    [Fact]
    public void TableHeldEndpointIsCompiledSeparatelyFromLocationFurniture()
    {
        var snapshot = Snapshot("StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 1, 1, true, true);
        var request = Request(snapshot, "StardewValley.Objects.Furniture", "StardewValley.Objects.Furniture", 0, 0, 1, 1, true, 400, true);
        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains("current_location.furniture[0].held_object=(F)1440",
            Assert.Single(item.NormalizedCommand.Steps).ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void FurniturePlacementReusesSharedNativePlacementAndHasNoDirectProductionFurnitureWrite()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_furniture");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_furniture").PrimaryEngineId);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.place_furniture", out _));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FurniturePlacement.cs"));
        var fixture = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FurniturePlacementFixture.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.FurniturePlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("Furniture.rotate", projection, StringComparison.Ordinal);
        Assert.Contains("Data\\\\Furniture", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("location.furniture.Add", runtime, StringComparison.Ordinal);
        Assert.Contains("location.furniture.Add", fixture, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string inventoryType, string placedType, int desiredRotation, int rotationSteps,
        int width, int height, bool passable, int reachableAfter, bool table = false) => new()
    {
        ModelOutputId = "furniture-placement-test",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "test",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "place-furniture",
                OptionId = "executor.place_furniture",
                Rationale = "purpose-bound furniture layout",
                Parameters = new[]
                {
                    P("target_location", "FarmHouse"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "10"), P("stand_tile_y", "10"), P("inventory_slot_index", "2"),
                    P("inventory_stack_before", "1"), P("item_id", "1440"), P("qualified_item_id", "(F)1440"),
                    P("inventory_runtime_type", inventoryType), P("target_runtime_type", placedType),
                    P("inventory_current_rotation", "0"), P("desired_current_rotation", desiredRotation.ToString()),
                    P("rotation_steps_from_inventory", rotationSteps.ToString()), P("furniture_type", "0"),
                    P("can_free_place_furniture", "true"), P("expected_passable", passable.ToString().ToLowerInvariant()),
                    P("placement_endpoint", table ? "table_held_object" : "location_furniture"),
                    P("table_index", table ? "0" : "-1"), P("table_tile_x", table ? "12" : "-1"),
                    P("table_tile_y", table ? "10" : "-1"), P("expected_anchor_x", "12"), P("expected_anchor_y", "10"),
                    P("footprint_width", width.ToString()), P("footprint_height", height.ToString()),
                    P("placement_projection_fingerprint", "furniture-fingerprint"),
                    P("baseline_reachable_tile_count", "400"), P("reachable_tile_count_after_placement", reachableAfter.ToString()),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "0"),
                    P("layout_projection_basis", LayoutBasis), P("furniture_layout_reason", "home_workflow_and_storage_layout"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string inventoryType, string placedType, int desiredRotation, int rotationSteps,
        int width, int height, bool passable, bool table = false)
    {
        var endpoint = table ? "table_held_object" : "location_furniture";
        var tableIndex = table ? 0 : -1;
        var tableX = table ? 12 : -1;
        var tableY = table ? 10 : -1;
        var furnitureRows = table
            ? "[{\"index\":0,\"tile_x\":12,\"tile_y\":10,\"tiles_wide\":1,\"tiles_high\":1,\"storage_capacity\":null}]"
            : "[]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(F)1440","stack":1}],"status":"available"},
            "furniture_placement":{"value":{"static_projection_fingerprint":"furniture-fingerprint","rows":[{
              "inventory_slot_index":2,"item_id":"1440","qualified_item_id":"(F)1440","stack":1,
              "inventory_runtime_type":"{{{inventoryType}}}","expected_placed_runtime_type":"{{{placedType}}}",
              "runtime_type_supported":true,"inventory_current_rotation":0,
              "rotations":[{"inventory_rotation_before":0,"rotation_steps_from_inventory":{{{rotationSteps}}},
                "desired_current_rotation":{{{desiredRotation}}},"location_id":"FarmHouse","can_free_place_furniture":true,
                "placement_probe_status":"native_legal_tiles_available","static_legal_tile_ranges":[{
                  "y":10,"start_x":12,"end_x":12,"anchor_offset_x":0,"anchor_offset_y":0,
                  "footprint_width":{{{width}}},"footprint_height":{{{height}}},"expected_passable":{{{passable.ToString().ToLowerInvariant()}}},
                  "placement_endpoint":"{{{endpoint}}}","table_index":{{{tableIndex}}},"table_tile_x":{{{tableX}}},"table_tile_y":{{{tableY}}}
                }]}]
            }]},"status":"available"}
          },
          "current_location":{"furniture":{"value":{{{furnitureRows}}},"status":"available"},"chests":{"value":{},"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-25T00:00:00Z", Completeness = "complete", State = state
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
