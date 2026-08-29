using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class GrassPlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)297|(O)BlueGrassStarter)->terrainFeatures.Add(Grass(type,4))";
    private const string LayoutBasis = "native_legal_range+collision_grid_passable_target_bfs";

    [Theory]
    [InlineData("297", "(O)297", 1)]
    [InlineData("BlueGrassStarter", "(O)BlueGrassStarter", 7)]
    public void EveryNativeGrassStarterCompilesToOneExactPlacement(
        string itemId, string qualifiedItemId, int grassType)
    {
        var snapshot = Snapshot(itemId, qualifiedItemId, grassType);
        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, qualifiedItemId, grassType), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("plant_grass", step.StepType);
        Assert.Equal($"Farm(12,10):slot2:{qualifiedItemId}", step.Target);
        Assert.Contains("grass_type=" + grassType, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("number_of_weeds=4", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("placement_projection_fingerprint", "drifted", "plant_grass_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_before", "1", "plant_grass_inventory_or_variant_identity_drifted")]
    [InlineData("expected_grass_type", "7", "plant_grass_inventory_or_variant_identity_drifted")]
    [InlineData("reachable_tile_count_after_placement", "399", "plant_grass_passable_layout_drifted")]
    public void StaleInventoryVariantAndLayoutBindingsFailClosed(
        string parameterName, string value, string expectedReason)
    {
        var snapshot = Snapshot("297", "(O)297", 1);
        var request = Request(snapshot, "297", "(O)297", 1);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameterName)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void GrassPlacementOwnsAllFiveGatesAndUsesOnlySharedNativePlacement()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.plant_grass");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, capability.CompilerStatus);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.OutputTrainingGate);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.plant_grass").PrimaryEngineId);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.plant_grass", out _));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.GrassPlacement.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.GrassPlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("CanPlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("\"plant_grass\" => ExecutePlantGrass", dispatcher, StringComparison.Ordinal);
        Assert.Contains("BlueGrassStarter", projection, StringComparison.Ordinal);
        Assert.DoesNotContain(".terrainFeatures.Add(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("numberOfWeeds.Value = 4", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("grassType.Value = 1", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("grassType.Value = 7", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void ExactUpstreamLayoutPurposeAndNativeContractAreMandatory()
    {
        var snapshot = Snapshot("297", "(O)297", 1);
        var request = Request(snapshot, "297", "(O)297", 1);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "grass_layout_reason" and not "native_contract")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains("plant_grass_layout_reason_required", item.BlockingReasons);
        Assert.Contains("plant_grass_native_contract_mismatch", item.BlockingReasons);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string itemId, string qualifiedItemId, int grassType) => new()
    {
        ModelOutputId = "grass-placement-test",
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
                ActionId = "plant-grass",
                OptionId = "executor.plant_grass",
                Rationale = "place one purpose-bound grass starter",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("item_id", itemId), P("qualified_item_id", qualifiedItemId),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("target_runtime_type", "StardewValley.TerrainFeatures.Grass"),
                    P("expected_grass_type", grassType.ToString()), P("expected_initial_number_of_weeds", "4"),
                    P("placement_sound", "dirtyHit"), P("expected_passable", "true"),
                    P("placement_projection_fingerprint", "grass-fingerprint"),
                    P("baseline_reachable_tile_count", "400"), P("reachable_tile_count_after_placement", "400"),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "1"),
                    P("layout_projection_basis", LayoutBasis), P("grass_layout_reason", "livestock_feed_spread"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(string itemId, string qualifiedItemId, int grassType)
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"{{{qualifiedItemId}}}","stack":2}],"status":"available"},
            "grass_placement":{"value":{
              "static_projection_fingerprint":"grass-fingerprint",
              "rows":[{
                "inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qualifiedItemId}}}","stack":2,
                "inventory_runtime_type":"StardewValley.Object","placed_runtime_type":"StardewValley.TerrainFeatures.Grass",
                "expected_grass_type":{{{grassType}}},"expected_initial_number_of_weeds":4,"placement_sound":"dirtyHit",
                "expected_passable":true,
                "locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_ranges":[{"y":10,"start_x":12,"end_x":12}]}]
              }]
            },"status":"available"}
          },
          "current_location":{"objects":{"value":[],"status":"available"},"terrain_features":{"value":[],"status":"available"}},
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
            RealTimestamp = "2026-08-29T00:00:00Z",
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
