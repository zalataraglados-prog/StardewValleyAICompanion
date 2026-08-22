using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class CrabPotBaitMainlineTests
{
    private const string BaitId = "(O)685";
    private const string BaitRuntimeType = "StardewValley.Object";
    private const string BaitUnitState = "bait-unit-state";
    private const string NativeContract =
        "GameLocation.checkAction->CrabPot.performObjectDropInAction(Category=-21,probe:false,owner=current_player)->Farmer.reduceActiveItemByOne";

    [Fact]
    public void ExactNativeAcceptedBaitCompiles()
    {
        var snapshot = Snapshot();
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("load_crab_pot_bait", step.StepType);
        Assert.Equal("Beach(12,10):slot2:(O)685", step.Target);
        Assert.Contains("owner=current_player", step.ExpectedEffect);
        Assert.Contains("stack_decreases=1", step.ExpectedEffect);
    }

    [Theory]
    [InlineData("already_baited", 2, 1234, BaitUnitState, "load_crab_pot_bait_target_not_ready_or_drifted")]
    [InlineData("ready", 1, 5678, BaitUnitState, "load_crab_pot_bait_owner_projection_drifted")]
    [InlineData("ready", 1, 1234, "drifted", "load_crab_pot_bait_inventory_projection_drifted")]
    public void StalePotOwnerOrBaitUnitFailsClosed(
        string status,
        int stack,
        long ownerBefore,
        string unitState,
        string expectedReason)
    {
        var snapshot = Snapshot(status, stack, ownerBefore, unitState);
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void ReasonAndNativeContractAreMandatory()
    {
        var snapshot = Snapshot();
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(row => row.Name is not "crab_pot_bait_reason" and not "native_contract")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("load_crab_pot_bait_reason_required", item.BlockingReasons);
        Assert.Contains("load_crab_pot_bait_native_contract_mismatch", item.BlockingReasons);
    }

    [Fact]
    public void PrimitiveIsCalibrationOnlyAndUsesTheNativeCrabPotDropInPath()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.load_crab_pot_bait");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(
            ImplementationEngineIds.FarmMachine,
            OptionImplementationCatalog.GetRequired("executor.load_crab_pot_bait").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.CrabPots.cs"));
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPotBait.cs"));
        var sharedMovement = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CropTileActions.cs"));
        var collection = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CrabPots.cs"));
        Assert.Contains("performObjectDropInAction(candidate, probe: true, player)", projection, StringComparison.Ordinal);
        Assert.Contains("location.checkAction(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("pot.bait.Value =", runtime, StringComparison.Ordinal);
        Assert.Contains("\"load_crab_pot_bait\" => ExecuteLoadCrabPotBait", sharedMovement, StringComparison.Ordinal);
        Assert.Contains("StartCrabPotCollect", collection, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot) => new()
    {
        ModelOutputId = "crab-pot-bait-test",
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
                ActionId = "load-crab-pot-bait",
                OptionId = "executor.load_crab_pot_bait",
                Rationale = "service one empty production endpoint",
                Parameters = new[]
                {
                    P("target_location", "Beach"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"),
                    P("inventory_slot_index", "2"), P("expected_stack_before", "1"),
                    P("qualified_item_id", BaitId), P("bait_runtime_type", BaitRuntimeType), P("bait_quality", "0"),
                    P("expected_container_bait_qualified_item_id", BaitId),
                    P("expected_container_bait_unit_state_sha256", BaitUnitState),
                    P("target_runtime_type", "StardewValley.Objects.CrabPot"),
                    P("expected_container_owner_player_id_before", "1234"),
                    P("expected_container_owner_player_id_after", "1234"),
                    P("native_contract", NativeContract), P("crab_pot_bait_reason", "production_service")
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string status = "ready",
        int stack = 1,
        long ownerBefore = 1234,
        string unitState = BaitUnitState)
    {
        var existingBait = status == "already_baited" ? BaitId : string.Empty;
        var json = """
        {
          "player":{
            "location_id":{"value":"Beach","status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"(O)685","stack":STACK}],"status":"available"}
          },
          "current_location":{"objects":{"value":[{
            "tile_x":12,"tile_y":10,"type":"StardewValley.Objects.CrabPot",
            "crab_pot_bait_load_status":"STATUS","crab_pot_needs_bait":true,
            "crab_pot_ready_for_harvest":false,"crab_pot_output_qualified_item_id":"",
            "crab_pot_bait_qualified_item_id":"EXISTING_BAIT",
            "crab_pot_owner_player_id_before_bait":OWNER_BEFORE,
            "crab_pot_expected_owner_player_id_after_bait":1234,
            "crab_pot_bait_load_native_contract":"NATIVE_CONTRACT",
            "crab_pot_bait_load_inventory_rows":[{
              "inventory_slot_index":2,"qualified_item_id":"(O)685","stack":STACK,"quality":0,"category":-21,
              "runtime_type":"StardewValley.Object","unit_state_sha256":"UNIT_STATE","native_probe_accepts":true,
              "expected_container_bait_qualified_item_id":"(O)685","expected_container_bait_runtime_type":"StardewValley.Object",
              "expected_container_bait_quality":0,"expected_container_bait_unit_state_sha256":"UNIT_STATE","expected_consumed_quantity":1
            }]
          }],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Beach","width":100,"height":100,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """
        .Replace("STATUS", status)
        .Replace("STACK", stack.ToString())
        .Replace("OWNER_BEFORE", ownerBefore.ToString())
        .Replace("EXISTING_BAIT", existingBait)
        .Replace("UNIT_STATE", unitState)
        .Replace("NATIVE_CONTRACT", NativeContract);
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
