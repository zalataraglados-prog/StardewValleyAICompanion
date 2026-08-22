using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class CookoutKitPlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)926)->Torch((BC)278,destroyOvernight:true)";

    [Fact]
    public void ExactCookoutKitAndLegalTileCompileToOneNativePlacementStep()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.True(
            queue.Status == "pending",
            string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_cookout_kit", step.StepType);
        Assert.Equal("Farm(12,10):slot2:(O)926", step.Target);
        Assert.Contains("destroy_over_night=true", step.ExpectedEffect);
    }

    [Theory]
    [InlineData(0, true, "place_cookout_kit_inventory_identity_drifted")]
    [InlineData(2, false, "place_cookout_kit_exact_tile_not_native_legal")]
    public void EmptyInventoryOrIllegalTileFailsClosed(int stack, bool legalTile, string expectedReason)
    {
        var snapshot = Snapshot(stack, legalTile);
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void PurposeAndNativeLifetimeContractAreMandatory()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true);
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "cookout_placement_reason" and not "native_contract")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("place_cookout_kit_reason_required", item.BlockingReasons);
        Assert.Contains("place_cookout_kit_native_contract_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void PrimitiveIsCalibrationOnlyAndAllPlacementFamiliesUseSharedNativeKernel()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_cookout_kit");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(
            ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_cookout_kit").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var helper = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectPlacement.cs"));
        var cookout = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CookoutKitPlacement.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.CookoutKitPlacement.cs"));
        var machine = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MachinePlacement.cs"));
        var storage = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.StoragePlacement.cs"));
        Assert.Contains("Utility.tryToPlaceItem", helper, StringComparison.Ordinal);
        Assert.Contains("PlaceInventoryObjectNative", cookout, StringComparison.Ordinal);
        Assert.Contains("placed_runtime_type = typeof(Torch).FullName", projection, StringComparison.Ordinal);
        Assert.Contains("PlaceInventoryObjectNative", machine, StringComparison.Ordinal);
        Assert.Contains("PlaceInventoryObjectNative", storage, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility.tryToPlaceItem(", cookout, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility.tryToPlaceItem(", machine, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility.tryToPlaceItem(", storage, StringComparison.Ordinal);
        Assert.Contains("placedKit.destroyOvernight", cookout, StringComparison.Ordinal);
        Assert.Contains("placedKit.Fragility == 1", cookout, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot) => new()
    {
        ModelOutputId = "cookout-placement-test",
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
                ActionId = "place-cookout",
                OptionId = "executor.place_cookout_kit",
                Rationale = "enable one planned same-day cooking operation without a kitchen",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("qualified_item_id", "(O)926"), P("placement_projection_fingerprint", "cookout-fingerprint"),
                    P("cookout_placement_reason", "cook_recipe_without_available_kitchen"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot(int stack, bool legalTile)
    {
        var ranges = legalTile ? "[{\"y\":10,\"start_x\":12,\"end_x\":12}]" : "[]";
        var rows = stack > 0
            ? $$$"""[{"inventory_slot_index":2,"qualified_item_id":"(O)926","stack":{{{stack}}},"locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available","static_legal_tile_ranges":{{{ranges}}}}]}]"""
            : "[]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "inventory":{"value":[],"status":"available"},
            "cooking":{"value":{"sources":[]},"status":"available"},
            "cookout_kit_placement":{"value":{"static_projection_fingerprint":"cookout-fingerprint","rows":{{{rows}}}},"status":"available"}
          },
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-22T00:00:00Z",
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
