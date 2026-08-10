using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartQuestDropBoxDonation(PendingExecution pending)
    {
        var request = pending.Request;
        var requestedEffect = QuestDropBoxRequestedEffect(request);
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                "request=invalid",
                genericReasons.ToArray()));
            return;
        }
        if (activeQuestDropBoxDonation is not null ||
            Game1.activeClickableMenu is not null ||
            Game1.dialogueUp ||
            Game1.player.UsingTool ||
            !Game1.player.CanMove)
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_player_or_menu_busy"));
            return;
        }
        if (request.QuestFamily != "special_order" ||
            request.QuestRuntimeType != "SpecialOrder" ||
            string.IsNullOrWhiteSpace(request.QuestCandidateId) ||
            string.IsNullOrWhiteSpace(request.QuestKey) ||
            string.IsNullOrWhiteSpace(request.QuestDropBoxId) ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            !request.QuestObjectiveIndex.HasValue ||
            !request.QuestExpectedCurrentCount.HasValue ||
            !request.QuestExpectedTargetCount.HasValue ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue ||
            !request.StandTileY.HasValue ||
            !request.QuestDropBoxSlotIndex.HasValue ||
            !request.QuestDropBoxExpectedStackBefore.HasValue ||
            !request.QuestDropBoxExpectedAcceptedCount.HasValue ||
            request.QuestDropBoxExpectedAcceptedCount.Value <= 0 ||
            string.IsNullOrWhiteSpace(request.QuestDropBoxQualifiedItemId))
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_typed_identity_required"));
            return;
        }

        var location = Game1.currentLocation;
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_location_mismatch"));
            return;
        }

        var actionTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var standTile = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (Game1.player.TilePoint != standTile ||
            Math.Abs(standTile.X - actionTile.X) + Math.Abs(standTile.Y - actionTile.Y) != 1)
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_player_not_on_adjacent_stand_tile"));
            return;
        }

        var action = location.doesTileHaveProperty(
            actionTile.X,
            actionTile.Y,
            "Action",
            "Buildings");
        if (!TryParseDropBoxAction(action, out var liveDropBoxId) ||
            !string.Equals(liveDropBoxId, request.QuestDropBoxId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_native_action_tile_drifted"));
            return;
        }

        var order = Game1.player.team.specialOrders.SingleOrDefault(value =>
            string.Equals(value.questKey.Value, request.QuestKey, StringComparison.Ordinal));
        if (order is null ||
            order.questState.Value != SpecialOrderStatus.InProgress ||
            request.QuestObjectiveIndex.Value < 0 ||
            request.QuestObjectiveIndex.Value >= order.objectives.Count ||
            order.objectives[request.QuestObjectiveIndex.Value] is not DonateObjective objective)
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(),
                "quest_drop_box_live_objective_not_found"));
            return;
        }
        if (!string.Equals(objective.dropBox.Value, request.QuestDropBoxId, StringComparison.Ordinal) ||
            !string.Equals(objective.GetDropboxLocationName(), request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            objective.GetCount() != request.QuestExpectedCurrentCount.Value ||
            objective.GetMaxCount() != request.QuestExpectedTargetCount.Value ||
            objective.confirmed.Value ||
            !order.UsesDropBox(request.QuestDropBoxId))
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(order, objective),
                "quest_drop_box_objective_projection_drifted"));
            return;
        }

        var slotIndex = request.QuestDropBoxSlotIndex.Value;
        var item = slotIndex >= 0 && slotIndex < Game1.player.Items.Count
            ? Game1.player.Items[slotIndex]
            : null;
        if (item is null ||
            !string.Equals(
                item.QualifiedItemId,
                request.QuestDropBoxQualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            item.Stack != request.QuestDropBoxExpectedStackBefore.Value ||
            !objective.IsValidItem(item) ||
            order.GetAcceptCount(item) != request.QuestDropBoxExpectedAcceptedCount.Value)
        {
            pending.Completion.SetResult(QuestDropBoxBlocked(
                request,
                requestedEffect,
                QuestDropBoxObservedEffect(order, objective),
                "quest_drop_box_inventory_or_native_capacity_drifted"));
            return;
        }

        activeQuestDropBoxDonation = new ActiveQuestDropBoxDonation(
            pending,
            location,
            order,
            objective,
            actionTile,
            standTile,
            slotIndex,
            item.QualifiedItemId,
            item.Stack,
            request.QuestDropBoxExpectedAcceptedCount.Value);
    }

    private void TickQuestDropBoxDonation()
    {
        var active = activeQuestDropBoxDonation;
        if (active is null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Location) ||
            active.ElapsedTicks > 1200)
        {
            CompleteQuestDropBoxBlocked(active, "quest_drop_box_world_location_or_timeout");
            return;
        }

        StopAllMovement();
        if (!active.OpenIssued)
        {
            var item = LiveDropBoxItem(active);
            if (Game1.player.TilePoint != active.StandTile ||
                item is null ||
                item.Stack != active.StackBefore ||
                active.Objective.GetCount() != active.ProgressBefore ||
                active.Order.GetAcceptCount(item) != active.ExpectedAcceptedCount ||
                Game1.activeClickableMenu is not null)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_preopen_projection_drifted");
                return;
            }

            Game1.player.faceDirection(DirectionTo(active.StandTile, active.ActionTile));
            var viewport = new TileRectangle(
                Game1.viewport.X,
                Game1.viewport.Y,
                Game1.viewport.Width,
                Game1.viewport.Height);
            var handled = active.Location.checkAction(
                new TileLocation(active.ActionTile.X, active.ActionTile.Y),
                viewport,
                Game1.player);
            if (!handled)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_checkAction_not_handled");
                return;
            }
            active.OpenIssued = true;
            return;
        }

        if (active.InventoryClickIssued && Game1.activeClickableMenu is null)
        {
            active.SettlementTicks++;
            if (QuestDropBoxPostconditionsSatisfied(active))
            {
                CompleteQuestDropBoxDonation(active);
            }
            else if (active.SettlementTicks > 240)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_settlement_timeout_or_mismatch");
            }
            return;
        }

        if (Game1.activeClickableMenu is not QuestContainerMenu menu)
        {
            active.OpenWaitTicks++;
            if (Game1.activeClickableMenu is not null || active.OpenWaitTicks > 180)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_menu_open_failed");
            }
            return;
        }
        if (!ReferenceEquals(menu.ItemsToGrabMenu.actualInventory, active.Order.donatedItems) ||
            !active.Order.donateMutex.IsLockHeld())
        {
            CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_menu_identity_mismatch");
            return;
        }

        if (!active.InventoryClickIssued)
        {
            var item = LiveDropBoxItem(active);
            if (item is null ||
                active.InventorySlotIndex < 0 ||
                active.InventorySlotIndex >= menu.inventory.inventory.Count ||
                menu.GetDonatableAmount(item) != active.ExpectedAcceptedCount)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_inventory_slot_drifted");
                return;
            }

            var component = menu.inventory.inventory[active.InventorySlotIndex];
            menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
            active.InventoryClickIssued = true;
            var stackAfter = DropBoxItemStack(active);
            if (stackAfter != active.StackBefore - active.ExpectedAcceptedCount ||
                active.Objective.GetCount() <= active.ProgressBefore)
            {
                CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_place_postcondition_mismatch");
            }
            return;
        }

        if (!active.CloseIssued)
        {
            if (!menu.readyToClose())
            {
                if (++active.SettlementTicks > 180)
                {
                    CompleteQuestDropBoxBlocked(active, "quest_drop_box_menu_not_ready_to_close");
                }
                return;
            }
            menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
            active.CloseIssued = true;
            return;
        }

        active.SettlementTicks++;
        if (Game1.activeClickableMenu is null && QuestDropBoxPostconditionsSatisfied(active))
        {
            CompleteQuestDropBoxDonation(active);
        }
        else if (active.SettlementTicks > 240)
        {
            CompleteQuestDropBoxBlocked(active, "quest_drop_box_native_settlement_timeout_or_mismatch");
        }
    }

    private static bool QuestDropBoxPostconditionsSatisfied(ActiveQuestDropBoxDonation active)
    {
        return DropBoxItemStack(active) == active.StackBefore - active.ExpectedAcceptedCount &&
            active.Objective.GetCount() > active.ProgressBefore &&
            (active.Objective.GetCount() < active.Objective.GetMaxCount() ||
             active.Objective.confirmed.Value);
    }

    private void CompleteQuestDropBoxDonation(ActiveQuestDropBoxDonation active)
    {
        activeQuestDropBoxDonation = null;
        var request = active.Pending.Request;
        var stackAfter = DropBoxItemStack(active);
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "quest_drop_box_donate",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "GameLocation.checkAction_DropBox_handled",
                "QuestContainerMenu.receiveLeftClick_inventory_completed",
                "QuestContainerMenu_ok_confirm_completed",
                "matching_DonateObjective_progress_increased"
            },
            RequestedEffect = QuestDropBoxRequestedEffect(request),
            ObservedEffect = QuestDropBoxObservedEffect(active.Order, active.Objective),
            EstimatedTicks = 240,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Location.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestKey = request.QuestKey,
            QuestObjectiveIndex = request.QuestObjectiveIndex,
            QuestProgressBefore = active.ProgressBefore,
            QuestProgressAfter = active.Objective.GetCount(),
            QuestTargetCount = active.Objective.GetMaxCount(),
            QuestPresentBefore = true,
            QuestPresentAfter = Game1.player.team.specialOrders.Contains(active.Order),
            QuestCompletedBefore = active.OrderStateBefore == SpecialOrderStatus.Complete,
            QuestCompletedAfter = active.Order.questState.Value == SpecialOrderStatus.Complete,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.inventory[" + active.InventorySlotIndex + "].stack",
                    Before = active.StackBefore.ToString(),
                    After = stackAfter.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "quests.special_orders[" + request.QuestKey + "].objectives[" +
                        request.QuestObjectiveIndex + "].current_count",
                    Before = active.ProgressBefore.ToString(),
                    After = active.Objective.GetCount().ToString()
                },
                new SimulatedFactChange
                {
                    Path = "quests.special_orders[" + request.QuestKey + "].objectives[" +
                        request.QuestObjectiveIndex + "].confirmed",
                    Before = "false",
                    After = active.Objective.confirmed.Value.ToString().ToLowerInvariant()
                }
            }
        });
    }

    private void CompleteQuestDropBoxBlocked(
        ActiveQuestDropBoxDonation active,
        string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is QuestContainerMenu)
        {
            Game1.exitActiveMenu();
        }
        activeQuestDropBoxDonation = null;
        active.Pending.Completion.SetResult(QuestDropBoxBlocked(
            active.Pending.Request,
            QuestDropBoxRequestedEffect(active.Pending.Request),
            QuestDropBoxObservedEffect(active.Order, active.Objective),
            reason));
    }

    private static TrainingExecutionResult QuestDropBoxBlocked(
        TrainingExecutionRequest request,
        string requestedEffect,
        string observedEffect,
        params string[] reasons)
    {
        var result = BlockedWithPrimitive(
            request,
            "quest_drop_box_donate",
            requestedEffect,
            observedEffect,
            reasons);
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        return result;
    }

    private static Item? LiveDropBoxItem(ActiveQuestDropBoxDonation active)
    {
        if (active.InventorySlotIndex < 0 ||
            active.InventorySlotIndex >= Game1.player.Items.Count)
        {
            return null;
        }
        var item = Game1.player.Items[active.InventorySlotIndex];
        return item is not null &&
            string.Equals(
                item.QualifiedItemId,
                active.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase)
            ? item
            : null;
    }

    private static int DropBoxItemStack(ActiveQuestDropBoxDonation active)
    {
        return LiveDropBoxItem(active)?.Stack ?? 0;
    }

    private static bool TryParseDropBoxAction(string? action, out string boxId)
    {
        var parts = action?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ??
            Array.Empty<string>();
        boxId = parts.Length > 1 ? parts[1] : string.Empty;
        return parts.Length > 0 &&
            string.Equals(parts[0], "DropBox", StringComparison.OrdinalIgnoreCase);
    }

    private static string QuestDropBoxRequestedEffect(TrainingExecutionRequest request)
    {
        return "quest_key=" + request.QuestKey +
            ";objective_index=" + request.QuestObjectiveIndex +
            ";drop_box=" + request.QuestDropBoxId +
            ";qualified_item_id=" + request.QuestDropBoxQualifiedItemId +
            ";accepted_count=" + request.QuestDropBoxExpectedAcceptedCount;
    }

    private static string QuestDropBoxObservedEffect(
        SpecialOrder? order = null,
        DonateObjective? objective = null)
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";quest_key=" + (order?.questKey.Value ?? "none") +
            ";order_state=" + (order?.questState.Value.ToString() ?? "unavailable") +
            ";objective_current=" + (objective?.GetCount().ToString() ?? "unavailable") +
            ";objective_target=" + (objective?.GetMaxCount().ToString() ?? "unavailable") +
            ";objective_confirmed=" +
            (objective?.confirmed.Value.ToString().ToLowerInvariant() ?? "unavailable");
    }
}
