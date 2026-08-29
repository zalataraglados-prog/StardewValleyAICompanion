using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class TreasureTotemMainlineTests
{
    private const string OptionId = "executor.use_treasure_totem";
    private const string SpawnTilesJson = "[{\"tile_x\":7,\"tile_y\":10},{\"tile_x\":9,\"tile_y\":7},{\"tile_x\":13,\"tile_y\":10}]";
    private const string NativeContract =
        "Object.performUseAction((O)TreasureTotem)->outdoors_guard->Object.treasureTotem->TreasureTotemsUsed++->rounded_distance_3_ring->placement_occupancy_front_bush_diggable_or_winter_grass_gate->objects.Add((O)590)";

    [Fact]
    public void ExactNativeUseCompilesOneImmediateTreasureRingStep()
    {
        var snapshot = Snapshot();
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_treasure_totem", step.StepType);
        Assert.Equal("Farm:10,10:slot2:(O)TreasureTotem", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("treasure_totems_used=6", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("artifact_spots_spawned=3", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("treasure_totem_projection_fingerprint", "drifted", "use_treasure_totem_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_after", "2", "use_treasure_totem_inventory_identity_drifted")]
    [InlineData("center_tile_x", "11", "use_treasure_totem_center_tile_drifted")]
    [InlineData("expected_spawn_tiles_json", "[]", "use_treasure_totem_spawn_projection_drifted")]
    [InlineData("treasure_totems_used_after", "7", "use_treasure_totem_counter_projection_drifted")]
    [InlineData("native_initial_sound", "silent", "use_treasure_totem_native_contract_drifted")]
    [InlineData("native_contract", "direct_objects_add", "use_treasure_totem_native_contract_drifted")]
    public void StaleInventoryTileSpawnCounterAndNativeClaimsFailClosed(
        string parameter,
        string value,
        string reason)
    {
        var snapshot = Snapshot();
        var request = Request(snapshot);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains(reason, item.BlockingReasons);
    }

    [Theory]
    [InlineData("blocked_location_not_outdoors", true, 3)]
    [InlineData("blocked_no_spawnable_ring_tiles", false, 0)]
    public void InvalidOrWastefulNativeConsumptionIsExcludedUpstream(
        string gate,
        bool indoors,
        int expectedSpawnCount)
    {
        var snapshot = Snapshot(gate, indoors, expectedSpawnCount);
        var request = Request(snapshot, expectedSpawnCount);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("use_treasure_totem_native_effect_gate_blocked", item.BlockingReasons);
        Assert.Equal(gate, snapshot.State["player"].GetProperty("treasure_totem")
            .GetProperty("value").GetProperty("native_use_gate_status").GetString());
    }

    [Fact]
    public void TreasureTotemClosesFiveGatesAsMechanicalExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-290" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-290" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-290" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-290" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-290" }, capability.OutputEvidenceIds);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(OptionInvocationPolicy.PolicyOrAutonomous, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.NotPolicyTrainingOption, capability.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(ImplementationEngineIds.ToolHarvest,
            OptionImplementationCatalog.GetRequired(OptionId).PrimaryEngineId);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void RuntimeUsesSharedNativeObjectUseAndNeverSynthesizesWorldOrInventoryMutation()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.TreasureTotem.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.TreasureTotem.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeTreasureTotemSmoke.ps1"));

        Assert.Contains("UseInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("CanItemBePlacedHere", projection, StringComparison.Ordinal);
        Assert.Contains("IsTileOccupiedBy", projection, StringComparison.Ordinal);
        Assert.Contains("doesTileHaveProperty", projection, StringComparison.Ordinal);
        Assert.Contains("TreasureTotemsUsed", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("location.objects.Add(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.netWorldState.Value.TreasureTotemsUsed++;", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("playSound(", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot, int expectedSpawnCount = 3) => new()
    {
        ModelOutputId = "treasure-totem-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.foraging_treasure_search",
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
                ActionId = "use-treasure-totem",
                OptionId = OptionId,
                Rationale = "day planner selected exact native artifact-spot generation",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("inventory_slot_index", "2"),
                    P("item_id", "TreasureTotem"), P("qualified_item_id", "(O)TreasureTotem"),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "2"), P("inventory_stack_after", "1"),
                    P("treasure_totem_projection_fingerprint", "treasure-totem-fingerprint"),
                    P("center_tile_x", "10"), P("center_tile_y", "10"),
                    P("ring_candidate_count", "16"), P("expected_spawn_count", expectedSpawnCount.ToString()),
                    P("expected_spawn_tiles_json", expectedSpawnCount == 0 ? "[]" : SpawnTilesJson),
                    P("existing_artifact_spot_count_before", "1"),
                    P("existing_artifact_spot_count_after", (1 + expectedSpawnCount).ToString()),
                    P("treasure_totems_used_before", "5"), P("treasure_totems_used_after", "6"),
                    P("native_ring_scan_radius", "4"), P("native_rounded_radius", "3"),
                    P("artifact_spot_qualified_item_id", "(O)590"),
                    P("native_initial_sound", "treasure_totem"), P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string gate = "ready",
        bool indoors = false,
        int expectedSpawnCount = 3)
    {
        var spawnTilesJson = expectedSpawnCount == 0 ? "[]" : SpawnTilesJson;
        var escapedSpawnTilesJson = JsonSerializer.Serialize(spawnTilesJson);
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"TreasureTotem","qualified_item_id":"(O)TreasureTotem","stack":2}],"status":"available"},
            "treasure_totem":{"value":{
              "projection_fingerprint":"treasure-totem-fingerprint","native_use_gate_status":"{{{gate}}}",
              "native_contract":"{{{NativeContract}}}","location_is_outdoors":{{{(!indoors).ToString().ToLowerInvariant()}}},
              "center_tile":{"tile_x":10,"tile_y":10},
              "spawn_projection":{"ring_candidate_count":16,"expected_spawn_count":{{{expectedSpawnCount}}},
                "expected_spawn_tiles_json":{{{escapedSpawnTilesJson}}},
                "existing_artifact_spot_count_before":1,"existing_artifact_spot_count_after":{{{1 + expectedSpawnCount}}},
                "treasure_totems_used_before":5,"treasure_totems_used_after":6},
              "ring_contract":{"scan_radius":4,"rounded_radius":3,"artifact_spot_qualified_item_id":"(O)590",
                "initial_sound":"treasure_totem"},
              "rows":[{"inventory_slot_index":2,"item_id":"TreasureTotem","qualified_item_id":"(O)TreasureTotem",
                "inventory_runtime_type":"StardewValley.Object","stack_before":2,"stack_after":1,"temporarily_invisible":false}]
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
