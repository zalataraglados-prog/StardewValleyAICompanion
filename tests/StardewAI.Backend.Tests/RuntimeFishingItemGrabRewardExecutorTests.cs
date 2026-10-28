using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeFishingItemGrabRewardExecutorTests
{
    [Fact]
    public void RequestCarriesExactRewardAndDiscardBindings()
    {
        var json = JsonSerializer.Serialize(new TrainingExecutionRequest
        {
            OptionId = "executor.claim_fishing_item_grab_reward",
            FishingRewardMenuSource = 3,
            FishingRewardMenuContextType =
                "StardewValley.Tools.FishingRod",
            FishingRewardMenuSlotIndex = 1,
            FishingRewardQualifiedItemId = "(O)145",
            FishingRewardStackBefore = 1,
            FishingRewardQuality = 0,
            FishingRewardPendingCountBefore = 2,
            FishingRewardRequiresDiscard = true,
            FishingRewardDiscardSlotIndex = 7,
            FishingRewardDiscardQualifiedItemId = "(O)390",
            FishingRewardDiscardStackBefore = 3,
            FishingRewardDiscardQuality = 0,
            FishingRewardDiscardAuthorization =
                "objective_safe_unprotected_lowest_total_store_value"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(
            3,
            root.GetProperty("fishing_reward_menu_source").GetInt32());
        Assert.Equal(
            "StardewValley.Tools.FishingRod",
            root.GetProperty(
                "fishing_reward_menu_context_type").GetString());
        Assert.Equal(
            1,
            root.GetProperty(
                "fishing_reward_menu_slot_index").GetInt32());
        Assert.Equal(
            "(O)145",
            root.GetProperty(
                "fishing_reward_qualified_item_id").GetString());
        Assert.Equal(
            1,
            root.GetProperty("fishing_reward_stack_before").GetInt32());
        Assert.Equal(
            0,
            root.GetProperty("fishing_reward_quality").GetInt32());
        Assert.Equal(
            2,
            root.GetProperty(
                "fishing_reward_pending_count_before").GetInt32());
        Assert.True(root.GetProperty(
            "fishing_reward_requires_discard").GetBoolean());
        Assert.Equal(
            7,
            root.GetProperty(
                "fishing_reward_discard_slot_index").GetInt32());
        Assert.Equal(
            "(O)390",
            root.GetProperty(
                "fishing_reward_discard_qualified_item_id").GetString());
        Assert.Equal(
            3,
            root.GetProperty(
                "fishing_reward_discard_stack_before").GetInt32());
        Assert.Equal(
            0,
            root.GetProperty(
                "fishing_reward_discard_quality").GetInt32());
        Assert.Equal(
            "objective_safe_unprotected_lowest_total_store_value",
            root.GetProperty(
                "fishing_reward_discard_authorization").GetString());
    }

    [Fact]
    public void RuntimeUsesOnlyNativeMenuClicksAndFailsClosed()
    {
        var source = RuntimeHarnessSources.File(
            "ModEntry.FishingItemGrabReward.cs");

        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(
            "executor.claim_fishing_item_grab_reward"));
        Assert.Contains("menu.context is not FishingRod", source);
        Assert.Contains("menu.receiveLeftClick(", source);
        Assert.Contains("menu.trashCan.bounds.Center", source);
        Assert.Contains(
            "FishingRewardDiscardProtectionReasons(discard)",
            source);
        Assert.True(
            source.IndexOf(
                "var rewardPosition = InventorySlotScreenPosition(",
                StringComparison.Ordinal) <
            source.IndexOf(
                "TryDiscardAuthorizedFishingRewardInventoryStack(",
                StringComparison.Ordinal),
            "Reward menu geometry must be validated before any native discard click.");
        Assert.Contains(
            "discardedSlot >= Game1.player.maxItems.Value",
            source);
        Assert.Contains(
            "single_reward_slice_requires_fresh_snapshot_replan",
            source);
        Assert.DoesNotContain(
            "Game1.player.Items[discardedSlot] =",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.player.Items.Remove",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.activeClickableMenu = null",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DebugFixtureUsesTheVanillaFishingRewardMenuShape()
    {
        var fixture = RuntimeHarnessSources.File(
            "ModEntry.FishingItemGrabRewardFixture.cs");
        var supportedOptions = RuntimeHarnessSources.File(
            "ModEntry.SupportedOptions.cs");
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");

        Assert.Contains(
            "\"debug.setup_fishing_item_grab_reward\"",
            supportedOptions);
        Assert.Contains(
            "pending.Request.OptionId == \"debug.setup_fishing_item_grab_reward\"",
            dispatch);
        Assert.Contains(
            "ExecuteSetupFishingItemGrabReward(pending.Request)",
            dispatch);
        Assert.Contains("new FishingRod(4)", fixture);
        Assert.Contains("new ItemGrabMenu(", fixture);
        Assert.Contains("rod).setEssential(essential: true)", fixture);
        Assert.Contains("menu.source = source", fixture);
        Assert.Contains("FishingItemGrabPendingCount(menu) == 1", fixture);
    }

    [Fact]
    public void NativeSmokeCoversBothCapacityBranchesWithoutAutoAdmittingEvidence()
    {
        var smoke = RuntimeHarnessSources.RepositoryFile(
            "scripts",
            "Invoke-RuntimeFishingItemGrabRecoverySmoke.ps1");

        Assert.Contains(
            "--daily-plan-candidate-options \"recovery.stabilize_day\"",
            smoke);
        Assert.Contains("/api/v1/performance", smoke);
        Assert.Contains("profile=fishing_reward_recovery&fresh=1", smoke);
        Assert.Contains("observed_updates_per_second", smoke);
        Assert.Contains("recovery_snapshot_last_build_ms", smoke);
        Assert.Contains("--skip-training", smoke);
        Assert.Contains("Id = \"capacity_available\"", smoke);
        Assert.Contains(
            "Id = \"inventory_full_authorized_discard\"",
            smoke);
        Assert.Contains(
            "STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"",
            smoke);
        Assert.Contains("-WindowStyle Hidden", smoke);
        Assert.Contains(
            "evidence_admission = \"not_automatic\"",
            smoke);
    }

    [Fact]
    public void BridgeAndTransportExposeEveryRuntimeBinding()
    {
        var bridge = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "MenuReadAdapter.cs");
        var bridgeEntry = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs");
        var transport = LiveTrainingLoopSources.All;

        Assert.Contains("kind = \"fishing_item_grab\"", bridge);
        Assert.Contains("pending_items = pendingItems", bridge);
        Assert.Contains(
            "player_can_accept_whole_stack",
            bridge);
        Assert.Contains(
            "or \"recovery\" or \"fishing_reward_recovery\" or \"route\"",
            bridgeEntry);
        Assert.Contains("profileSnapshots.Clear()", bridgeEntry);
        Assert.Contains("InvalidateSnapshotCaches();", bridgeEntry);
        Assert.Contains(
            "ReadQueueParameterInt(item, \"fishing_reward_menu_slot_index\")",
            transport);
        Assert.Contains(
            "executionRequest.FishingRewardDiscardAuthorization =",
            transport);
    }
}
