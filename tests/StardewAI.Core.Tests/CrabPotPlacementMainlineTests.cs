using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class CrabPotPlacementMainlineTests
{
    private const string Fingerprint = "crab-pot-placement-fingerprint";
    private const string ProductionSignature = "Beach|0.2|ocean|152,153";
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)710)->CrabPot.placementAction(owner=current_player)";

    [Fact]
    public void ExactWaterPlacementCompiles()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_crab_pot", step.StepType);
        Assert.Equal("Beach(12,10):slot2:(O)710", step.Target);
        Assert.Contains("StardewValley.Objects.CrabPot", step.ExpectedEffect);
        Assert.Contains("owner=current_player", step.ExpectedEffect);
    }

    [Theory]
    [InlineData(0, true, 1234, ProductionSignature, "place_crab_pot_inventory_identity_drifted")]
    [InlineData(2, false, 1234, ProductionSignature, "place_crab_pot_exact_water_tile_not_native_legal")]
    [InlineData(2, true, 5678, ProductionSignature, "place_crab_pot_owner_identity_drifted")]
    [InlineData(2, true, 1234, "drifted", "place_crab_pot_production_context_drifted")]
    public void StaleInventoryWaterOwnerOrProductionContextFailsClosed(
        int stack,
        bool legalTile,
        long ownerId,
        string productionSignature,
        string expectedReason)
    {
        var snapshot = Snapshot(stack, legalTile, ownerId);
        var request = Request(snapshot, productionSignature);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void ReasonNativeContractAndProductionSignatureAreMandatory()
    {
        var snapshot = Snapshot(stack: 2, legalTile: true, ownerId: 1234);
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "crab_pot_placement_reason" and not "native_contract" and not "production_signature")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("place_crab_pot_reason_required", item.BlockingReasons);
        Assert.Contains("place_crab_pot_production_signature_required", item.BlockingReasons);
        Assert.Contains("place_crab_pot_native_contract_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void PrimitiveIsCalibrationOnlyAndReusesPlacementMovementAndCollectSystems()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_crab_pot");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(
            ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_crab_pot").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPotPlacement.cs"));
        var dispatcher = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var collect = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPots.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.CrabPotPlacement.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Utility.tryToPlaceItem(", runtime, StringComparison.Ordinal);
        Assert.Contains("\"place_crab_pot\" => ExecutePlaceCrabPot", dispatcher, StringComparison.Ordinal);
        Assert.Contains("StartCrabPotCollect", collect, StringComparison.Ordinal);
        Assert.Contains("CrabPot.IsValidCrabPotLocationTile", projection, StringComparison.Ordinal);
        Assert.Contains("native_order_catch_rows", projection, StringComparison.Ordinal);
        Assert.Contains("typeof(CrabPot).FullName", projection, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeBaitLoadingIsRegisteredSeparatelyFromPlacement()
    {
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.load_crab_pot_bait", out _));
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.load_crab_pot_bait");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(ImplementationEngineIds.FarmMachine, OptionImplementationCatalog.GetRequired("executor.load_crab_pot_bait").PrimaryEngineId);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot, string productionSignature = ProductionSignature) => new()
    {
        ModelOutputId = "crab-pot-placement-test",
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
                ActionId = "place-crab-pot",
                OptionId = "executor.place_crab_pot",
                Rationale = "establish one purpose-bound ocean catch endpoint",
                Parameters = new[]
                {
                    P("target_location", "Beach"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("inventory_stack_before", "2"),
                    P("qualified_item_id", "(O)710"), P("expected_owner_player_id", "1234"),
                    P("placement_projection_fingerprint", Fingerprint), P("production_signature", productionSignature),
                    P("native_contract", NativeContract), P("crab_pot_placement_reason", "task_and_production_capacity")
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(int stack, bool legalTile, long ownerId)
    {
        var ranges = legalTile
            ? $$"""[{"y":10,"start_x":12,"end_x":12,"production_signature":"{{ProductionSignature}}","fish_area_id":"Beach","base_junk_chance":0.2}]"""
            : "[]";
        var json = """
        {
          "player":{
            "location_id":{"value":"Beach","status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(O)710","stack":STACK}],"status":"available"},
            "crab_pot_placement":{"value":{
              "static_projection_fingerprint":"FINGERPRINT","owner_player_id":OWNER,
              "rows":[{"inventory_slot_index":2,"qualified_item_id":"(O)710","stack":STACK,"locations":[{
                "location_id":"Beach","placement_probe_status":"native_legal_water_tiles_available","static_legal_tile_ranges":RANGES
              }]}]
            },"status":"available"}
          },
          "current_location":{"objects":{"value":[],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Beach","width":100,"height":100,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """
        .Replace("FINGERPRINT", Fingerprint)
        .Replace("OWNER", ownerId.ToString())
        .Replace("STACK", stack.ToString())
        .Replace("RANGES", ranges);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-22T00:00:00Z",
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
