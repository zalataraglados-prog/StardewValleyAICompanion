using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class ReturnScepterMainlineTests
{
    private const string OptionId = "executor.use_return_scepter";
    private const string NativeContract =
        "Farmer.BeginUsingTool->Tool.beginUsing(InstantUse)->Game1.toolAnimationDone->Wand.DoFunction->1000ms_wandWarpForReal->Utility.getHomeOfFarmer(player).getFrontDoorSpot->Game1.warpFarmer(Farm)";

    [Theory]
    [InlineData("FarmHouse", "FarmHouse", 64, 15, false)]
    [InlineData("Cabin123", "Cabin", 12, 8, true)]
    public void ExactHomeAndCabinDestinationsCompileAsReusableNativeTool(
        string homeLocationId, string homeRuntimeType, int doorX, int doorY, bool isCabin)
    {
        var snapshot = Snapshot(homeLocationId, homeRuntimeType, doorX, doorY, isCabin);

        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot, homeLocationId, homeRuntimeType,
            doorX, doorY, isCabin), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_return_scepter", step.StepType);
        Assert.Equal("Farm:" + doorX + "," + doorY + ":slot2:(T)ReturnScepter", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("home_location_id=" + homeLocationId, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("is_cabin=" + isCabin.ToString().ToLowerInvariant(), step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("return_scepter_projection_fingerprint", "drifted", "use_return_scepter_projection_fingerprint_drifted")]
    [InlineData("source_location_id", "Farm", "use_return_scepter_source_location_drifted")]
    [InlineData("home_location_id", "FarmHouse2", "use_return_scepter_destination_drifted")]
    [InlineData("front_door_tile_x", "1", "use_return_scepter_destination_drifted")]
    [InlineData("inventory_stack_after", "0", "use_return_scepter_inventory_identity_drifted")]
    [InlineData("native_callback_delay_ms", "0", "use_return_scepter_animation_contract_drifted")]
    [InlineData("native_contract", "direct_warp", "use_return_scepter_native_contract_drifted")]
    public void StaleDestinationInventoryAndAnimationClaimsFailClosed(
        string parameter, string value, string reason)
    {
        var snapshot = Snapshot("FarmHouse", "FarmHouse", 64, 15, false);
        var request = Request(snapshot, "FarmHouse", "FarmHouse", 64, 15, false);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains(reason, item.BlockingReasons);
    }

    [Theory]
    [InlineData("blocked_home_unavailable")]
    [InlineData("blocked_bathing_clothes")]
    [InlineData("blocked_on_bridge")]
    [InlineData("blocked_already_at_destination")]
    public void NativeAndWastePreventionGatesExcludeCandidateUpstream(string gateStatus)
    {
        var snapshot = Snapshot("FarmHouse", "FarmHouse", 64, 15, false, gateStatus);

        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, "FarmHouse", "FarmHouse", 64, 15, false), snapshot).Items);

        Assert.Contains("use_return_scepter_native_effect_gate_blocked", item.BlockingReasons);
    }

    [Fact]
    public void ReturnScepterClosesFiveGatesAsMovementExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-289" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-289" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-289" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-289" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-289" }, capability.OutputEvidenceIds);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(OptionInvocationPolicy.PolicyOrAutonomous, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.NotPolicyTrainingOption, capability.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(ImplementationEngineIds.MovementNavigation,
            OptionImplementationCatalog.GetRequired(OptionId).PrimaryEngineId);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void RuntimeUsesPlayerNativeInstantToolChainWithoutDirectWarpOrStateMutation()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.ReturnScepter.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.ReturnScepter.cs"));
        var mapping = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop", "Program.RuntimeExecution.ReturnScepter.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeReturnScepterSmoke.ps1"));

        Assert.Contains("BeginUsingTool", runtime, StringComparison.Ordinal);
        Assert.Contains("Utility.getHomeOfFarmer", projection, StringComparison.Ordinal);
        Assert.Contains("getFrontDoorSpot", projection, StringComparison.Ordinal);
        Assert.Contains("request.LocationId = ReadQueueParameterString(item, \"source_location_id\")", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer(\"", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Position =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("CanMove =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("temporarilyInvincible =", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string homeLocationId, string homeRuntimeType, int doorX, int doorY, bool isCabin) => new()
    {
        ModelOutputId = "return-scepter-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.return_home",
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
                ActionId = "use-return-scepter",
                OptionId = OptionId,
                Rationale = "movement compiler selected the reusable exact-home warp",
                Parameters = new[]
                {
                    P("source_location_id", "Town"), P("target_location", "Farm"), P("inventory_slot_index", "2"),
                    P("item_id", "ReturnScepter"), P("qualified_item_id", "(T)ReturnScepter"),
                    P("inventory_runtime_type", "StardewValley.Tools.Wand"),
                    P("inventory_stack_before", "1"), P("inventory_stack_after", "1"),
                    P("return_scepter_projection_fingerprint", "return-scepter-fingerprint"),
                    P("home_location_id", homeLocationId), P("home_runtime_type", homeRuntimeType),
                    P("destination_location_id", "Farm"), P("front_door_tile_x", doorX.ToString()),
                    P("front_door_tile_y", doorY.ToString()), P("home_is_cabin", isCabin.ToString().ToLowerInvariant()),
                    P("already_at_destination", "false"), P("native_instant_use", "true"),
                    P("native_facing_direction", "2"), P("native_callback_delay_ms", "1000"),
                    P("native_freeze_pause_ms", "2000"), P("native_poof_sprite_count", "12"),
                    P("native_trail_sprite_count", "17"), P("native_trail_delay_step_ms", "25"),
                    P("native_trail_max_delay_ms", "400"), P("native_sound", "wand"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string homeLocationId, string homeRuntimeType, int doorX, int doorY, bool isCabin,
        string gateStatus = "ready")
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Town","status":"available"},
            "tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"ReturnScepter","qualified_item_id":"(T)ReturnScepter","stack":1}],"status":"available"},
            "return_scepter":{"value":{
              "projection_fingerprint":"return-scepter-fingerprint","native_use_gate_status":"{{{gateStatus}}}",
              "destination":{"home_location_id":"{{{homeLocationId}}}","home_runtime_type":"{{{homeRuntimeType}}}",
                "destination_location_id":"Farm","front_door_tile_x":{{{doorX}}},"front_door_tile_y":{{{doorY}}},
                "home_is_cabin":{{{isCabin.ToString().ToLowerInvariant()}}},"already_at_destination":false},
              "animation_contract":{"instant_use":true,"facing_direction":2,"callback_delay_ms":1000,
                "freeze_pause_ms":2000,"poof_sprite_count":12,"trail_sprite_count":17,
                "trail_delay_step_ms":25,"trail_max_delay_ms":400,"sound":"wand"},
              "native_contract":"{{{NativeContract}}}",
              "rows":[{"inventory_slot_index":2,"item_id":"ReturnScepter","qualified_item_id":"(T)ReturnScepter",
                "inventory_runtime_type":"StardewValley.Tools.Wand","stack_before":1,"stack_after":1,"reusable_tool":true}]
            },"status":"available"}
          },
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
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
