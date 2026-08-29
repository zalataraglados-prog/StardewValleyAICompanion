using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class MonsterMuskMainlineTests
{
    private const string OptionId = "executor.use_monster_musk";
    private const string NativeContract =
        "Object.performUseAction((O)879)->750ms_callback_Object.MonsterMusk->Farmer.applyBuff(24)->BuffManager.Apply_remove_then_replace";

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 120000)]
    public void ExactNativeUseCompilesForInitialApplyAndRefresh(bool activeBefore, int remainingBefore)
    {
        var snapshot = Snapshot(activeBefore, remainingBefore);
        var item = Assert.Single(new ActionQueueCompiler().Compile(
            Request(snapshot, activeBefore, remainingBefore), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("use_monster_musk", step.StepType);
        Assert.Equal("Mine:slot2:(O)879", step.Target);
        Assert.Contains("inventory_stack=1", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("buff_id=24", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("buff_duration_ms=600000", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("ordinary_mine_multiplier=2", step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("volcano_multiplier=2", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("monster_musk_projection_fingerprint", "drifted", "use_monster_musk_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_after", "2", "use_monster_musk_inventory_identity_drifted")]
    [InlineData("buff_duration_ms", "500000", "use_monster_musk_buff_contract_drifted")]
    [InlineData("buff_remaining_before_ms", "600000", "use_monster_musk_active_buff_drifted")]
    [InlineData("ordinary_mine_spawn_multiplier", "1", "use_monster_musk_spawn_semantics_drifted")]
    [InlineData("native_callback_delay_ms", "0", "use_monster_musk_animation_contract_drifted")]
    [InlineData("native_contract", "direct_apply", "use_monster_musk_native_contract_drifted")]
    public void StaleIdentityBuffSpawnAndAnimationClaimsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot(activeBefore: true, remainingBefore: 120000);
        var request = Request(snapshot, activeBefore: true, remainingBefore: 120000);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains(reason, item.BlockingReasons);
    }

    [Fact]
    public void MonsterMuskClosesFiveGatesAsMechanicalExecutorCalibrationOnly()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired(OptionId);

        Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
        Assert.Equal(new[] { "EVD-287" }, capability.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-287" }, capability.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-287" }, capability.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-287" }, capability.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-287" }, capability.OutputEvidenceIds);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(OptionInvocationPolicy.PolicyOrAutonomous, capability.InvocationPolicy);
        Assert.Contains(TrainingAdmissionExclusionReason.NotPolicyTrainingOption, capability.TrainingExclusionReasons);
        Assert.DoesNotContain(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired(OptionId).PrimaryEngineId);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));
    }

    [Fact]
    public void RuntimeUsesSharedNativeObjectUseAndNeverMutatesBuffOrEffectsDirectly()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.MonsterMusk.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MonsterMusk.cs"));
        var smoke = File.ReadAllText(Path.Combine(root, "scripts", "Invoke-RuntimeMonsterMuskSmoke.ps1"));

        Assert.Contains("UseInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.Contains("Game1.player.buffs.AppliedBuffs", runtime, StringComparison.Ordinal);
        Assert.Contains("MonsterMuskActiveBuffMatchesRequest", runtime, StringComparison.Ordinal);
        Assert.Contains("DataLoader.Buffs", projection, StringComparison.Ordinal);
        Assert.Contains("AnyOnlineFarmerHasBuff(\"24\")", projection, StringComparison.Ordinal);
        Assert.Contains("onlineFarmer.hasBuff(\"24\")", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.applyBuff(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.buffs.Apply(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AppliedBuffs[", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("broadcastSprites(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("playSound(", runtime, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot, bool activeBefore, int remainingBefore) => new()
    {
        ModelOutputId = "monster-musk-test",
        SourceModel = "compiler-owned-mechanical-test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.monster_drop_collection",
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
                ActionId = "use-monster-musk",
                OptionId = OptionId,
                Rationale = "combat plan selected native monster density buff",
                Parameters = new[]
                {
                    P("target_location", "Mine"), P("inventory_slot_index", "2"),
                    P("item_id", "879"), P("qualified_item_id", "(O)879"),
                    P("inventory_runtime_type", "StardewValley.Object"),
                    P("inventory_stack_before", "2"), P("inventory_stack_after", "1"),
                    P("monster_musk_projection_fingerprint", "monster-musk-fingerprint"),
                    P("buff_id", "24"), P("buff_active_before", activeBefore.ToString().ToLowerInvariant()),
                    P("buff_remaining_before_ms", remainingBefore.ToString()),
                    P("buff_total_before_ms", activeBefore ? "600000" : "0"),
                    P("buff_duration_ms", "600000"), P("buff_max_duration_ms", "-1"),
                    P("buff_is_debuff", "false"), P("buff_icon_sprite_index", "24"),
                    P("buff_icon_texture", "TileSheets\\BuffsIcons"), P("buff_glow_color", "#2000203F"),
                    P("buff_effects_empty", "true"), P("buff_actions_on_apply_count", "0"),
                    P("buff_reapply_semantics", "remove_same_id_then_replace"),
                    P("ordinary_mine_spawn_multiplier", "2"), P("volcano_spawn_multiplier", "2"),
                    P("repellent_buff_id", "23"),
                    P("native_facing_direction", "2"), P("native_freeze_pause_ms", "1750"),
                    P("native_callback_delay_ms", "750"), P("native_followup_animation_ms", "1400"),
                    P("native_sprite_count", "3"), P("native_sprite_delays_ms", "0,100,200"),
                    P("native_sprite_motion_x_domain", "random_float[-1,1]"),
                    P("native_initial_sound", "steam"), P("native_callback_sound", "croak"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(bool activeBefore, int remainingBefore)
    {
        var active = activeBefore
            ? $$$"""{"active":true,"remaining_ms":{{{remainingBefore}}},"total_ms":600000}"""
            : "{\"active\":false,\"remaining_ms\":0,\"total_ms\":0}";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Mine","status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"879","qualified_item_id":"(O)879","stack":2}],"status":"available"},
            "monster_musk":{"value":{
              "projection_fingerprint":"monster-musk-fingerprint","native_use_gate_status":"ready",
              "native_contract":"{{{NativeContract}}}",
              "buff_contract":{"id":"24","duration_ms":600000,"max_duration_ms":-1,"is_debuff":false,
                "icon_sprite_index":24,"icon_texture":"TileSheets\\BuffsIcons","glow_color":"#2000203F",
                "effects_empty":true,"actions_on_apply_count":0,"reapply_semantics":"remove_same_id_then_replace"},
              "active_buff":{{{active}}},
              "spawn_semantics":{"ordinary_mine_multiplier":2,"volcano_multiplier":2,"repellent_buff_id":"23"},
              "animation_contract":{"facing_direction":2,"freeze_pause_ms":1750,"callback_delay_ms":750,
                "followup_animation_ms":1400,"sprite_count":3,"sprite_delays_ms":"0,100,200",
                "sprite_motion_x_domain":"random_float[-1,1]","initial_sound":"steam","callback_sound":"croak"},
              "rows":[{"inventory_slot_index":2,"item_id":"879","qualified_item_id":"(O)879",
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
