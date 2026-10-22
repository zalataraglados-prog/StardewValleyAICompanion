using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Internal;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeToolUpgradeNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Upgrade)->ShopMenu(ClintUpgrade).receiveLeftClick_exact_row->Tool.actionWhenPurchased->two_day_wait->Blacksmith_checkAction->native_tool_claim";

    private void StartToolUpgrade(PendingExecution pending)
    {
        var request = pending.Request;
        var requestReasons = ValidateExecutionRequest(request);
        if (requestReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, requestReasons.ToArray()));
            return;
        }
        var claim = request.OptionId == "executor.claim_tool_upgrade";
        if (!ToolUpgradeRequestIsTyped(request, claim))
        {
            pending.Completion.SetResult(ToolUpgradeBlocked(
                request,
                "tool_upgrade_complete_typed_request_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(ToolUpgradeBlocked(
                request,
                "tool_upgrade_player_menu_event_or_control_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var liveReasons = ValidateToolUpgradeLiveState(
            location,
            target,
            stand,
            request,
            claim);
        if (liveReasons.Length > 0)
        {
            pending.Completion.SetResult(ToolUpgradeBlocked(request, liveReasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            location,
            Game1.player.TilePoint,
            stand,
            maxMovementTiles,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(ToolUpgradeBlocked(
                request,
                "tool_upgrade_path_unavailable:" + pathReason));
            return;
        }
        activeToolUpgrade = new ActiveToolUpgrade(
            pending,
            location,
            target,
            stand,
            path,
            maxMovementTiles,
            claim);
    }

    private static bool ToolUpgradeRequestIsTyped(
        TrainingExecutionRequest request,
        bool claim)
    {
        var common = request.ToolUpgradeProjectionFingerprint.Length == 64 &&
            request.ToolUpgradeActionToken == "Blacksmith" &&
            request.NativeContract == RuntimeToolUpgradeNativeContract &&
            request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
            request.StandTileX.HasValue && request.StandTileY.HasValue &&
            request.LocationId == "Blacksmith";
        if (!common)
            return false;
        return claim
            ? request.ToolUpgradeLifecycleStage == "claim_ready_axe_upgrade" &&
                !string.IsNullOrWhiteSpace(request.ToolUpgradePendingQualifiedItemId) &&
                request.ToolUpgradePendingLevel is >= 0 &&
                request.ToolUpgradeDaysLeftBefore is <= 0 &&
                request.ToolUpgradeFreeSlotsBefore is > 0
            : request.ToolUpgradeLifecycleStage == "submit_next_axe_upgrade" &&
                request.ToolUpgradeSourceSlotIndex is >= 0 &&
                !string.IsNullOrWhiteSpace(request.ToolUpgradeSourceQualifiedItemId) &&
                !string.IsNullOrWhiteSpace(request.ToolUpgradeTargetQualifiedItemId) &&
                request.ToolUpgradeTargetLevel == request.ToolUpgradeSourceLevel + 1 &&
                request.ToolUpgradeMoneyBefore is >= 0 &&
                request.ToolUpgradePriceGold is > 0 &&
                !string.IsNullOrWhiteSpace(request.ToolUpgradeTradeQualifiedItemId) &&
                request.ToolUpgradeTradeAmount is > 0 &&
                request.ToolUpgradeTradeAvailableBefore >= request.ToolUpgradeTradeAmount &&
                request.ToolUpgradeExpectedDaysLeftAfter == 2;
    }

    private static string[] ValidateToolUpgradeLiveState(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        bool claim)
    {
        var reasons = new List<string>();
        var clint = location.characters.FirstOrDefault(npc => npc.Name == "Clint");
        if (location.NameOrUniqueName != "Blacksmith" ||
            request.LocationId != "Blacksmith" ||
            location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") !=
                request.ToolUpgradeActionRaw ||
            request.ToolUpgradeActionRaw
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() != "Blacksmith" ||
            !AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand))
        {
            reasons.Add("tool_upgrade_location_endpoint_or_geometry_drifted");
        }
        if (clint is null)
            reasons.Add("tool_upgrade_clint_not_at_blacksmith");

        var pending = Game1.player.toolBeingUpgraded.Value;
        if (claim)
        {
            if (pending is not Axe ||
                pending.QualifiedItemId != request.ToolUpgradePendingQualifiedItemId ||
                pending.ItemId != request.ToolUpgradePendingItemId ||
                pending.UpgradeLevel != request.ToolUpgradePendingLevel ||
                Game1.player.daysLeftForToolUpgrade.Value !=
                    request.ToolUpgradeDaysLeftBefore ||
                Game1.player.daysLeftForToolUpgrade.Value > 0 ||
                Game1.player.freeSpotsInInventory() !=
                    request.ToolUpgradeFreeSlotsBefore ||
                Game1.player.freeSpotsInInventory() <= 0)
            {
                reasons.Add("tool_upgrade_pending_claim_or_capacity_drifted");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (pending is not null || Game1.player.daysLeftForToolUpgrade.Value > 0)
            reasons.Add("tool_upgrade_pending_slot_not_clear");
        var slot = request.ToolUpgradeSourceSlotIndex!.Value;
        var source = slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as Axe
            : null;
        if (source is null ||
            source.QualifiedItemId != request.ToolUpgradeSourceQualifiedItemId ||
            source.ItemId != request.ToolUpgradeSourceItemId ||
            source.UpgradeLevel != request.ToolUpgradeSourceLevel)
        {
            reasons.Add("tool_upgrade_source_axe_slot_drifted");
        }
        if (Game1.player.Money != request.ToolUpgradeMoneyBefore ||
            Game1.player.Money < request.ToolUpgradePriceGold ||
            CountInventoryItems(request.ToolUpgradeTradeQualifiedItemId) !=
                request.ToolUpgradeTradeAvailableBefore ||
            request.ToolUpgradeTradeAvailableBefore < request.ToolUpgradeTradeAmount)
        {
            reasons.Add("tool_upgrade_money_or_trade_material_drifted");
        }
        if (!Game1.toolData.TryGetValue(
                request.ToolUpgradeTargetItemId,
                out var targetData) ||
            targetData.ConventionalUpgradeFrom != request.ToolUpgradeSourceItemId ||
            ItemRegistry.Create(
                request.ToolUpgradeTargetQualifiedItemId,
                allowNull: true) is not Axe targetAxe ||
            targetAxe.UpgradeLevel != request.ToolUpgradeTargetLevel ||
            ShopBuilder.GetToolUpgradeData(targetData, Game1.player) is not { } upgrade ||
            upgrade.Price != request.ToolUpgradePriceGold ||
            upgrade.TradeItemAmount != request.ToolUpgradeTradeAmount ||
            ItemRegistry.QualifyItemId(upgrade.TradeItemId) !=
                request.ToolUpgradeTradeQualifiedItemId)
        {
            reasons.Add("tool_upgrade_target_or_native_cost_contract_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickToolUpgrade()
    {
        var active = activeToolUpgrade;
        if (active is null)
            return;
        active.StageTicks++;
        if (active.CounterActionIssued)
            active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteToolUpgrade(active, false, "tool_upgrade_timeout");
            return;
        }
        var request = active.Pending.Request;
        if (!active.CounterActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(
                active,
                "tool_upgrade",
                out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteToolUpgrade(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving)
                return;
            var reasons = ValidateToolUpgradeLiveState(
                active.Location,
                active.Target,
                active.Stand,
                request,
                active.Claim);
            if (reasons.Length > 0)
            {
                CompleteToolUpgrade(active, false, reasons);
                return;
            }
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
            active.Location.checkAction(
                new xTile.Dimensions.Location(active.Target.X, active.Target.Y),
                Game1.viewport,
                Game1.player);
            active.CounterActionIssued = true;
            active.StageTicks = 0;
            return;
        }

        if (active.Claim)
        {
            AdvanceToolUpgradeReceiptSettlement(active);
            return;
        }
        if (!active.UpgradeAnswered)
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue ||
                !dialogue.isQuestion || active.Location.lastQuestionKey != "Blacksmith")
            {
                if (active.StageTicks > 180)
                    CompleteToolUpgrade(active, false, "tool_upgrade_blacksmith_question_timeout");
                return;
            }
            var upgrade = dialogue.responses?.FirstOrDefault(
                response => response.responseKey == "Upgrade");
            if (upgrade is null || !active.Location.answerDialogue(upgrade))
            {
                CompleteToolUpgrade(active, false, "tool_upgrade_native_upgrade_response_failed");
                return;
            }
            active.UpgradeAnswered = true;
            active.StageTicks = 0;
            return;
        }
        if (!active.PurchaseClicked)
        {
            if (Game1.activeClickableMenu is not ShopMenu menu)
            {
                if (active.StageTicks > 240)
                    CompleteToolUpgrade(active, false, "tool_upgrade_ClintUpgrade_menu_timeout");
                return;
            }
            if (!TryClickToolUpgradeRow(active, menu, out var clickFailure))
            {
                if (!string.IsNullOrWhiteSpace(clickFailure))
                    CompleteToolUpgrade(active, false, clickFailure);
                return;
            }
            active.PurchaseClicked = true;
            active.StageTicks = 0;
            return;
        }
        AdvanceToolUpgradeReceiptSettlement(active);
    }

    private static bool TryClickToolUpgradeRow(
        ActiveToolUpgrade active,
        ShopMenu menu,
        out string failure)
    {
        failure = string.Empty;
        var request = active.Pending.Request;
        if (menu.ShopId != "ClintUpgrade" || menu.readOnly || menu.currency != 0 ||
            menu.heldItem is not null)
        {
            failure = "tool_upgrade_native_shop_context_drifted";
            return false;
        }
        var index = menu.forSale.FindIndex(salable =>
            salable.QualifiedItemId == request.ToolUpgradeTargetQualifiedItemId &&
            salable is Axe axe && axe.ItemId == request.ToolUpgradeTargetItemId &&
            axe.UpgradeLevel == request.ToolUpgradeTargetLevel);
        if (index < 0)
        {
            failure = "tool_upgrade_exact_target_row_missing";
            return false;
        }
        var salable = menu.forSale[index];
        if (!menu.itemPriceAndStock.TryGetValue(salable, out var stock) ||
            stock.Price != request.ToolUpgradePriceGold ||
            ItemRegistry.QualifyItemId(stock.TradeItem) !=
                request.ToolUpgradeTradeQualifiedItemId ||
            stock.TradeItemCount != request.ToolUpgradeTradeAmount ||
            stock.ActionsOnPurchase is { Count: > 0 })
        {
            failure = "tool_upgrade_shop_price_trade_or_action_contract_drifted";
            return false;
        }
        if (menu.safetyTimer > 0)
            return false;
        if (index < menu.currentItemIndex)
        {
            menu.receiveLeftClick(
                menu.upArrow.bounds.Center.X,
                menu.upArrow.bounds.Center.Y,
                playSound: false);
            return false;
        }
        if (index >= menu.currentItemIndex + menu.forSaleButtons.Count)
        {
            menu.receiveLeftClick(
                menu.downArrow.bounds.Center.X,
                menu.downArrow.bounds.Center.Y,
                playSound: false);
            return false;
        }
        var button = menu.forSaleButtons[index - menu.currentItemIndex];
        menu.receiveLeftClick(
            button.bounds.Center.X,
            button.bounds.Center.Y,
            playSound: false);
        return true;
    }

    private void AdvanceToolUpgradeReceiptSettlement(ActiveToolUpgrade active)
    {
        if (!ToolUpgradeReceiptMatches(active))
        {
            if (active.StageTicks > 240)
                CompleteToolUpgrade(active, false, "tool_upgrade_native_receipt_mismatch");
            return;
        }
        active.ReceiptObserved = true;
        if (Game1.activeClickableMenu is DialogueBox dialogue)
        {
            if (active.StageTicks % 12 == 0)
            {
                dialogue.receiveLeftClick(
                    dialogue.xPositionOnScreen + dialogue.width / 2,
                    dialogue.yPositionOnScreen + dialogue.height / 2,
                    playSound: false);
            }
            return;
        }
        if (Game1.activeClickableMenu is ShopMenu)
        {
            CompleteToolUpgrade(active, false, "tool_upgrade_shop_remained_open_after_native_purchase");
            return;
        }
        if (Game1.activeClickableMenu is null && !Game1.dialogueUp &&
            Game1.player.CanMove)
        {
            CompleteToolUpgrade(active, true);
        }
    }

    private static bool ToolUpgradeReceiptMatches(ActiveToolUpgrade active)
    {
        var request = active.Pending.Request;
        if (active.Claim)
        {
            return Game1.player.toolBeingUpgraded.Value is null &&
                CountInventoryItems(request.ToolUpgradePendingQualifiedItemId) ==
                    active.TargetCountBefore + 1 &&
                Game1.player.Money == active.MoneyBefore;
        }
        var pending = Game1.player.toolBeingUpgraded.Value;
        return pending is Axe &&
            pending.QualifiedItemId == request.ToolUpgradeTargetQualifiedItemId &&
            pending.ItemId == request.ToolUpgradeTargetItemId &&
            pending.UpgradeLevel == request.ToolUpgradeTargetLevel &&
            Game1.player.daysLeftForToolUpgrade.Value ==
                request.ToolUpgradeExpectedDaysLeftAfter &&
            Game1.player.Money == active.MoneyBefore - request.ToolUpgradePriceGold &&
            CountInventoryItems(request.ToolUpgradeSourceQualifiedItemId) ==
                active.SourceCountBefore - 1 &&
            CountInventoryItems(request.ToolUpgradeTargetQualifiedItemId) ==
                active.TargetCountBefore &&
            CountInventoryItems(request.ToolUpgradeTradeQualifiedItemId) ==
                active.TradeCountBefore - request.ToolUpgradeTradeAmount;
    }

    private void CompleteToolUpgrade(
        ActiveToolUpgrade active,
        bool verified,
        params string[] reasons)
    {
        StopAllMovement();
        activeToolUpgrade = null;
        var request = active.Pending.Request;
        var verification = verified
            ? active.Claim
                ? new[]
                {
                    "shared_bfs_reached_exact_Blacksmith_counter",
                    "native_completed_tool_claim_intercept_completed",
                    "pending_tool_cleared_and_exact_upgraded_axe_returned"
                }
                : new[]
                {
                    "shared_bfs_reached_exact_Blacksmith_counter",
                    "native_ClintUpgrade_row_click_completed",
                    "source_axe_money_trade_bars_pending_target_and_two_day_timer_verified"
                }
            : reasons.Length == 0
                ? new[] { "tool_upgrade_post_state_mismatch" }
                : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration_and_strategy_weather_reserve_feedback",
            PrimitiveKind = active.Claim ? "claim_tool_upgrade" : "submit_tool_upgrade",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = ToolUpgradeRequestedEffect(request, active.Claim),
            ObservedEffect = ToolUpgradeObservedEffect(request),
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = ToolUpgradeChangedFacts(active)
        });
    }

    private static SimulatedFactChange[] ToolUpgradeChangedFacts(
        ActiveToolUpgrade active)
    {
        var request = active.Pending.Request;
        return active.Claim
            ?
            new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.tool_upgrade.pending_tool",
                    Before = request.ToolUpgradePendingQualifiedItemId,
                    After = string.Empty
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory." +
                        request.ToolUpgradePendingQualifiedItemId + ".count",
                    Before = active.TargetCountBefore.ToString(),
                    After = CountInventoryItems(
                        request.ToolUpgradePendingQualifiedItemId).ToString()
                }
            }
            :
            new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.money",
                    Before = active.MoneyBefore.ToString(),
                    After = Game1.player.Money.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory." +
                        request.ToolUpgradeSourceQualifiedItemId + ".count",
                    Before = active.SourceCountBefore.ToString(),
                    After = CountInventoryItems(
                        request.ToolUpgradeSourceQualifiedItemId).ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory." +
                        request.ToolUpgradeTradeQualifiedItemId + ".count",
                    Before = active.TradeCountBefore.ToString(),
                    After = CountInventoryItems(
                        request.ToolUpgradeTradeQualifiedItemId).ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.tool_upgrade.pending_tool",
                    Before = string.Empty,
                    After = Game1.player.toolBeingUpgraded.Value?.QualifiedItemId ?? string.Empty
                }
            };
    }

    private static string ToolUpgradeRequestedEffect(
        TrainingExecutionRequest request,
        bool claim) => claim
            ? "claim=" + request.ToolUpgradePendingQualifiedItemId +
                ";pending_after=none;inventory_delta=1"
            : "submit=" + request.ToolUpgradeTargetQualifiedItemId +
                ";money_delta=-" + request.ToolUpgradePriceGold +
                ";trade_delta=-" + request.ToolUpgradeTradeAmount +
                ";days_left_after=2";

    private static string ToolUpgradeObservedEffect(
        TrainingExecutionRequest request) =>
        "pending=" +
            (Game1.player.toolBeingUpgraded.Value?.QualifiedItemId ?? "none") +
        ";days_left=" + Game1.player.daysLeftForToolUpgrade.Value +
        ";money=" + Game1.player.Money +
        ";target_inventory=" + CountInventoryItems(
            string.IsNullOrWhiteSpace(request.ToolUpgradeTargetQualifiedItemId)
                ? request.ToolUpgradePendingQualifiedItemId
                : request.ToolUpgradeTargetQualifiedItemId);

    private static TrainingExecutionResult ToolUpgradeBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) => BlockedWithPrimitive(
            request,
            request.OptionId == "executor.claim_tool_upgrade"
                ? "claim_tool_upgrade"
                : "submit_tool_upgrade",
            ToolUpgradeRequestedEffect(
                request,
                request.OptionId == "executor.claim_tool_upgrade"),
            ToolUpgradeObservedEffect(request),
            reasons);
}
