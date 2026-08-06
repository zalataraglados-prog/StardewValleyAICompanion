using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartShipInventoryItemToBin(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), reasons.ToArray()));
            return;
        }

        var slotIndex = pending.Request.SlotIndex ?? -1;
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_index_invalid"));
            return;
        }

        var slotItem = Game1.player.Items[slotIndex];
        if (slotItem is null || slotItem.Stack <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_empty"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(pending.Request.QualifiedItemId) &&
            !string.Equals(slotItem.QualifiedItemId, pending.Request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "slot_item_id_mismatch"));
            return;
        }

        var quantity = pending.Request.Quantity ?? 1;
        if (quantity != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "quantity_one_required"));
            return;
        }

        if (slotItem.Stack < quantity)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "insufficient_stack"));
            return;
        }

        if (!slotItem.canBeShipped())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "item_not_shippable"));
            return;
        }

        if (!pending.Request.ExpectedUnitPrice.HasValue ||
            slotItem.sellToStorePrice(-1L) != pending.Request.ExpectedUnitPrice.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "shipping_unit_price_drift"));
            return;
        }

        if (string.IsNullOrWhiteSpace(pending.Request.RequestNonce))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "request_nonce_required"));
            return;
        }

        if (Game1.currentLocation is not Farm farm ||
            !string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "not_on_farm"));
            return;
        }

        ShippingBin? bin = null;
        if (pending.Request.TargetTileX.HasValue && pending.Request.TargetTileY.HasValue)
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b =>
                    b.daysOfConstructionLeft.Value <= 0 &&
                    pending.Request.TargetTileX.Value == b.tileX.Value &&
                    pending.Request.TargetTileY.Value == b.tileY.Value);
            if (bin is null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                    ShipRequestedEffect(pending.Request), ShipObservedEffect(), "no_completed_bin_at_target_tile"));
                return;
            }
        }
        else
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "target_tile_required"));
            return;
        }

        if (!pending.Request.StandTileX.HasValue || !pending.Request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "stand_tile_required"));
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile.X != pending.Request.StandTileX.Value || playerTile.Y != pending.Request.StandTileY.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(),
                "player_not_on_exact_stand_tile:expected=" + pending.Request.StandTileX.Value + "," + pending.Request.StandTileY.Value +
                ";actual=" + playerTile.X + "," + playerTile.Y));
            return;
        }

        // Match vanilla ShippingBin interaction range and the transparent bridge.
        // Farmer.Tile is already the player's tile-space position; adding another
        // half-tile incorrectly rejects valid stand tiles beside a two-tile-wide bin.
        var distance = Vector2.Distance(
            Game1.player.Tile,
            new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value));
        if (distance > 2.0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "player_out_of_shipping_range"));
            return;
        }

        if (!TryResolveShippingActionTile(bin, playerTile, out var actionTile))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "stand_tile_not_cardinal_to_shipping_bin"));
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "menu_already_open"));
            return;
        }

        var binInventory = farm.getShippingBin(Game1.player);
        var beforeInventoryCount = CountInventoryItems(slotItem.QualifiedItemId);
        var beforeBinCount = CountBinItems(binInventory, slotItem.QualifiedItemId);
        var binAggregate = ReadBinAggregate(binInventory);
        var beforeBinTotal = binAggregate.Sum(c => c.count);
        var beforeBinDistinct = binAggregate.Length;
        var beforeBinSignature = ComputeShippingBinSignature(binAggregate);
        var unqualifiedItemId = slotItem.ItemId ?? string.Empty;
        var beforeBasicShipped = GetBasicShippedCount(Game1.player, unqualifiedItemId);
        var beforeSlotQualifiedId = slotItem.QualifiedItemId ?? string.Empty;
        var beforeSlotStack = slotItem.Stack;
        var beforeSlotItemId = unqualifiedItemId;

        activeShipInventoryToBin = new ActiveShipInventoryToBin(
            pending, bin, actionTile, slotIndex, slotItem.QualifiedItemId ?? string.Empty, unqualifiedItemId, quantity,
            beforeInventoryCount, beforeBinCount, beforeBinTotal, beforeBinDistinct,
            beforeBinSignature, beforeBasicShipped, beforeSlotQualifiedId, beforeSlotStack,
            beforeSlotItemId);
    }

    private void TickShipInventoryToBin()
    {
        if (activeShipInventoryToBin is null) return;

        var active = activeShipInventoryToBin;
        active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            ReleaseShipInputOverrides();
            CleanupAndBlock(active,
                "ship_timeout",
                "ship_timeout_phase=" + active.Phase,
                "ship_timeout_state=" +
                "facing_set:" + active.FacingSet.ToString().ToLowerInvariant() +
                ",native_action_dispatched:" + active.NativeActionDispatched.ToString().ToLowerInvariant() +
                ",button_pressed:" + active.ButtonPressed.ToString().ToLowerInvariant() +
                ",button_released:" + active.ButtonReleased.ToString().ToLowerInvariant() +
                ",saw_shipping_menu:" + active.SawShippingMenu.ToString().ToLowerInvariant() +
                ",slot_click_dispatched:" + active.SlotClickDispatched.ToString().ToLowerInvariant());
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.BinFace:
            case ShipPhase.BinPress:
            case ShipPhase.BinRelease:
            case ShipPhase.WaitForShippingMenu:
                TickShipBinOpenPhase(active);
                break;
            case ShipPhase.SlotDispatch:
            case ShipPhase.WaitForSlotDispatch:
                TickShipSlotClickPhase(active);
                break;
            case ShipPhase.VerifyAndClose:
                TickShipVerifyAndClose(active);
                break;
        }
    }

    private void TickShipBinOpenPhase(ActiveShipInventoryToBin active)
    {
        switch (active.Phase)
        {
            case ShipPhase.BinFace:
                if (active.FacingSet && !active.ButtonPressed)
                    active.Phase = ShipPhase.BinPress;
                break;

            case ShipPhase.BinPress:
                if (active.ButtonPressed && !active.ButtonReleased)
                    active.Phase = ShipPhase.BinRelease;
                break;

            case ShipPhase.BinRelease:
                if (active.ButtonReleased)
                {
                    active.Phase = ShipPhase.WaitForShippingMenu;
                }
                break;

            case ShipPhase.WaitForShippingMenu:
                if (Game1.activeClickableMenu is ItemGrabMenu binMenu && binMenu.shippingBin)
                {
                    active.SawShippingMenu = true;
                    active.Phase = ShipPhase.SlotDispatch;
                }
                break;
        }
    }

    private void TickShipSlotClickPhase(ActiveShipInventoryToBin active)
    {
        var menu = Game1.activeClickableMenu as ItemGrabMenu;
        if (menu is null || !menu.shippingBin)
        {
            ReleaseShipInputOverrides();
            CleanupAndBlock(active, "shipping_menu_lost");
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.SlotDispatch:
                break;

            case ShipPhase.WaitForSlotDispatch:
                if (active.SlotClickDispatched)
                {
                    var slotItem = Game1.player.Items[active.SlotIndex];
                    var slotStackNow = slotItem?.Stack ?? 0;
                    var slotQualifiedIdNow = slotItem?.QualifiedItemId ?? string.Empty;
                    var afterInventoryCount = CountInventoryItems(active.QualifiedItemId);
                    var binInventory = (Game1.currentLocation as Farm)?.getShippingBin(Game1.player);
                    var afterBinCount = CountBinItems(binInventory, active.QualifiedItemId);

                    var slotIdentityOk = string.Equals(slotQualifiedIdNow, active.BeforeSlotQualifiedId, StringComparison.OrdinalIgnoreCase);
                    var slotStackOk = false;
                    if (active.BeforeSlotStack > 1)
                        slotStackOk = slotIdentityOk && slotStackNow == active.BeforeSlotStack - active.Quantity;
                    else
                        slotStackOk = slotStackNow == 0 && (slotItem is null || string.IsNullOrEmpty(slotQualifiedIdNow));

                    var inventoryDecreased = afterInventoryCount == active.InventoryCountBefore - active.Quantity;
                    var binIncreased = afterBinCount == active.BinCountBefore + active.Quantity;
                    var verified = slotStackOk && inventoryDecreased && binIncreased;

                    if (!slotStackOk)
                    {
                        ReleaseShipInputOverrides();
                        CleanupAndBlock(active, "slot_stack_delta_mismatch",
                            "expected_stack=" + (active.BeforeSlotStack - active.Quantity) + ";actual=" + slotStackNow,
                            "before_stack=" + active.BeforeSlotStack + ";slot_null=" + (slotItem is null).ToString().ToLowerInvariant());
                        return;
                    }
                    if (afterInventoryCount != active.InventoryCountBefore && !inventoryDecreased)
                    {
                        ReleaseShipInputOverrides();
                        CleanupAndBlock(active, "ambiguous_quantity_delta",
                            "inventory_delta=" + (afterInventoryCount - active.InventoryCountBefore),
                            "bin_delta=" + (afterBinCount - active.BinCountBefore));
                        return;
                    }
                    if (verified)
                    {
                        active.Phase = ShipPhase.VerifyAndClose;
                        active.AfterSlotStack = slotStackNow;
                        active.AfterSlotQualifiedId = slotQualifiedIdNow;
                        active.SawShipDispatch = true;
                    }
                }
                break;
        }
    }

    private void TickShipVerifyAndClose(ActiveShipInventoryToBin active)
    {
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
            return;
        }

        var fItem = Game1.player.Items[active.SlotIndex];
        var fSlotQualifiedId = fItem?.QualifiedItemId ?? string.Empty;
        var fSlotStack = fItem?.Stack ?? 0;
        var fInventoryCount = CountInventoryItems(active.QualifiedItemId);
        var fBinInventory = (Game1.currentLocation as Farm)?.getShippingBin(Game1.player);
        var fBinCount = CountBinItems(fBinInventory, active.QualifiedItemId);
        var fBinAggregate = ReadBinAggregate(fBinInventory);
        var fBinTotal = fBinAggregate.Sum(c => c.count);
        var fBinDistinct = fBinAggregate.Length;
        var fBinSignature = ComputeShippingBinSignature(fBinAggregate);

        var fSlotStackOk = false;
        if (active.BeforeSlotStack > 1)
            fSlotStackOk = string.Equals(fSlotQualifiedId, active.BeforeSlotQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                fSlotStack == active.BeforeSlotStack - active.Quantity;
        else
            fSlotStackOk = fSlotStack == 0 && (fItem is null || string.IsNullOrEmpty(fSlotQualifiedId));

        var fInventoryDecreased = fInventoryCount == active.InventoryCountBefore - active.Quantity;
        var fBinIncreased = fBinCount == active.BinCountBefore + active.Quantity;
        var fVerified = fSlotStackOk && fInventoryDecreased && fBinIncreased;

        var receiptPath = string.Empty;
        if (fVerified)
        {
            receiptPath = WriteShipPendingReceipt(active, fInventoryCount, fBinCount,
                fBinTotal, fBinDistinct, fBinSignature, fSlotStack, fSlotQualifiedId);
            if (string.IsNullOrWhiteSpace(receiptPath))
            {
                ReleaseShipInputOverrides();
                CleanupAndBlock(active, "receipt_write_failed");
                return;
            }
        }

        CompleteShip(active, fVerified, fInventoryCount, fBinCount,
            fBinTotal, fBinDistinct, fBinSignature, fSlotStack,
            fSlotQualifiedId, receiptPath);
    }

    private void ApplyShipPhaseInput(ActiveShipInventoryToBin active)
    {
        switch (active.Phase)
        {
            case ShipPhase.BinFace:
                Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.ActionTile));
                active.FacingSet = true;
                break;

            case ShipPhase.BinPress:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiButtonOverride(SButton.X, pressed: true, out var reason))
                    {
                        ReleaseShipInputOverrides();
                        CleanupAndBlock(active, "bin_press_failed:" + reason);
                        return;
                    }

                    var handled = Game1.currentLocation.checkAction(
                        new TileLocation(active.ActionTile.X, active.ActionTile.Y),
                        Game1.viewport,
                        Game1.player);
                    if (!handled && Game1.activeClickableMenu is not ItemGrabMenu { shippingBin: true })
                    {
                        if (!TryOpenNativeShippingMenu(active.Bin, out var nativeMenuReason))
                        {
                            ReleaseShipInputOverrides();
                            CleanupAndBlock(active, "native_shipping_action_not_handled", nativeMenuReason);
                            return;
                        }
                    }

                    active.NativeActionDispatched = true;
                    active.ButtonPressed = true;
                }
                break;

            case ShipPhase.BinRelease:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiButtonOverride(SButton.X, pressed: false, out var releaseReason))
                    {
                        active.ReleaseRetries++;
                        if (active.ReleaseRetries > 3)
                        {
                            CleanupAndBlock(active, "bin_release_failed_after_retries:" + releaseReason);
                            return;
                        }
                        return;
                    }
                    active.ButtonReleased = true;
                }
                break;

            case ShipPhase.SlotDispatch:
                if (!active.SlotClickDispatched && Game1.activeClickableMenu is ItemGrabMenu menu && menu.shippingBin)
                {
                    var slotPos = InventorySlotScreenPosition(menu, active.SlotIndex);
                    if (!slotPos.HasValue)
                    {
                        ReleaseShipInputOverrides();
                        CleanupAndBlock(active, "slot_screen_position_unavailable");
                        return;
                    }
                    menu.receiveRightClick(slotPos.Value.X, slotPos.Value.Y, playSound: true);
                    active.SlotClickDispatched = true;
                    active.Phase = ShipPhase.WaitForSlotDispatch;
                }
                break;
        }
    }

}
