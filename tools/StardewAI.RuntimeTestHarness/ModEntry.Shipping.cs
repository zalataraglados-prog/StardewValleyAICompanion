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
    private void OnDayStartedForShippingReceipts(object? sender, StardewModdingAPI.Events.DayStartedEventArgs e)
    {
        try
        {
            TrySettleActiveRunPendingShippingReceipts();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shipping receipt reconciliation error: {ex.Message}", LogLevel.Warn);
        }
    }

    private void ReconcileShippingReceipts()
    {
        try
        {
            var receiptsDir = ResolveReceiptDirectory();
            if (!Directory.Exists(receiptsDir)) return;

            var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
            var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
            foreach (var receiptPath in receiptFiles)
            {
                try
                {
                    var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                    var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                    if (receipt is null) continue;

                    var isTerminal = receipt.Status == "completed" || receipt.Status == "failed" || receipt.Status == "ambiguous" || receipt.Status == "timed_out";

                    if (receipt.Status == "pending")
                    {
                        if (string.IsNullOrWhiteSpace(activeRunId) ||
                            !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                            continue;

                        if (receipt.ExpiresAt != null && DateTimeOffset.TryParse(receipt.ExpiresAt, out var expires) && DateTimeOffset.UtcNow > expires)
                        {
                            receipt.Status = "timed_out";
                            receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                            receipt.SettlementReason = "receipt_expired";
                            if (!receipt.FeedbackAppended)
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                                if (AppendDelayedFeedback(receipt))
                                {
                                    receipt.FeedbackAppended = true;
                                }
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                            else
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                        }
                    }
                    else if (isTerminal && !receipt.FeedbackAppended)
                    {
                        if (AppendDelayedFeedback(receipt))
                        {
                            receipt.FeedbackAppended = true;
                        }
                        AtomicWriteReceipt(receiptPath, receipt);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void AtomicWriteReceipt(string receiptPath, ShippingReceipt receipt)
    {
        var tempPath = receiptPath + ".tmp";
        var json = JsonSerializer.Serialize(receipt, JsonOptions);
        File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fs.Length == 0) throw new InvalidOperationException("temp file empty after flush");
        }
        File.Move(tempPath, receiptPath, overwrite: true);
    }

    private static string ResolveReceiptDirectory()
    {
        var trainingDir = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(trainingDir))
        {
            return Path.Combine(trainingDir, "pending_receipts");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") == "1")
        {
            throw new InvalidOperationException("STARDEWAI_TRAINING_OUTPUT_DIR is required when STARDEWAI_TRAINING_MODE=1");
        }

        var dir = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "training_output");
        return Path.Combine(dir, "pending_receipts");
    }

    private bool AppendDelayedFeedback(ShippingReceipt receipt)
    {
        try
        {
            if (receipt.FeedbackAppended) return true;

            var dir = ResolveReceiptDirectory();
            var feedbackPath = Path.Combine(dir, "delayed_shipping_feedback.jsonl");

            if (File.Exists(feedbackPath))
            {
                var existingLines = File.ReadAllLines(feedbackPath, System.Text.Encoding.UTF8);
                foreach (var existingLine in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(existingLine)) continue;
                    try
                    {
                        var existingRow = JsonSerializer.Deserialize<JsonElement>(existingLine, JsonOptions);
                        if (existingRow.TryGetProperty("receipt_id", out var existingId) &&
                            string.Equals(existingId.GetString(), receipt.ReceiptId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }

            var row = new
            {
                receipt_id = receipt.ReceiptId,
                run_id = receipt.RunId,
                queue_id = receipt.QueueId,
                queue_item_id = receipt.QueueItemId,
                request_nonce = receipt.RequestNonce,
                unqualified_item_id = receipt.UnqualifiedItemId,
                qualified_item_id = receipt.QualifiedItemId,
                quantity = receipt.Quantity,
                source_date = receipt.SourceDate,
                pre_basic_shipped_count = receipt.PreBasicShippedCount,
                settled_basic_shipped_count = receipt.SettledBasicShippedCount,
                settlement_status = receipt.Status,
                settlement_reason = receipt.SettlementReason,
                settled_at = receipt.SettledAt,
                settled_game_date = receipt.SettledGameDate
            };
            var line = JsonSerializer.Serialize(row, JsonOptions) + "\n";
            File.AppendAllText(feedbackPath, line, System.Text.Encoding.UTF8);

            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write delayed shipping feedback: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private void TrySettleActiveRunPendingShippingReceipts()
    {
        var receiptsDir = ResolveReceiptDirectory();
        if (!Directory.Exists(receiptsDir)) return;

        var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
        var errors = new List<Exception>();
        foreach (var receiptPath in receiptFiles)
        {
            try
            {
                var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                if (receipt is null || receipt.Status != "pending") continue;

                if (string.IsNullOrWhiteSpace(activeRunId) ||
                    !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                    continue;

                if (!int.TryParse(receipt.SourceDate, out var sourceDate)) continue;
                var currentGameDate = Game1.Date.TotalDays;
                if (currentGameDate <= sourceDate) continue;

                var currentCount = GetBasicShippedCount(Game1.player, receipt.UnqualifiedItemId);
                var expected = receipt.PreBasicShippedCount + receipt.Quantity;
                var newStatus = "ambiguous";
                var reason = string.Empty;

                if (currentCount == expected)
                {
                    newStatus = "completed";
                    reason = "basicShipped_incremented_by_expected_quantity";
                }
                else if (currentCount == receipt.PreBasicShippedCount)
                {
                    newStatus = "failed";
                    reason = "basicShipped_did_not_increment";
                }
                else
                {
                    newStatus = "ambiguous";
                    reason = "basicShipped_unexpected_delta:" + (currentCount - receipt.PreBasicShippedCount);
                }

                receipt.Status = newStatus;
                receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                receipt.SettlementReason = reason;
                receipt.SettledBasicShippedCount = currentCount;
                receipt.SettledGameDate = currentGameDate.ToString();
                receipt.SettledSeason = Game1.currentSeason;
                receipt.SettledDayOfMonth = Game1.dayOfMonth.ToString();
                receipt.SettledYear = Game1.year.ToString();

                AtomicWriteReceipt(receiptPath, receipt);

                if (AppendDelayedFeedback(receipt))
                {
                    receipt.FeedbackAppended = true;
                    AtomicWriteReceipt(receiptPath, receipt);
                }
                Monitor.Log($"Shipping receipt {receipt.ReceiptId} settled: {newStatus} ({reason}). basicShipped: {receipt.PreBasicShippedCount}->{currentCount}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to process shipping receipt {receiptPath}: {ex.Message}", LogLevel.Warn);
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("One or more shipping receipt settlements failed.", errors);
    }

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

        var distance = Vector2.Distance(
            new Vector2(Game1.player.TilePoint.X + 0.5f, Game1.player.TilePoint.Y + 0.5f),
            new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value + 0.5f));
        if (distance > 2.0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "ship_inventory_item_to_bin",
                ShipRequestedEffect(pending.Request), ShipObservedEffect(), "player_out_of_shipping_range"));
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
            pending, bin, slotIndex, slotItem.QualifiedItemId ?? string.Empty, unqualifiedItemId, quantity,
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
            ReleaseShipRightButton();
            CleanupAndBlock(active, "ship_timeout");
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.BinPosition:
            case ShipPhase.BinPositionVerify:
            case ShipPhase.BinPress:
            case ShipPhase.BinRelease:
            case ShipPhase.WaitForShippingMenu:
                TickShipBinOpenPhase(active);
                break;
            case ShipPhase.SlotPosition:
            case ShipPhase.SlotPositionVerify:
            case ShipPhase.SlotPress:
            case ShipPhase.SlotRelease:
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
            case ShipPhase.BinPosition:
                if (active.PositionSet && !active.PositionVerified)
                    active.Phase = ShipPhase.BinPositionVerify;
                break;

            case ShipPhase.BinPositionVerify:
                if (active.PositionVerified && !active.ButtonPressed)
                    active.Phase = ShipPhase.BinPress;
                break;

            case ShipPhase.BinPress:
                if (active.ButtonPressed && !active.ButtonReleased)
                    active.Phase = ShipPhase.BinRelease;
                break;

            case ShipPhase.BinRelease:
                if (active.ButtonReleased && Game1.activeClickableMenu is ItemGrabMenu menu && menu.shippingBin)
                {
                    active.SawShippingMenu = true;
                    active.ButtonPressed = false;
                    active.ButtonReleased = false;
                    active.PositionSet = false;
                    active.PositionVerified = false;
                    active.Phase = ShipPhase.SlotPosition;
                }
                break;

            case ShipPhase.WaitForShippingMenu:
                if (Game1.activeClickableMenu is ItemGrabMenu binMenu && binMenu.shippingBin)
                {
                    active.SawShippingMenu = true;
                    active.Phase = ShipPhase.SlotPosition;
                }
                break;
        }
    }

    private void TickShipSlotClickPhase(ActiveShipInventoryToBin active)
    {
        var menu = Game1.activeClickableMenu as ItemGrabMenu;
        if (menu is null || !menu.shippingBin)
        {
            ReleaseShipRightButton();
            CleanupAndBlock(active, "shipping_menu_lost");
            return;
        }

        switch (active.Phase)
        {
            case ShipPhase.SlotPosition:
                if (active.PositionSet && !active.PositionVerified)
                    active.Phase = ShipPhase.SlotPositionVerify;
                break;

            case ShipPhase.SlotPositionVerify:
                if (active.PositionVerified && !active.ButtonPressed)
                    active.Phase = ShipPhase.SlotPress;
                break;

            case ShipPhase.SlotPress:
                if (active.ButtonPressed && !active.ButtonReleased)
                    active.Phase = ShipPhase.SlotRelease;
                break;

            case ShipPhase.SlotRelease:
                if (active.ButtonReleased)
                {
                    active.SlotClickDispatched = true;
                    active.Phase = ShipPhase.WaitForSlotDispatch;
                }
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
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_stack_delta_mismatch",
                            "expected_stack=" + (active.BeforeSlotStack - active.Quantity) + ";actual=" + slotStackNow,
                            "before_stack=" + active.BeforeSlotStack + ";slot_null=" + (slotItem is null).ToString().ToLowerInvariant());
                        return;
                    }
                    if (afterInventoryCount != active.InventoryCountBefore && !inventoryDecreased)
                    {
                        ReleaseShipRightButton();
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
                ReleaseShipRightButton();
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
            case ShipPhase.BinPosition:
                if (!active.PositionSet)
                {
                    var pos = BinScreenPosition(active.Bin);
                    Game1.setMousePosition(pos.X, pos.Y, ui_scale: false);
                    active.PositionTarget = pos;
                    active.PositionSet = true;
                }
                break;

            case ShipPhase.BinPositionVerify:
                if (active.PositionSet && !active.PositionVerified)
                {
                    var actualX = Game1.getMouseX(ui_scale: false);
                    var actualY = Game1.getMouseY(ui_scale: false);
                    if (Math.Abs(actualX - active.PositionTarget.X) > 2 || Math.Abs(actualY - active.PositionTarget.Y) > 2)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active,
                            "cursor_position_mismatch:expected=" + active.PositionTarget.X + "," + active.PositionTarget.Y + ";actual=" + actualX + "," + actualY);
                        return;
                    }
                    active.PositionVerified = true;
                }
                break;

            case ShipPhase.BinPress:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "bin_press_failed:" + reason);
                        return;
                    }
                    active.ButtonPressed = true;
                }
                break;

            case ShipPhase.BinRelease:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: false, out var releaseReason))
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

            case ShipPhase.SlotPosition:
                if (!active.PositionSet && Game1.activeClickableMenu is ItemGrabMenu menu && menu.shippingBin)
                {
                    var slotPos = InventorySlotScreenPosition(menu, active.SlotIndex);
                    if (!slotPos.HasValue)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_screen_position_unavailable");
                        return;
                    }
                    Game1.setMousePosition(slotPos.Value.X, slotPos.Value.Y, ui_scale: true);
                    active.PositionTarget = slotPos.Value;
                    active.PositionSet = true;
                }
                break;

            case ShipPhase.SlotPositionVerify:
                if (active.PositionSet && !active.PositionVerified && Game1.activeClickableMenu is ItemGrabMenu vMenu && vMenu.shippingBin)
                {
                    var ax = Game1.getMouseX(ui_scale: true);
                    var ay = Game1.getMouseY(ui_scale: true);
                    if (Math.Abs(ax - active.PositionTarget.X) > 2 || Math.Abs(ay - active.PositionTarget.Y) > 2)
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active,
                            "slot_cursor_position_mismatch:expected=" + active.PositionTarget.X + "," + active.PositionTarget.Y + ";actual=" + ax + "," + ay);
                        return;
                    }
                    active.PositionVerified = true;
                }
                break;

            case ShipPhase.SlotPress:
                if (!active.ButtonPressed)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseShipRightButton();
                        CleanupAndBlock(active, "slot_press_failed:" + reason);
                        return;
                    }
                    active.ButtonPressed = true;
                }
                break;

            case ShipPhase.SlotRelease:
                if (!active.ButtonReleased)
                {
                    if (!TryApplySmapiRightButtonOverride(pressed: false, out var relReason))
                    {
                        active.ReleaseRetries++;
                        if (active.ReleaseRetries > 3)
                        {
                            CleanupAndBlock(active, "slot_release_failed_after_retries:" + relReason);
                            return;
                        }
                        return;
                    }
                    active.ButtonReleased = true;
                }
                break;
        }
    }

    private void CleanupAndBlock(ActiveShipInventoryToBin active, params string[] reasons)
    {
        ReleaseShipRightButton();
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
        }
        activeShipInventoryToBin = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "ship_inventory_item_to_bin",
            ShipRequestedEffect(active.Pending.Request), ShipObservedEffect(), reasons));
    }

    private static Point BinScreenPosition(ShippingBin bin)
    {
        var worldX = (bin.tileX.Value + 1) * 64;
        var worldY = bin.tileY.Value * 64 + 32;
        return new Point(worldX - Game1.viewport.X, worldY - Game1.viewport.Y);
    }

    private static Point? InventorySlotScreenPosition(ItemGrabMenu menu, int slotIndex)
    {
        if (menu.inventory is null || menu.inventory.inventory is null) return null;
        if (slotIndex < 0 || slotIndex >= menu.inventory.inventory.Count) return null;
        var component = menu.inventory.inventory[slotIndex];
        var bounds = component.bounds;
        return new Point(bounds.Center.X, bounds.Center.Y);
    }

    private static int CountBinItems(object? binInventory, string qualifiedItemId)
    {
        if (binInventory is null) return 0;
        System.Collections.IEnumerable enumerable;
        if (binInventory is System.Collections.IEnumerable binEnum)
            enumerable = binEnum;
        else
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (itemsProp is null) return 0;
            var items = itemsProp.GetValue(binInventory);
            if (items is not System.Collections.IEnumerable itemsEnum) return 0;
            enumerable = itemsEnum;
        }
        var count = 0;
        foreach (var obj in enumerable)
        {
            if (obj is Item item && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
                count += item.Stack;
        }
        return count;
    }

    private static (string itemId, string qualifiedItemId, int count)[] ReadBinAggregate(object? binInventory)
    {
        if (binInventory is null) return Array.Empty<(string, string, int)>();
        System.Collections.IEnumerable enumerable;
        if (binInventory is System.Collections.IEnumerable binEnum)
            enumerable = binEnum;
        else
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (itemsProp is null) return Array.Empty<(string, string, int)>();
            var items = itemsProp.GetValue(binInventory);
            if (items is not System.Collections.IEnumerable itemsEnum) return Array.Empty<(string, string, int)>();
            enumerable = itemsEnum;
        }
        var dict = new Dictionary<string, (string itemId, int count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in enumerable)
        {
            if (obj is Item item && item.Stack > 0)
            {
                var qid = item.QualifiedItemId ?? string.Empty;
                if (dict.TryGetValue(qid, out var existing))
                    dict[qid] = (existing.itemId, existing.count + item.Stack);
                else
                    dict[qid] = (item.ItemId ?? string.Empty, item.Stack);
            }
        }
        return dict
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ThenBy(kvp => kvp.Value.itemId, StringComparer.Ordinal)
            .Select(kvp => (kvp.Value.itemId, kvp.Key, kvp.Value.count))
            .ToArray();
    }

    private static string ComputeShippingBinSignature((string itemId, string qualifiedItemId, int count)[] aggregate)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var entry in aggregate)
        {
            sb.Append(entry.qualifiedItemId);
            sb.Append('|');
            sb.Append(entry.count);
            sb.Append('\n');
        }
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int GetBasicShippedCount(Farmer farmer, string unqualifiedItemId)
    {
        if (farmer.basicShipped.TryGetValue(unqualifiedItemId, out var count))
            return count;
        return 0;
    }

    private void ReleaseShipRightButton()
    {
        for (var i = 0; i < 3; i++)
        {
            if (TryApplySmapiRightButtonOverride(pressed: false, out _))
                return;
        }
    }

    private string WriteShipPendingReceipt(ActiveShipInventoryToBin active,
        int afterInventoryCount, int afterBinCount, int afterBinTotal, int afterBinDistinct,
        string afterBinSignature, int afterSlotStack, string afterSlotQualifiedId)
    {
        try
        {
            var receiptsDir = ResolveReceiptDirectory();
            Directory.CreateDirectory(receiptsDir);

            var safeRunId = SanitizeFileName(active.Pending.Request.RunId);
            var safeQueueItemId = SanitizeFileName(active.Pending.Request.QueueItemId);
            var safeNonce = SanitizeFileName(active.Pending.Request.RequestNonce);
            if (string.IsNullOrWhiteSpace(safeNonce) || safeNonce == "unknown")
            {
                Monitor.Log("Shipping receipt write blocked: request nonce is empty after sanitization", LogLevel.Error);
                return string.Empty;
            }
            var receiptFileName = "ship_" + safeRunId + "_" + safeQueueItemId + "_" + safeNonce + ".json";
            var receiptId = "ship_" + safeRunId + "_" + safeQueueItemId + "_" + safeNonce;
            var receiptPath = Path.Combine(receiptsDir, receiptFileName);
            var tempPath = receiptPath + ".tmp";

            var receipt = new ShippingReceipt
            {
                ReceiptId = receiptId,
                Status = "pending",
                RunId = active.Pending.Request.RunId,
                QueueId = active.Pending.Request.QueueId,
                QueueItemId = active.Pending.Request.QueueItemId,
                RequestNonce = active.Pending.Request.RequestNonce,
                UnqualifiedItemId = active.UnqualifiedItemId,
                QualifiedItemId = active.QualifiedItemId,
                Quantity = active.Quantity,
                SourceDate = Game1.Date.TotalDays.ToString(),
                SourceSeason = Game1.currentSeason,
                SourceDayOfMonth = Game1.dayOfMonth.ToString(),
                SourceYear = Game1.year.ToString(),
                PreBasicShippedCount = active.BasicShippedCountBefore,
                PreInventoryCount = active.InventoryCountBefore,
                PreBinCount = active.BinCountBefore,
                PreBinTotal = active.BinTotalCountBefore,
                PreBinDistinct = active.BinDistinctCountBefore,
                PreBinSignature = active.BinSignatureBefore,
                PreSlotStack = active.BeforeSlotStack,
                PreSlotQualifiedId = active.BeforeSlotQualifiedId,
                SlotIndex = active.SlotIndex,
                AfterInventoryCount = afterInventoryCount,
                AfterBinCount = afterBinCount,
                AfterBinTotal = afterBinTotal,
                AfterBinDistinct = afterBinDistinct,
                AfterBinSignature = afterBinSignature,
                AfterSlotStack = afterSlotStack,
                AfterSlotQualifiedId = afterSlotQualifiedId,
                CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToString("O")
            };

            var json = JsonSerializer.Serialize(receipt, JsonOptions);
            File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

            using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var bytes = new byte[fs.Length];
                fs.Read(bytes, 0, bytes.Length);
                if (bytes.Length == 0) throw new InvalidOperationException("temp file empty after flush");
            }

            File.Move(tempPath, receiptPath, overwrite: true);
            return receiptPath;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write shipping pending receipt: {ex.Message}", LogLevel.Error);
            return string.Empty;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Where(c => !invalid.Contains(c)).ToArray();
        return new string(chars).Replace(" ", "_");
    }

    private void CompleteShip(ActiveShipInventoryToBin active, bool verified,
        int afterInventoryCount, int afterBinCount,
        int afterBinTotal, int afterBinDistinct, string afterBinSignature,
        int afterSlotStack, string afterSlotQualifiedId,
        string pendingReceiptPath)
    {
        ReleaseShipRightButton();
        activeShipInventoryToBin = null;

        var startedAt = active.StartedAt;
        var completedAt = DateTimeOffset.UtcNow.ToString("O");
        var status = verified ? "applied" : "blocked";
        var verificationStatus = verified ? "verified" : "observed_mismatch";
        var verificationReasons = verified
            ? new[] { "inventory_count_decreased_by_one", "shipping_bin_count_increased_by_one", "slot_stack_decreased_by_one" }
            : new[] { "shipping_post_state_mismatch" };

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            PrimitiveKind = "ship_inventory_item_to_bin",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = ShipRequestedEffect(active.Pending.Request),
            ObservedEffect = ShipObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ShipSlotIndex = active.SlotIndex,
            ShipQualifiedItemId = active.QualifiedItemId,
            ShipItemId = active.UnqualifiedItemId,
            ShipInventoryCountBefore = active.InventoryCountBefore,
            ShipInventoryCountAfter = afterInventoryCount,
            ShipBinCountBefore = active.BinCountBefore,
            ShipBinCountAfter = afterBinCount,
            ShipBinTotalCountBefore = active.BinTotalCountBefore,
            ShipBinTotalCountAfter = afterBinTotal,
            ShipBinDistinctCountBefore = active.BinDistinctCountBefore,
            ShipBinDistinctCountAfter = afterBinDistinct,
            ShipBinSignatureBefore = active.BinSignatureBefore,
            ShipBinSignatureAfter = afterBinSignature,
            ShipBasicShippedCountBefore = active.BasicShippedCountBefore,
            ShipPendingReceiptPath = pendingReceiptPath,
            ShipBeforeSlotStack = active.BeforeSlotStack,
            ShipAfterSlotStack = afterSlotStack,
            ShipBeforeSlotQualifiedId = active.BeforeSlotQualifiedId,
            ShipAfterSlotQualifiedId = afterSlotQualifiedId,
            ShipSourceDate = Game1.Date.TotalDays.ToString(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.inventory." + active.QualifiedItemId + ".count", Before = active.InventoryCountBefore.ToString(), After = afterInventoryCount.ToString() },
                new SimulatedFactChange { Path = "player.inventory.slot." + active.SlotIndex + ".stack", Before = active.BeforeSlotStack.ToString(), After = afterSlotStack.ToString() },
                new SimulatedFactChange { Path = "player.inventory.slot." + active.SlotIndex + ".qualified_item_id", Before = active.BeforeSlotQualifiedId, After = afterSlotQualifiedId },
                new SimulatedFactChange { Path = "farm.shipping_bin." + active.QualifiedItemId + ".count", Before = active.BinCountBefore.ToString(), After = afterBinCount.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.total_count", Before = active.BinTotalCountBefore.ToString(), After = afterBinTotal.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.distinct_count", Before = active.BinDistinctCountBefore.ToString(), After = afterBinDistinct.ToString() },
                new SimulatedFactChange { Path = "farm.shipping_bin.contents_signature", Before = active.BinSignatureBefore, After = afterBinSignature },
                new SimulatedFactChange { Path = "ship.pending_receipt_path", Before = "", After = pendingReceiptPath }
            }
        });
    }

    private static string ShipRequestedEffect(TrainingExecutionRequest request)
    {
        return "executor_kind=ship_inventory_item_to_bin" +
            ";qualified_item_id=" + (string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "missing" : request.QualifiedItemId) +
            ";slot_index=" + (request.SlotIndex?.ToString() ?? "missing") +
            ";quantity=" + (request.Quantity?.ToString() ?? "1") +
            ";target_tile=" + (request.TargetTileX?.ToString() ?? "missing") + "," + (request.TargetTileY?.ToString() ?? "missing");
    }

    private static string ShipObservedEffect()
    {
        var menuOpen = Game1.activeClickableMenu is not null;
        var menuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var isShippingMenu = (Game1.activeClickableMenu as ItemGrabMenu)?.shippingBin ?? false;
        var playerTile = Game1.player?.TilePoint;
        return "menus.active_menu.is_open=" + menuOpen.ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + menuType +
            ";menus.active_menu.is_shipping=" + isShippingMenu.ToString().ToLowerInvariant() +
            ";player.tile=" + (playerTile?.X.ToString() ?? "none") + "," + (playerTile?.Y.ToString() ?? "none");
    }

    private bool TryApplySmapiRightButtonOverride(bool pressed, out string reason)
    {
        reason = string.Empty;
        var input = Game1.input;
        if (input is null)
        {
            reason = "smapi_input_null";
            return false;
        }

        var inputType = input.GetType();
        if (smapiInputStateType != inputType)
        {
            smapiInputStateType = inputType;
            smapiOverrideButtonMethod = inputType.GetMethod(
                "OverrideButton",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(SButton), typeof(bool) },
                modifiers: null);
        }

        if (smapiOverrideButtonMethod is null)
        {
            reason = "override_button_not_found";
            return false;
        }

        try
        {
            smapiOverrideButtonMethod.Invoke(input, new object[] { SButton.MouseRight, pressed });
            return true;
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException { InnerException: not null } ? ex.InnerException : ex;
            reason = "override_button_invoke_failed:" + cause.GetType().Name;
            return false;
        }
    }

    private List<string> ValidateExecutionRequest(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (request.SchemaVersion != "training_execution_request.v1")
        {
            reasons.Add("unsupported_schema_version");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") != "1")
        {
            reasons.Add("training_mode_env_required");
        }

        var expectedRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.RunId) || request.RunId != expectedRunId)
        {
            reasons.Add("run_id_mismatch");
        }

        var expectedSavePath = Path.GetFullPath(Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH") ?? config.SavesPath);
        var requestedSavePath = string.IsNullOrWhiteSpace(request.SaveIsolationPath)
            ? string.Empty
            : Path.GetFullPath(request.SaveIsolationPath);
        if (string.IsNullOrWhiteSpace(requestedSavePath) ||
            !string.Equals(requestedSavePath, expectedSavePath, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("save_isolation_path_mismatch");
        }

        if (!Context.IsWorldReady)
        {
            reasons.Add("world_not_ready");
        }

        if (request.OptionId != "farm.maintain_crops" &&
            request.OptionId != "debug.visible_walk" &&
            request.OptionId != "executor.move_to_tile" &&
            request.OptionId != "executor.traverse_connector" &&
            request.OptionId != "executor.face_direction" &&
            request.OptionId != "executor.wait_ticks" &&
            request.OptionId != "executor.clear_obstacle" &&
            request.OptionId != "executor.mine_stone" &&
            request.OptionId != "executor.break_resource_clump" &&
            request.OptionId != "executor.cool_volcano_lava" &&
            request.OptionId != "executor.break_volcano_stone" &&
            request.OptionId != "executor.break_volcano_container" &&
            request.OptionId != "executor.combat_volcano_monster" &&
            request.OptionId != "executor.break_container" &&
            request.OptionId != "executor.combat_monster" &&
            request.OptionId != "executor.shoot_monster" &&
            request.OptionId != "executor.place_bomb" &&
            request.OptionId != "executor.consume_food" &&
            request.OptionId != "executor.descend_ladder" &&
            request.OptionId != "executor.descend_shaft" &&
            request.OptionId != "executor.exit_mine" &&
            request.OptionId != "executor.till_soil" &&
            request.OptionId != "debug.advance_time_to" &&
            request.OptionId != "debug.setup_watering_target" &&
            request.OptionId != "debug.setup_till_soil_target" &&
            request.OptionId != "debug.setup_fish_frenzy" &&
            request.OptionId != "debug.setup_fish_pond" &&
            request.OptionId != "debug.setup_mine_fishing_floor" &&
            request.OptionId != "debug.setup_mining_floor" &&
            request.OptionId != "debug.setup_skull_cavern_shaft" &&
            request.OptionId != "debug.setup_quarry_mine" &&
            request.OptionId != "debug.setup_volcano_floor" &&
            request.OptionId != "debug.setup_breakable_container" &&
            request.OptionId != "debug.setup_mining_combat_fixture" &&
            request.OptionId != "debug.setup_clear_obstacle" &&
            request.OptionId != "debug.setup_plant_seed_target" &&
            request.OptionId != "debug.setup_harvest_crop_target" &&
            request.OptionId != "debug.setup_giant_crop_target" &&
            request.OptionId != "debug.setup_debris_target" &&
            request.OptionId != "debug.setup_machine_output_target" &&
            request.OptionId != "debug.setup_machine_input_target" &&
            request.OptionId != "debug.setup_shipping_target" &&
            request.OptionId != "executor.select_safe_item_slot" &&
            request.OptionId != "executor.close_menu" &&
            request.OptionId != "executor.interact" &&
            request.OptionId != "executor.buy_shop_item" &&
            request.OptionId != "executor.plant_seed" &&
            request.OptionId != "executor.harvest_crop" &&
            request.OptionId != "executor.harvest_giant_crop" &&
            request.OptionId != "executor.pickup_debris" &&
            request.OptionId != "executor.collect_machine_output" &&
            request.OptionId != "executor.load_machine_input" &&
            request.OptionId != "executor.catch_fish" &&
            request.OptionId != "executor.choose_dialogue_response" &&
            request.OptionId != "executor.social_interact" &&
            request.OptionId != "executor.sleep" &&
            request.OptionId != "executor.ship_inventory_item_to_bin")
        {
            reasons.Add("unsupported_option_id");
        }

        if (request.ExecutionMode != "training_singleplayer")
        {
            reasons.Add("unsupported_execution_mode");
        }

        return reasons;
    }
}
