using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class TentSleepMainlineTests
{
    private const string NativeContract =
        "GameLocation.checkAction->Tent.performUseAction->SleepTent_Yes->startSleep->CanWakeUpHere(sleptInTemporaryBed)->Tent.dayUpdate/tickUpdate";

    [Fact]
    public void ExactPlacedTentCompilesToOneTerminalNativeSleepStep()
    {
        var snapshot = Snapshot();
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("sleep_in_tent", step.StepType);
        Assert.Equal("Farm:tent(13,10):stand(13,11)", step.Target);
        Assert.Contains("time.total_days=increases_by_1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("destroyed", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void TentSleepMustBeTerminal()
    {
        var snapshot = Snapshot();
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Select(row => row.Name == "compiler_context.is_terminal_step" ? P(row.Name, "false") : row)
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("tent_sleep_action_must_be_terminal", item.BlockingReasons);
    }

    [Theory]
    [InlineData(false, true, false, "tent_sleep_native_prompt_gate_closed")]
    [InlineData(true, false, false, "tent_sleep_native_prompt_gate_closed")]
    [InlineData(true, true, true, "tent_sleep_native_prompt_gate_closed")]
    public void NativePromptGatesFailClosed(bool hasMoved, bool shouldTimePass, bool passedOut, string expected)
    {
        var snapshot = Snapshot(hasMoved, shouldTimePass, passedOut);
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Contains(expected, item.BlockingReasons);
    }

    [Fact]
    public void CanonicalGrabGeometryCannotBeOverridden()
    {
        var snapshot = Snapshot();
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Select(row => row.Name == "stand_tile_x" ? P(row.Name, "12") : row)
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("tent_sleep_canonical_grab_geometry_required", item.BlockingReasons);
        Assert.Contains("tent_sleep_projection_geometry_drifted", item.BlockingReasons);
    }

    [Fact]
    public void TentSleepUsesSharedCrossDayStateMachineWithoutDirectWorldMutation()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("recovery.sleep_in_tent");
        Assert.Equal(CapabilityCandidateStatus.Declared, capability.CandidateStatus);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.False(capability.PolicyTrainingCandidate);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, capability.CompilerStatus);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.OutputTrainingGate);
        Assert.Equal(ImplementationEngineIds.RecoveryTiming,
            OptionImplementationCatalog.GetRequired("recovery.sleep_in_tent").PrimaryEngineId);
        Assert.False(PendingSemanticActionCatalog.TryGet("recovery.sleep_in_tent", out _));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Sleep.cs"));
        var tentRuntime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.TentSleep.cs"));
        var state = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.State.RecoveryShipping.cs"));
        Assert.Contains("StartTentSleep", tentRuntime, StringComparison.Ordinal);
        Assert.Contains("TryOpenNativeTentSleepPrompt", tentRuntime, StringComparison.Ordinal);
        Assert.Contains("location.checkAction", tentRuntime, StringComparison.Ordinal);
        Assert.Contains("SleepMode.Tent", runtime, StringComparison.Ordinal);
        Assert.Contains("private sealed class ActiveSleep", state, StringComparison.Ordinal);
        Assert.DoesNotContain("sleptInTemporaryBed.Value =", runtime + tentRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("health.Value = 0", runtime + tentRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("largeTerrainFeatures.Remove", runtime + tentRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.NewDay", runtime, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot) => new()
    {
        ModelOutputId = "tent-sleep-test",
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
                ActionId = "sleep-in-tent",
                OptionId = "recovery.sleep_in_tent",
                Rationale = "end the committed remote day at the placed temporary sleep endpoint",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "13"), P("target_tile_y", "10"),
                    P("stand_tile_x", "13"), P("stand_tile_y", "11"), P("direction", "0"),
                    P("target_runtime_type", "StardewValley.TerrainFeatures.Tent"), P("tent_health_before", "5"),
                    P("native_question_key", "SleepTent"), P("native_confirm_action_key", "SleepTent_Yes"),
                    P("native_contract", NativeContract), P("compiler_context.is_terminal_step", "true")
                }
            }
        }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot(bool hasMoved = true, bool shouldTimePass = true, bool passedOut = false)
    {
        var json = $$$"""
        {
          "time":{"time":{"value":2200,"status":"available"},"total_days":{"value":1,"status":"available"}},
          "player":{
            "location_id":{"value":"Farm","status":"available"},"tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "temporary_sleep":{"value":{"is_in_bed":false,"slept_in_temporary_bed":false,"last_sleep_location":"FarmHouse","last_sleep_point_x":9,"last_sleep_point_y":9},"status":"available"}
          },
          "current_location":{"large_terrain_features":{"value":[{
            "tile_x":13,"tile_y":10,"runtime_type":"StardewValley.TerrainFeatures.Tent","is_tent":true,"health":5,
            "passable_for_player":true,"passable_without_character":false,"player_has_moved":{{{hasMoved.ToString().ToLowerInvariant()}}},
            "player_passed_out":{{{passedOut.ToString().ToLowerInvariant()}}},"game_new_day":false,"time_should_pass":{{{shouldTimePass.ToString().ToLowerInvariant()}}},
            "sleep_location_id":"Farm","sleep_interaction_tile_x":13,"sleep_interaction_tile_y":10,
            "canonical_sleep_stand_tile_x":13,"canonical_sleep_stand_tile_y":11,"canonical_sleep_facing_direction":0,
            "native_sleep_question_key":"SleepTent","native_sleep_confirm_action_key":"SleepTent_Yes","slept_in_temporary_bed":false
          }],"status":"available"}},
          "menus":{
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"},
            "tent_sleep_prompt_context":{"value":{"prompt_open":false,"expected_question_key":"SleepTent","confirm_action_key":"SleepTent_Yes"},"status":"available"}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":30,"height":30,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
          }
        }
        """;
        return new SnapshotEnvelope
        {
            StateHash = "tent-sleep-hash",
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
