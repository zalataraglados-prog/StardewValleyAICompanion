namespace StardewAI.Backend.Tests;

public sealed class RuntimeMaterialTransferExecutorTests
{
    [Fact]
    public void RuntimeUsesNativeChestMenuAndVerifiesEveryUnit()
    {
        var all = RuntimeHarnessSources.All;
        var source = RuntimeHarnessSources.File("ModEntry.MaterialTransfer.cs");

        Assert.Contains("StartMaterialTransfer(pending);", all, StringComparison.Ordinal);
        Assert.Contains("NativeRightClickEdgePatch.Arm();", source, StringComparison.Ordinal);
        Assert.Contains("active.Chest.checkForAction(Game1.player)", source, StringComparison.Ordinal);
        Assert.Contains("rightClickObserved = NativeRightClickEdgePatch.WasObserved", source, StringComparison.Ordinal);
        Assert.Contains("NativeRightClickEdgePatch.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("chest.giftbox.Value", source, StringComparison.Ordinal);
        Assert.Contains("material_transfer_live_open_context_drifted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryApplySmapiButtonOverride(SButton.X", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseOpenInput", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".ShowMenu()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RequestLock(", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveRightClick(", source, StringComparison.Ordinal);
        Assert.Contains("var oldKeyboard = Game1.oldKBState;", source, StringComparison.Ordinal);
        Assert.Contains("Game1.oldKBState = default;", source, StringComparison.Ordinal);
        Assert.Contains("Game1.oldKBState = oldKeyboard;", source, StringComparison.Ordinal);
        Assert.Contains("MaterialSourceReferenceIndex(source, active.SourceItem)", source, StringComparison.Ordinal);
        Assert.Contains("InventorySlotScreenPosition(sourceMenu, sourceSlotIndex)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InventorySlotScreenPosition(sourceMenu, intent.SourceSlotIndex)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MaterialSourceStackAt(", all, StringComparison.Ordinal);
        Assert.Contains("ReferenceEquals(items[index], original)", RuntimeHarnessSources.File("ModEntry.MaterialTransfer.SourceIdentity.cs"), StringComparison.Ordinal);
        Assert.Contains("MaterialTransferSourceSlotAfter", source, StringComparison.Ordinal);
        Assert.Contains("active.Chest.GetMutex().IsLockHeld()", source, StringComparison.Ordinal);
        Assert.Contains("MaterialChestActorUseAuthorized(chest)", source, StringComparison.Ordinal);
        Assert.Contains("MaterialChestActorUseAuthorized(active.Chest)", source, StringComparison.Ordinal);
        Assert.Contains("chest.owner.Value == Game1.player.UniqueMultiplayerID", source, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(chest.GlobalInventoryId)", source, StringComparison.Ordinal);
        Assert.Contains("material_transfer_chest_not_actor_authorized", source, StringComparison.Ordinal);
        Assert.Contains("Game1.exitActiveMenu()", source, StringComparison.Ordinal);
        Assert.Contains("MaterialTransferNativeLockReleased", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Items.Add(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Items.Remove(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("addItem(", source, StringComparison.Ordinal);
    }
}
