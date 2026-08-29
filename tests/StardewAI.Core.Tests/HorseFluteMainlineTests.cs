using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class HorseFluteMainlineTests
{
    private const string OptionId = "executor.use_horse_flute";
    private const string NativeContract =
        "Object.performUseAction((O)911)->Utility.GetHorseWarpRestrictionsForFarmer(start+delayed)->FarmerTeam.requestHorseWarpEvent->OnRequestHorseWarp->Horse.mutex->Game1.warpCharacter";

    [Theory]
    [InlineData(false, "summon_after_1500ms", 1500, 2)]
    [InlineData(true, "already_adjacent_no_warp", 0, 1)]
    public void ExactNativeSuccessBranchesCompileWithoutConsumingFlute(
        bool horseNearby, string expectedResult, int expectedDelayMs, int expectedFacingDirection)
    {
        var snapshot = Snapshot(horseNearby);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, horseNearby, expectedResult, expectedDelayMs), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_horse_flute", step.StepType);
        Assert.Equal("Farm:slot2:(O)911", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("result=" + expectedResult, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("delay_ms=" + expectedDelayMs, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Equal(expectedFacingDirection.ToString(),
            Request(snapshot, horseNearby, expectedResult, expectedDelayMs).Actions[0].Parameters
                .Single(row => row.Name == "facing_direction").Value);
    }

    [Theory]
    [InlineData("horse_flute_projection_fingerprint", "drifted", "use_horse_flute_projection_fingerprint_drifted")]
    [InlineData("horse_warp_restrictions", "2", "use_horse_flute_native_restrictions_drifted")]
    [InlineData("owned_horse_id", "drifted-horse", "use_horse_flute_owned_horse_identity_drifted")]
    [InlineData("team_event_stable_horse_id", "drifted-horse", "use_horse_flute_team_event_stable_binding_drifted")]
    [InlineData("expected_result", "already_adjacent_no_warp", "use_horse_flute_result_projection_drifted")]
    [InlineData("use_delay_ms", "0", "use_horse_flute_native_timing_drifted")]
    [InlineData("facing_direction", "1", "use_horse_flute_native_timing_drifted")]
    [InlineData("inventory_stack_after", "0", "use_horse_flute_inventory_identity_drifted")]
    public void StaleIdentityRestrictionTimingAndConsumptionClaimsFailClosed(
        string parameter, string value, string reason)
    {
        var snapshot = Snapshot(horseNearby: false);
        var request = Request(snapshot, horseNearby: false, "summon_after_1500ms", 1500);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(reason, item.BlockingReasons);
    }

    [Fact]
    public void MissingOwnedHorseProjectionFailsClosedWithoutThrowing()
    {
        var snapshot = Snapshot(horseNearby: false, includeOwnedHorse: false);

        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, horseNearby: false, "summon_after_1500ms", 1500), snapshot).Items);

        Assert.Contains("use_horse_flute_owned_horse_identity_drifted", item.BlockingReasons);
        Assert.Contains("use_horse_flute_team_event_stable_binding_drifted", item.BlockingReasons);
    }

    [Fact]
    public void HorseFluteClosesFiveGatesAsMechanicalExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-286" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-286" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-286" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-286" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-286" }, capability.OutputEvidenceIds);
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
    public void RuntimeUsesNativeObjectAndTeamWarpWithoutDirectHorseMutation()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.HorseFlute.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.HorseFlute.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeHorseFluteSmoke.ps1"));

        Assert.Contains("performUseAction", runtime, StringComparison.Ordinal);
        Assert.Contains("Utility.GetHorseWarpRestrictionsForFarmer", runtime, StringComparison.Ordinal);
        Assert.Contains("Utility.GetHorseWarpRestrictionsForFarmer", projection, StringComparison.Ordinal);
        Assert.Contains("Utility.findHorseForPlayer", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("warpCharacter(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("requestHorseWarpEvent.Fire", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("horse.Position =", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, bool horseNearby, string expectedResult, int expectedDelayMs) => new()
    {
        ModelOutputId = "horse-flute-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.route.acceleration",
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
                ActionId = "use-horse-flute",
                OptionId = OptionId,
                Rationale = "movement compiler selected a reusable horse summon",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("inventory_slot_index", "2"),
                    P("item_id", "911"), P("qualified_item_id", "(O)911"),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "1"), P("inventory_stack_after", "1"),
                    P("horse_flute_projection_fingerprint", "horse-flute-fingerprint"),
                    P("horse_warp_restrictions", "0"), P("horse_warp_restriction_names", "none"),
                    P("owned_horse_id", "11111111-1111-1111-1111-111111111111"),
                    P("owned_horse_location_id", "Farm"), P("owned_horse_tile_x", horseNearby ? "11" : "20"),
                    P("owned_horse_tile_y", horseNearby ? "11" : "20"), P("owned_horse_nearby", horseNearby.ToString().ToLowerInvariant()),
                    P("team_event_stable_horse_id", "11111111-1111-1111-1111-111111111111"),
                    P("team_event_stable_location_id", "Farm"), P("team_event_stable_tile_x", "5"),
                    P("team_event_stable_tile_y", "5"), P("team_event_stable_matches_owned_horse", "true"),
                    P("expected_result", expectedResult), P("use_delay_ms", expectedDelayMs.ToString()),
                    P("facing_direction", horseNearby ? "1" : "2"), P("freeze_pause_ms", expectedDelayMs.ToString()),
                    P("music_duck_ms", horseNearby ? "0" : "2000"), P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(bool horseNearby, bool includeOwnedHorse = true)
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"911","qualified_item_id":"(O)911","stack":1}],"status":"available"},
            "horse_flute":{"value":{
              "projection_fingerprint":"horse-flute-fingerprint","native_use_gate_status":"ready",
              "horse_warp_restrictions":0,"horse_warp_restriction_names":[],"native_contract":"{{{NativeContract}}}",
              "owned_horse":{"horse_id":"11111111-1111-1111-1111-111111111111","owner_player_id":"123",
                "location_id":"Farm","tile_x":{{{(horseNearby ? 11 : 20)}}},"tile_y":{{{(horseNearby ? 11 : 20)}}},
                "is_in_current_location":true,"is_nearby":{{{horseNearby.ToString().ToLowerInvariant()}}}},
              "team_event_stable_binding":{"stable_horse_id":"11111111-1111-1111-1111-111111111111",
                "stable_location_id":"Farm","stable_tile_x":5,"stable_tile_y":5,"matches_owned_horse":true},
              "expected_result":"{{{(horseNearby ? "already_adjacent_no_warp" : "summon_after_1500ms")}}}",
              "use_delay_ms":{{{(horseNearby ? 0 : 1500)}}},"facing_direction":{{{(horseNearby ? 1 : 2)}}},"freeze_pause_ms":{{{(horseNearby ? 0 : 1500)}}},
              "music_duck_ms":{{{(horseNearby ? 0 : 2000)}}},
              "rows":[{"inventory_slot_index":2,"item_id":"911","qualified_item_id":"(O)911",
                "inventory_runtime_type":"StardewValley.Object","stack_before":1,"stack_after":1,"temporarily_invisible":false}]
            },"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var root = JsonNode.Parse(json)!.AsObject();
        if (!includeOwnedHorse)
        {
            root["player"]!["horse_flute"]!["value"]!["owned_horse"] = null;
            root["player"]!["horse_flute"]!["value"]!["native_use_gate_status"] = "blocked_owned_horse_instance_unavailable";
        }
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(root.ToJsonString())!;
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
