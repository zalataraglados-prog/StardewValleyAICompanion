using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class FireworkMainlineTests
{
    private const string OptionId = "executor.use_firework";
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)893|(O)894|(O)895)->broadcastSprites+netAudio(fuse)+DelayedAction.StopPlaying(fuse)";
    private const string RandomContract = "live_Game1.random_runtime_only_no_read_side_rng_advance";

    [Theory]
    [InlineData("893", "(O)893", 0, 256)]
    [InlineData("894", "(O)894", 1, 272)]
    [InlineData("895", "(O)895", 2, 288)]
    public void AllThreeNativeVariantsCompileToOneExactPlacement(
        string itemId, string qualifiedItemId, int fireworkType, int sourceRectX)
    {
        var snapshot = Snapshot(itemId, qualifiedItemId, fireworkType, sourceRectX);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, qualifiedItemId, fireworkType, sourceRectX), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_firework", step.StepType);
        Assert.Equal($"Farm(11,10):slot2:{qualifiedItemId}", step.Target);
        Assert.Contains("firework_type=" + fireworkType, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("inventory_stack=0", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("fuse_duration_ms=2400", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("firework_projection_fingerprint", "drifted", "use_firework_projection_fingerprint_drifted")]
    [InlineData("firework_type", "2", "use_firework_inventory_or_variant_identity_drifted")]
    [InlineData("firework_source_rect_x", "288", "use_firework_inventory_or_variant_identity_drifted")]
    [InlineData("firework_random_contract", "predict_exact_rng", "use_firework_random_contract_drifted")]
    [InlineData("target_tile_x", "12", "use_firework_exact_tile_not_native_legal")]
    public void StaleVariantTargetAndRandomClaimsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot("893", "(O)893", 0, 256);
        var request = Request(snapshot, "893", "(O)893", 0, 256);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(reason, item.BlockingReasons);
    }

    [Fact]
    public void ExactTileOccupiedByTemporarySpriteFailsClosed()
    {
        var snapshot = Snapshot("893", "(O)893", 0, 256, transientBlocked: true);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, "893", "(O)893", 0, 256), snapshot).Items);

        Assert.Contains("use_firework_exact_tile_transiently_occupied", item.BlockingReasons);
    }

    [Fact]
    public void FireworkClosesFiveGatesButRemainsExplicitPlayerCommandOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-285" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-285" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-285" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-285" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-285" }, capability.OutputEvidenceIds);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.PlayerCommandOnly, capability.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired(OptionId).PrimaryEngineId);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void RuntimeUsesSharedNativePlacementAndSmokeCoversEveryVariantSilently()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Fireworks.cs"));
        var shared = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeObjectPlacement.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Fireworks.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeFireworkSmoke.ps1"));

        Assert.Contains("CanPlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("Utility.playerCanPlaceItemHere", shared, StringComparison.Ordinal);
        Assert.Contains("Utility.tryToPlaceItem", shared, StringComparison.Ordinal);
        Assert.Contains("temporarySprites", projection, StringComparison.Ordinal);
        Assert.Contains(RandomContract, projection, StringComparison.Ordinal);
        Assert.DoesNotContain("broadcastSprites(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("temporarySprites.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random.Next(", projection, StringComparison.Ordinal);
        Assert.Contains("(O)893", smoke, StringComparison.Ordinal);
        Assert.Contains("(O)894", smoke, StringComparison.Ordinal);
        Assert.Contains("(O)895", smoke, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string itemId, string qualifiedItemId, int fireworkType, int sourceRectX) => new()
    {
        ModelOutputId = "firework-test",
        SourceModel = "explicit-player-command-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.firework",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "use-firework",
                OptionId = OptionId,
                Rationale = "explicit player request",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "11"), P("target_tile_y", "10"),
                    P("stand_tile_x", "10"), P("stand_tile_y", "10"), P("inventory_slot_index", "2"),
                    P("item_id", itemId), P("qualified_item_id", qualifiedItemId), P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "1"), P("inventory_stack_after", "0"),
                    P("firework_type", fireworkType.ToString()), P("firework_source_rect_x", sourceRectX.ToString()),
                    P("firework_source_rect_y", "397"), P("firework_fuse_duration_ms", "2400"),
                    P("firework_rocket_delay_ms", "2400"), P("firework_rocket_id_min", "20"), P("firework_rocket_id_max", "30"),
                    P("firework_acceleration_y_min", "-0.36"), P("firework_acceleration_y_max", "-0.27"), P("firework_acceleration_y_step", "0.01"),
                    P("firework_random_contract", RandomContract), P("firework_projection_fingerprint", "firework-fingerprint"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId, string qualifiedItemId, int fireworkType, int sourceRectX, bool transientBlocked = false)
    {
        var blocked = transientBlocked ? "[{\"tile_x\":11,\"tile_y\":10}]" : "[]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qualifiedItemId}}}","stack":1}],"status":"available"},
            "firework_placement":{"value":{
              "projection_fingerprint":"firework-fingerprint",
              "random_outcome_contract":"{{{RandomContract}}}",
              "rows":[{
                "inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qualifiedItemId}}}","inventory_runtime_type":"StardewValley.Object",
                "stack_before":1,"stack_after":0,"firework_type":{{{fireworkType}}},"source_rect_x":{{{sourceRectX}}},"source_rect_y":397,
                "fuse_duration_ms":2400,"rocket_delay_ms":2400,"rocket_id_min":20,"rocket_id_max":30,
                "acceleration_y_min":-0.36,"acceleration_y_max":-0.27,"acceleration_y_step":0.01,"native_contract":"{{{NativeContract}}}",
                "locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available",
                  "static_legal_tile_ranges":[{"y":10,"start_x":11,"end_x":11}],"temporary_sprite_blocked_tiles":{{{blocked}}}}]
              }]
            },"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-29T00:00:00Z", Completeness = "complete", State = state
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
