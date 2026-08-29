using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class WarpTotemMainlineTests
{
    private const string OptionId = "executor.use_warp_totem";
    private const string NativeContract =
        "Object.performUseAction((O)261|688|689|690|886)->2000ms_totem_animation->Object.totemWarp->1000ms_fadeAfterDelay->Object.totemWarpForReal->Farm_WarpTotemEntry_or_variant_destination->Game1.warpFarmer->active_or_passive_festival_routing";

    [Theory]
    [InlineData("688", "Farm", 48, 7)]
    [InlineData("689", "Mountain", 31, 20)]
    [InlineData("690", "Beach", 20, 4)]
    [InlineData("261", "Desert", 35, 43)]
    [InlineData("886", "IslandSouth", 11, 11)]
    public void EveryExactVanillaVariantCompilesOneNativeDelayedWarp(
        string itemId,
        string destination,
        int x,
        int y)
    {
        var snapshot = Snapshot(itemId, destination, destination, x, y);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, destination, destination, x, y), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_warp_totem", step.StepType);
        Assert.Equal($"{destination}:{x},{y}:slot2:(O){itemId}", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("route_mode=ordinary", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void PassiveFestivalReplacementIsCompilerBoundInsteadOfAssumingTheBaseMap()
    {
        const string routeJson = "[{\"festival_id\":\"NightMarket\",\"source_location_id\":\"Beach\",\"replacement_location_id\":\"BeachNightMarket\"}]";
        var snapshot = Snapshot("690", "Beach", "BeachNightMarket", 20, 4,
            routeMode: "passive_festival_replacement", passiveRouteJson: routeJson);
        var request = Request(snapshot, "690", "Beach", "BeachNightMarket", 20, 4,
            routeMode: "passive_festival_replacement", passiveRouteJson: routeJson);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Equal("BeachNightMarket:20,4:slot2:(O)690", Assert.Single(item.NormalizedCommand.Steps).Target);
    }

    [Fact]
    public void SinglePlayerActiveFestivalEntryBindsNativeEventStartTile()
    {
        var snapshot = Snapshot("690", "Beach", "Beach", 38, 3,
            routeMode: "active_festival_entry", festivalId: "summer11", festivalStart: 900,
            festivalEnd: 1400, festivalEntryX: 38, festivalEntryY: 3, festivalEntryFacing: 2);
        var request = Request(snapshot, "690", "Beach", "Beach", 38, 3,
            routeMode: "active_festival_entry", festivalId: "summer11", festivalStart: 900,
            festivalEnd: 1400, festivalEntryX: 38, festivalEntryY: 3, festivalEntryFacing: 2);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        Assert.Contains("route_mode=active_festival_entry", Assert.Single(item.NormalizedCommand.Steps).ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("warp_totem_projection_fingerprint", "drifted", "use_warp_totem_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_after", "2", "use_warp_totem_inventory_identity_drifted")]
    [InlineData("effective_destination_location_id", "Beach", "use_warp_totem_destination_route_drifted")]
    [InlineData("effective_destination_tile_x", "21", "use_warp_totem_destination_route_drifted")]
    [InlineData("passive_festival_route_json", "[]", "use_warp_totem_destination_route_drifted")]
    [InlineData("native_animation_duration_ms", "1999", "use_warp_totem_animation_contract_drifted")]
    [InlineData("native_contract", "direct_warp", "use_warp_totem_native_contract_drifted")]
    public void StaleInventoryDestinationFestivalAndTimingClaimsFailClosed(
        string parameter,
        string value,
        string reason)
    {
        const string routeJson = "[{\"festival_id\":\"NightMarket\",\"source_location_id\":\"Beach\",\"replacement_location_id\":\"BeachNightMarket\"}]";
        var snapshot = Snapshot("690", "Beach", "BeachNightMarket", 20, 4,
            routeMode: "passive_festival_replacement", passiveRouteJson: routeJson);
        var request = Request(snapshot, "690", "Beach", "BeachNightMarket", 20, 4,
            routeMode: "passive_festival_replacement", passiveRouteJson: routeJson);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains(reason, item.BlockingReasons);
    }

    [Theory]
    [InlineData("blocked_festival_not_started_consumption_without_warp")]
    [InlineData("blocked_multiplayer_festival_ready_check_required")]
    [InlineData("blocked_already_at_exact_destination")]
    [InlineData("blocked_base_object_use_gate")]
    public void UnsafeOrWastefulNativeConsumptionIsExcludedUpstream(string gate)
    {
        var snapshot = Snapshot("690", "Beach", "Beach", 20, 4, gate: gate);
        var request = Request(snapshot, "690", "Beach", "Beach", 20, 4);

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("use_warp_totem_native_effect_gate_blocked", item.BlockingReasons);
    }

    [Fact]
    public void MissingSelectedInventoryRowFailsClosedWithoutThrowing()
    {
        var snapshot = Snapshot("688", "Farm", "Farm", 48, 7);
        var request = Request(snapshot, "688", "Farm", "Farm", 48, 7);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == "inventory_slot_index")).Value = "7";

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("use_warp_totem_inventory_identity_drifted", item.BlockingReasons);
    }

    [Fact]
    public void MissingAnimationContractFailsClosedWithoutThrowing()
    {
        var snapshot = Snapshot("688", "Farm", "Farm", 48, 7);
        var state = JsonNode.Parse(JsonSerializer.Serialize(snapshot.State))!.AsObject();
        state["player"]!["warp_totem"]!["value"]!.AsObject().Remove("native_animation_contract");
        var drifted = new SnapshotEnvelope
        {
            SchemaVersion = snapshot.SchemaVersion,
            StateHash = snapshot.StateHash,
            GameTick = snapshot.GameTick,
            RealTimestamp = snapshot.RealTimestamp,
            Completeness = snapshot.Completeness,
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(state.ToJsonString())!
        };

        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(drifted, "688", "Farm", "Farm", 48, 7), drifted).Items);

        Assert.Contains("use_warp_totem_animation_contract_drifted", item.BlockingReasons);
    }

    [Fact]
    public void WarpTotemClosesFiveGatesAsMechanicalExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-291" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-291" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-291" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-291" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-291" }, capability.OutputEvidenceIds);
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
    public void RuntimeUsesSharedNativeObjectUseAndNeverSynthesizesWarpOrInventoryMutation()
    {
        var root = FindRepositoryRoot();
        var runtimePath = Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.WarpTotem.cs");
        var projectionPath = Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.WarpTotem.cs");
        var smokePath = Path.Combine(root, "scripts", "Invoke-RuntimeWarpTotemSmoke.ps1");

        Assert.True(File.Exists(runtimePath));
        Assert.True(File.Exists(projectionPath));
        Assert.True(File.Exists(smokePath));
        var runtime = File.ReadAllText(runtimePath);
        var projection = File.ReadAllText(projectionPath);
        var smoke = File.ReadAllText(smokePath);

        Assert.Contains("UseInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("TryGetMapPropertyAs(\"WarpTotemEntry\"", projection, StringComparison.Ordinal);
        Assert.Contains("ActivePassiveFestivals", projection, StringComparison.Ordinal);
        Assert.Contains(@"Data\\Festivals", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("playSound(", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot,
        string itemId,
        string baseDestination,
        string effectiveDestination,
        int x,
        int y,
        string routeMode = "ordinary",
        string passiveRouteJson = "[]",
        string festivalId = "",
        int festivalStart = -1,
        int festivalEnd = -1,
        int festivalEntryX = -1,
        int festivalEntryY = -1,
        int festivalEntryFacing = -1) => new()
    {
        ModelOutputId = "warp-totem-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.travel",
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
                ActionId = "use-warp-totem",
                OptionId = OptionId,
                Rationale = "day planner selected exact native fast-travel destination",
                Parameters = new[]
                {
                    P("target_location", "FarmHouse"), P("inventory_slot_index", "2"),
                    P("item_id", itemId), P("qualified_item_id", "(O)" + itemId),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "2"), P("inventory_stack_after", "1"),
                    P("warp_totem_projection_fingerprint", "warp-totem-fingerprint"),
                    P("base_destination_location_id", baseDestination),
                    P("requested_destination_tile_x", x.ToString()), P("requested_destination_tile_y", y.ToString()),
                    P("effective_destination_location_id", effectiveDestination),
                    P("effective_destination_tile_x", x.ToString()), P("effective_destination_tile_y", y.ToString()),
                    P("destination_route_mode", routeMode), P("farm_destination_source", itemId == "688" ? "fallback_default" : "fixed_variant"),
                    P("passive_festival_route_json", passiveRouteJson),
                    P("active_festival_id", festivalId), P("active_festival_start_time", festivalStart.ToString()),
                    P("active_festival_end_time", festivalEnd.ToString()),
                    P("active_festival_entry_tile_x", festivalEntryX.ToString()),
                    P("active_festival_entry_tile_y", festivalEntryY.ToString()),
                    P("active_festival_entry_facing", festivalEntryFacing.ToString()),
                    P("festival_prestart_warp_cancelled", "false"), P("festival_ready_check_required", "false"),
                    P("native_facing_direction", "2"), P("native_animation_duration_ms", "2000"),
                    P("native_totem_callback_delay_ms", "1000"), P("native_initial_item_sprite_count", "3"),
                    P("native_sprinkle_sprite_count", "65"), P("native_poof_sprite_count", "12"),
                    P("native_trail_sprite_count", "17"), P("native_initial_sound", "warrior"),
                    P("native_warp_sound", "wand"),
                    P("native_glow_color_rgba", itemId == "688" ? "LimeGreen" : itemId == "689" ? "OrangeRed" : itemId == "261" ? "255,200,0,255" : "LightBlue"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId,
        string baseDestination,
        string effectiveDestination,
        int x,
        int y,
        string routeMode = "ordinary",
        string passiveRouteJson = "[]",
        string festivalId = "",
        int festivalStart = -1,
        int festivalEnd = -1,
        int festivalEntryX = -1,
        int festivalEntryY = -1,
        int festivalEntryFacing = -1,
        string gate = "ready")
    {
        var passiveJsonString = JsonSerializer.Serialize(passiveRouteJson);
        var glowColor = itemId == "688" ? "LimeGreen" : itemId == "689" ? "OrangeRed" :
            itemId == "261" ? "255,200,0,255" : "LightBlue";
        var farmSource = itemId == "688" ? "fallback_default" : "fixed_variant";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":4,"status":"available"},
            "tile_y":{"value":4,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"(O){{{itemId}}}","stack":2}],"status":"available"},
            "warp_totem":{"value":{
              "projection_fingerprint":"warp-totem-fingerprint","native_use_gate_status":"{{{gate}}}",
              "native_contract":"{{{NativeContract}}}",
              "native_animation_contract":{"facing_direction":2,"animation_duration_ms":2000,
                "totem_callback_delay_ms":1000,"initial_item_sprite_count":3,"sprinkle_sprite_count":65,
                "poof_sprite_count":12,"trail_sprite_count":17,"initial_sound":"warrior","warp_sound":"wand"},
              "rows":[{"inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"(O){{{itemId}}}",
                "inventory_runtime_type":"StardewValley.Object","stack_before":2,"stack_after":1,
                "temporarily_invisible":false,"native_use_gate_status":"{{{gate}}}",
                "glow_color_rgba":"{{{glowColor}}}",
                "destination_route":{"base_destination_location_id":"{{{baseDestination}}}",
                  "requested_destination_tile_x":{{{x}}},"requested_destination_tile_y":{{{y}}},
                  "effective_destination_location_id":"{{{effectiveDestination}}}",
                  "effective_destination_tile_x":{{{x}}},"effective_destination_tile_y":{{{y}}},
                  "destination_route_mode":"{{{routeMode}}}",
                  "farm_destination_source":"{{{farmSource}}}",
                  "passive_festival_route_json":{{{passiveJsonString}}},
                  "active_festival_id":"{{{festivalId}}}","active_festival_start_time":{{{festivalStart}}},
                  "active_festival_end_time":{{{festivalEnd}}},
                  "active_festival_entry_tile_x":{{{festivalEntryX}}},
                  "active_festival_entry_tile_y":{{{festivalEntryY}}},
                  "active_festival_entry_facing":{{{festivalEntryFacing}}},
                  "festival_prestart_warp_cancelled":false,"festival_ready_check_required":false}}]
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
