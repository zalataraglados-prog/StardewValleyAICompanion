using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeShippingSourceGuardTests
{
    [Fact]
    public void SmokeScriptSelectsFarmWarpFromTransparentWarps()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("target_name -eq \"Farm\"", script, StringComparison.Ordinal);
        Assert.Contains("current_location.identity", script, StringComparison.Ordinal);
        Assert.Contains("current_location.warps", script, StringComparison.Ordinal);
        Assert.Contains("name_or_unique_name", script, StringComparison.Ordinal);
        Assert.Contains("multiple warps to Farm", script, StringComparison.Ordinal);
        Assert.Contains("no warp to Farm", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesProductionTraverseConnectorForPreflight()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("executor.traverse_connector", script, StringComparison.Ordinal);
        Assert.Contains("connector_kind = \"warp\"", script, StringComparison.Ordinal);
        Assert.Contains("expected_target_location = \"Farm\"", script, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_x = [int]$farmWarp.target_x", script, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_y = [int]$farmWarp.target_y", script, StringComparison.Ordinal);
        Assert.Contains("target_tile_x = [int]$farmWarp.x", script, StringComparison.Ordinal);
        Assert.Contains("queue_item_id = \"runtime-ship-inventory-smoke.preflight-warp\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesPostRouteFarmLocation()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("post-warp location is not Farm", script, StringComparison.Ordinal);
        Assert.Contains("expected Farm after warp", script, StringComparison.Ordinal);
        Assert.Contains("$afterLocationName -ne \"Farm\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesPostRouteStateHashForFixture()
    {
        var script = ShippingSmokeSource;
        Assert.Contains("$connectorSnapshot.state_hash", script, StringComparison.Ordinal);
        Assert.Contains("$connectorSnapshot = $initialSnapshot", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeHarnessHasNoDirectMovementCompatibilitySwitch()
    {
        var script = ShippingSmokeSource;
        var source = RuntimeHarnessSource;
        var config = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "HarnessConfig.cs"));

        Assert.DoesNotContain("STARDEWAI_USE_DIRECT_VALIDATED_MOVEMENT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("STARDEWAI_USE_DIRECT_VALIDATED_MOVEMENT", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UseDirectValidatedMovement", config, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.MovePosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position +=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorHasNativeActionAndMenuDispatchPhases()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("BinFace", source, StringComparison.Ordinal);
        Assert.Contains("BinPress", source, StringComparison.Ordinal);
        Assert.Contains("BinRelease", source, StringComparison.Ordinal);
        Assert.Contains("WaitForShippingMenu", source, StringComparison.Ordinal);
        Assert.Contains("SlotDispatch", source, StringComparison.Ordinal);
        Assert.Contains("WaitForSlotDispatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSeparatesActionEdgeFromMenuObservation()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("TryApplySmapiButtonOverride(SButton.X, pressed: true", source, StringComparison.Ordinal);
        Assert.Contains("Game1.currentLocation.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.X, pressed: false", source, StringComparison.Ordinal);
        Assert.Contains("Game1.activeClickableMenu is ItemGrabMenu binMenu && binMenu.shippingBin", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorDispatchesNativeRightClickAtInventoryComponentWithoutMovingCursor()
    {
        var source = RuntimeHarnessSource;
        Assert.Contains("InventorySlotScreenPosition(menu, active.SlotIndex)", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveRightClick(slotPos.Value.X, slotPos.Value.Y", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.setMousePosition(slotPos.Value.X", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorBinPhaseAdvancementRequiresFacingSet()
    {
        var source = RuntimeHarnessSource;
        var binOpenSlice = Slice(source, "private void TickShipBinOpenPhase", "private void TickShipSlotClickPhase");
        Assert.Contains("active.FacingSet && !active.ButtonPressed", binOpenSlice, StringComparison.Ordinal);
        Assert.Contains("ShipPhase.BinPress", binOpenSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorSlotPhaseWaitsForNativeInventoryAndBinDeltas()
    {
        var source = RuntimeHarnessSource;
        var slotClickSlice = Slice(source, "private void TickShipSlotClickPhase", "private void TickShipVerifyAndClose");
        Assert.Contains("case ShipPhase.WaitForSlotDispatch", slotClickSlice, StringComparison.Ordinal);
        Assert.Contains("slotStackOk && inventoryDecreased && binIncreased", slotClickSlice, StringComparison.Ordinal);
        Assert.Contains("active.Phase = ShipPhase.VerifyAndClose", slotClickSlice, StringComparison.Ordinal);
    }

    [Fact]
    public void ShipExecutorOnlyDispatchesSlotAfterShippingMenuObserved()
    {
        var source = RuntimeHarnessSource;
        var binOpenSlice = Slice(source, "private void TickShipBinOpenPhase", "private void TickShipSlotClickPhase");
        Assert.Contains("active.SawShippingMenu = true", binOpenSlice, StringComparison.Ordinal);
        Assert.Contains("active.Phase = ShipPhase.SlotDispatch", binOpenSlice, StringComparison.Ordinal);
    }

}
