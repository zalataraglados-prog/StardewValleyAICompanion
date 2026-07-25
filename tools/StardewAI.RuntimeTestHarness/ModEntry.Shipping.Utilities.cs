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
    private void CleanupAndBlock(ActiveShipInventoryToBin active, params string[] reasons)
    {
        ReleaseShipInputOverrides();
        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
        }
        activeShipInventoryToBin = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "ship_inventory_item_to_bin",
            ShipRequestedEffect(active.Pending.Request), ShipObservedEffect(), reasons));
    }

    private static bool TryResolveShippingActionTile(ShippingBin bin, Point standTile, out Point actionTile)
    {
        for (var x = bin.tileX.Value; x < bin.tileX.Value + bin.tilesWide.Value; x++)
        {
            var candidate = new Point(x, bin.tileY.Value);
            if (Math.Abs(candidate.X - standTile.X) + Math.Abs(candidate.Y - standTile.Y) == 1)
            {
                actionTile = candidate;
                return true;
            }
        }

        actionTile = default;
        return false;
    }

    private static bool TryOpenNativeShippingMenu(ShippingBin bin, out string reason)
    {
        reason = string.Empty;
        var shipItemMethod = AccessTools.Method(typeof(ShippingBin), "shipItem", new[] { typeof(Item), typeof(Farmer) });
        if (shipItemMethod is null)
        {
            reason = "native_shipping_callback_not_found";
            return false;
        }

        ItemGrabMenu.behaviorOnItemSelect shipItem;
        try
        {
            shipItem = (ItemGrabMenu.behaviorOnItemSelect)Delegate.CreateDelegate(
                typeof(ItemGrabMenu.behaviorOnItemSelect),
                bin,
                shipItemMethod);
        }
        catch (Exception ex)
        {
            reason = "native_shipping_callback_bind_failed:" + ex.GetType().Name;
            return false;
        }

        var menu = new ItemGrabMenu(
            inventory: null,
            reverseGrab: true,
            showReceivingMenu: false,
            Utility.highlightShippableObjects,
            shipItem,
            message: "",
            behaviorOnItemGrab: null,
            snapToBottom: true,
            canBeExitedWithKey: true,
            playRightClickSound: false,
            allowRightClick: true,
            showOrganizeButton: false,
            source: 0,
            sourceItem: null,
            whichSpecialButton: -1,
            context: bin);
        menu.initializeUpperRightCloseButton();
        menu.setBackgroundTransparency(b: false);
        menu.setDestroyItemOnClick(b: true);
        menu.initializeShippingBin();
        Game1.activeClickableMenu = menu;
        return true;
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

    private void ReleaseShipInputOverrides()
    {
        TryApplySmapiButtonOverride(SButton.X, pressed: false, out _);
        TryApplySmapiRightButtonOverride(pressed: false, out _);
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
        ReleaseShipInputOverrides();
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
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
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

        var trainingMode = string.Equals(request.ExecutionMode, "training_singleplayer", StringComparison.Ordinal);
        var companionMode = string.Equals(request.ExecutionMode, "coop_companion", StringComparison.Ordinal);
        var dedicatedHostMode = string.Equals(request.ExecutionMode, "dedicated_host_ai", StringComparison.Ordinal);
        if (!trainingMode && !companionMode && !dedicatedHostMode)
        {
            reasons.Add("unsupported_execution_mode");
        }

        if (trainingMode)
        {
            if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") != "1")
            {
                reasons.Add("training_mode_env_required");
            }

            if (Context.IsMultiplayer)
            {
                reasons.Add("training_singleplayer_world_required");
            }
        }

        if (companionMode)
        {
            if (Environment.GetEnvironmentVariable("STARDEWAI_COMPANION_MODE") != "1")
            {
                reasons.Add("companion_mode_env_required");
            }

            if (!Context.IsMultiplayer)
            {
                reasons.Add("coop_world_required");
            }
            else if (Context.IsMainPlayer)
            {
                reasons.Add("coop_farmhand_required");
            }

            if (string.IsNullOrWhiteSpace(config.CompanionActorId) ||
                !string.Equals(request.Actor, config.CompanionActorId, StringComparison.Ordinal))
            {
                reasons.Add("companion_actor_mismatch");
            }

            var currentFarmerId = Game1.player?.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(config.CompanionFarmerId))
            {
                reasons.Add("companion_farmer_id_required");
            }
            else if (!string.Equals(currentFarmerId, config.CompanionFarmerId, StringComparison.Ordinal))
            {
                reasons.Add("companion_farmer_id_mismatch");
            }

            if (!request.OptionId.StartsWith("executor.", StringComparison.Ordinal) &&
                !string.Equals(request.OptionId, "farm.maintain_crops", StringComparison.Ordinal))
            {
                reasons.Add("coop_debug_or_planning_option_forbidden");
            }
        }

        if (dedicatedHostMode)
        {
            if (Environment.GetEnvironmentVariable("STARDEWAI_DEDICATED_HOST_MODE") != "1")
            {
                reasons.Add("dedicated_host_mode_env_required");
            }

            if (!Context.IsMultiplayer)
            {
                reasons.Add("dedicated_host_multiplayer_world_required");
            }
            else if (!Context.IsMainPlayer)
            {
                reasons.Add("dedicated_host_main_player_required");
            }

            if (string.IsNullOrWhiteSpace(config.DedicatedHostActorId) ||
                !string.Equals(request.Actor, config.DedicatedHostActorId, StringComparison.Ordinal))
            {
                reasons.Add("dedicated_host_actor_mismatch");
            }

            var currentFarmerId = Game1.player?.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture)
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(config.DedicatedHostFarmerId))
            {
                reasons.Add("dedicated_host_farmer_id_required");
            }
            else if (!string.Equals(currentFarmerId, config.DedicatedHostFarmerId, StringComparison.Ordinal))
            {
                reasons.Add("dedicated_host_farmer_id_mismatch");
            }

            if (!request.OptionId.StartsWith("executor.", StringComparison.Ordinal) &&
                !string.Equals(request.OptionId, "farm.maintain_crops", StringComparison.Ordinal))
            {
                reasons.Add("dedicated_host_debug_or_planning_option_forbidden");
            }
        }

        var expectedRunIdVariable = dedicatedHostMode
            ? "STARDEWAI_DEDICATED_HOST_RUN_ID"
            : companionMode
                ? "STARDEWAI_COMPANION_RUN_ID"
                : "STARDEWAI_TRAINING_RUN_ID";
        var expectedRunId = Environment.GetEnvironmentVariable(expectedRunIdVariable) ?? string.Empty;
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
            request.OptionId != "executor.break_farm_resource_clump" &&
            request.OptionId != "executor.break_current_location_resource_clump" &&
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
            request.OptionId != "debug.setup_fish_pond_output" &&
            request.OptionId != "debug.setup_fish_pond_request" &&
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
            request.OptionId != "debug.setup_material_inventory_graph" &&
            request.OptionId != "debug.setup_crab_pot_target" &&
            request.OptionId != "debug.setup_animal_product_target" &&
            request.OptionId != "debug.setup_pan_ore_spot" &&
            request.OptionId != "debug.setup_machine_input_target" &&
            request.OptionId != "debug.setup_machine_placement_target" &&
            request.OptionId != "debug.setup_storage_placement_target" &&
            request.OptionId != "debug.setup_shipping_target" &&
            request.OptionId != "executor.select_safe_item_slot" &&
            request.OptionId != "executor.close_menu" &&
            request.OptionId != "executor.interact" &&
            request.OptionId != "executor.buy_shop_item" &&
            request.OptionId != "executor.sell_shop_item" &&
            request.OptionId != "executor.plant_seed" &&
            request.OptionId != "executor.harvest_crop" &&
            request.OptionId != "executor.harvest_giant_crop" &&
            request.OptionId != "executor.pickup_debris" &&
            request.OptionId != "executor.collect_spawned_object" &&
            request.OptionId != "executor.harvest_ginger" &&
            request.OptionId != "executor.harvest_bush" &&
            request.OptionId != "executor.collect_crab_pot" &&
            request.OptionId != "executor.collect_animal_product" &&
            request.OptionId != "executor.pet_interact" &&
            request.OptionId != "executor.fill_pet_bowl" &&
            request.OptionId != "executor.pan_ore_spot" &&
            request.OptionId != "executor.collect_fish_pond_output" &&
            request.OptionId != "executor.complete_fish_pond_request" &&
            request.OptionId != "executor.collect_machine_output" &&
            request.OptionId != "executor.load_machine_input" &&
            request.OptionId != "executor.place_machine" &&
            request.OptionId != "executor.place_storage" &&
            request.OptionId != "executor.catch_fish" &&
            request.OptionId != "executor.choose_dialogue_response" &&
            request.OptionId != "executor.social_interact" &&
            request.OptionId != "executor.quest_npc_interact" &&
            request.OptionId != "executor.quest_drop_box_donate" &&
            request.OptionId != "executor.sleep" &&
            request.OptionId != "executor.ship_inventory_item_to_bin")
        {
            reasons.Add("unsupported_option_id");
        }

        return reasons;
    }}
