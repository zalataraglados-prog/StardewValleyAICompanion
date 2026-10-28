using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FishingRewardDiscardAuthorization =
        "objective_safe_unprotected_lowest_total_store_value";

    private TrainingExecutionResult ExecuteClaimFishingItemGrabReward(
        TrainingExecutionRequest request)
    {
        var requested = FishingItemGrabRewardRequestedEffect(request);
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(
                request,
                "claim_fishing_item_grab_reward",
                requested,
                FishingItemGrabRewardObservedEffect(),
                reasons.ToArray());
        }

        if (Game1.activeClickableMenu is not ItemGrabMenu menu ||
            menu.context is not FishingRod ||
            !menu.essential)
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_item_grab_menu_identity_drifted");
        }

        if (!FishingItemGrabRewardRequestIsComplete(request) ||
            menu.source != request.FishingRewardMenuSource ||
            !string.Equals(
                menu.context.GetType().FullName,
                request.FishingRewardMenuContextType,
                StringComparison.Ordinal) ||
            menu.heldItem is not null)
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_item_grab_menu_binding_drifted");
        }

        var pendingBefore = FishingItemGrabPendingCount(menu);
        if (pendingBefore != request.FishingRewardPendingCountBefore)
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_reward_pending_count_drifted");
        }

        var rewardSlot = request.FishingRewardMenuSlotIndex!.Value;
        var reward = ItemAt(menu.ItemsToGrabMenu.actualInventory, rewardSlot);
        if (!FishingRewardMatchesRequest(reward, request))
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_reward_target_binding_drifted");
        }

        var canAcceptBefore =
            CanAcceptFishingRewardWithoutSideEffects(reward!);
        if (request.FishingRewardRequiresDiscard == canAcceptBefore)
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_reward_discard_requirement_drifted");
        }

        var rewardPosition = InventorySlotScreenPosition(
            menu.ItemsToGrabMenu,
            rewardSlot);
        if (!rewardPosition.HasValue)
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_reward_menu_slot_position_unavailable");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var rewardInventoryBefore = CountInventoryItems(
            reward!.QualifiedItemId);
        string? discardedItemId = null;
        var discardedStack = 0;
        var discardedSlot = -1;
        if (request.FishingRewardRequiresDiscard == true)
        {
            if (!TryDiscardAuthorizedFishingRewardInventoryStack(
                    menu,
                    request,
                    out discardedItemId,
                    out discardedStack,
                    out discardedSlot,
                    out var discardReason))
            {
                return BlockedFishingItemGrabReward(
                    request,
                    requested,
                    discardReason);
            }
        }

        if (!CanAcceptFishingRewardWithoutSideEffects(reward))
        {
            return BlockedFishingItemGrabReward(
                request,
                requested,
                "fishing_reward_capacity_not_recovered");
        }

        menu.receiveLeftClick(
            rewardPosition.Value.X,
            rewardPosition.Value.Y,
            playSound: false);

        var pendingAfter = FishingItemGrabPendingCount(menu);
        var rewardRemoved =
            ItemAt(menu.ItemsToGrabMenu.actualInventory, rewardSlot) is null;
        var heldItemCleared = menu.heldItem is null;
        var expectedPendingAfter = pendingBefore - 1;
        var remainedBound = ReferenceEquals(Game1.activeClickableMenu, menu);
        var menuClosed = false;
        if (rewardRemoved && heldItemCleared && pendingAfter == 0)
        {
            if (!menu.areAllItemsTaken() || !menu.readyToClose() ||
                menu.okButton is null)
            {
                return BlockedFishingItemGrabReward(
                    request,
                    requested,
                    "fishing_reward_empty_menu_not_ready_to_close");
            }

            var ok = menu.okButton.bounds.Center;
            menu.receiveLeftClick(ok.X, ok.Y, playSound: false);
            menuClosed = Game1.activeClickableMenu is null;
            remainedBound = false;
        }

        var verified = rewardRemoved &&
            heldItemCleared &&
            pendingAfter == expectedPendingAfter &&
            (pendingAfter == 0 ? menuClosed : remainedBound);
        var rewardInventoryAfter = CountInventoryItems(
            reward.QualifiedItemId);
        var verificationReasons = verified
            ? new[]
            {
                "exact_fishing_item_grab_menu_rebound",
                request.FishingRewardRequiresDiscard == true
                    ? "authorized_object_stack_trashed_by_native_menu_clicks"
                    : "preexisting_inventory_capacity_rechecked",
                "exact_reward_slot_claimed_by_native_menu_click",
                "single_reward_slice_requires_fresh_snapshot_replan"
            }
            : new[] { "fishing_reward_native_click_postcondition_failed" };

        var changedFacts = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "menus.fishing_item_grab.pending_item_count",
                Before = pendingBefore.ToString(),
                After = pendingAfter.ToString()
            },
            new()
            {
                Path = "player.inventory." +
                    reward.QualifiedItemId + ".count",
                Before = rewardInventoryBefore.ToString(),
                After = rewardInventoryAfter.ToString()
            }
        };
        if (discardedItemId is not null)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.inventory[" + discardedSlot + "]",
                Before = discardedItemId + "x" + discardedStack,
                After = "empty"
            });
        }

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "claim_fishing_item_grab_reward",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = requested,
            ObservedEffect = FishingItemGrabRewardObservedEffect(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : verificationReasons,
            ChangedFacts = changedFacts.ToArray()
        };
    }

    private static bool TryDiscardAuthorizedFishingRewardInventoryStack(
        ItemGrabMenu menu,
        TrainingExecutionRequest request,
        out string? discardedItemId,
        out int discardedStack,
        out int discardedSlot,
        out string failure)
    {
        discardedItemId = null;
        discardedStack = 0;
        discardedSlot = request.FishingRewardDiscardSlotIndex ?? -1;
        failure = string.Empty;

        if (!string.Equals(
                request.FishingRewardDiscardAuthorization,
                FishingRewardDiscardAuthorization,
                StringComparison.Ordinal) ||
            discardedSlot < 0 ||
            discardedSlot >= Game1.player.maxItems.Value ||
            discardedSlot >= Game1.player.Items.Count ||
            menu.inventory is null ||
            discardedSlot >= menu.inventory.inventory.Count)
        {
            failure = "fishing_reward_discard_authorization_or_slot_missing";
            return false;
        }

        var discard = Game1.player.Items[discardedSlot];
        if (discard is not StardewValley.Object ||
            !string.Equals(
                discard.QualifiedItemId,
                request.FishingRewardDiscardQualifiedItemId,
                StringComparison.Ordinal) ||
            discard.Stack != request.FishingRewardDiscardStackBefore ||
            discard.Quality != request.FishingRewardDiscardQuality)
        {
            failure = "fishing_reward_discard_item_binding_drifted";
            return false;
        }

        if (FishingRewardDiscardProtectionReasons(discard).Length > 0)
        {
            failure = "fishing_reward_discard_item_no_longer_objective_safe";
            return false;
        }

        discardedItemId = discard.QualifiedItemId;
        discardedStack = discard.Stack;
        var slot = menu.inventory.inventory[discardedSlot].bounds.Center;
        menu.receiveLeftClick(slot.X, slot.Y, playSound: false);
        if (!ReferenceEquals(menu.heldItem, discard))
        {
            failure = "fishing_reward_discard_native_inventory_pickup_failed";
            return false;
        }

        var trash = menu.trashCan.bounds.Center;
        menu.receiveLeftClick(trash.X, trash.Y, playSound: false);
        if (menu.heldItem is not null ||
            Game1.player.Items[discardedSlot] is not null)
        {
            failure = "fishing_reward_discard_native_trash_click_failed";
            return false;
        }

        return true;
    }

    private static string[] FishingRewardDiscardProtectionReasons(Item item)
    {
        var reasons = new List<string>();
        if (item.Category == StardewValley.Object.SeedsCategory ||
            Game1.cropData.ContainsKey(
                Crop.ResolveSeedId(item.ItemId, Game1.player.currentLocation)))
        {
            reasons.Add("plantable_seed");
        }
        if (item.specialItem)
            reasons.Add("special_item");
        if (!item.canBeTrashed())
            reasons.Add("cannot_be_trashed");
        if (item.sellToStorePrice(-1L) <= 0)
            reasons.Add("non_positive_sell_price");
        if (item is StardewValley.Object obj)
        {
            if (obj.questItem.Value)
                reasons.Add("quest_item");
            if (obj.Type == "Quest")
                reasons.Add("object_type_quest");
            if (obj.bigCraftable.Value)
                reasons.Add("big_craftable");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool CanAcceptFishingRewardWithoutSideEffects(Item item)
    {
        if (item.IsRecipe || item.QualifiedItemId is
            "(O)73" or "(O)930" or "(O)102" or "(O)326" or
            "(O)434" or "(O)858" or "(O)GoldCoin")
        {
            return true;
        }

        var maxItems = Math.Min(
            Game1.player.maxItems.Value,
            Game1.player.Items.Count);
        for (var slotIndex = 0; slotIndex < maxItems; slotIndex++)
        {
            var inventoryItem = Game1.player.Items[slotIndex];
            if (inventoryItem is null ||
                inventoryItem.canStackWith(item) &&
                inventoryItem.Stack + item.Stack <=
                    inventoryItem.maximumStackSize())
            {
                return true;
            }
        }

        return false;
    }

    private static int FishingItemGrabPendingCount(ItemGrabMenu menu) =>
        menu.ItemsToGrabMenu.actualInventory.Count(item => item is not null);

    private static bool FishingRewardMatchesRequest(
        Item? item,
        TrainingExecutionRequest request) =>
        item is not null &&
        string.Equals(
            item.QualifiedItemId,
            request.FishingRewardQualifiedItemId,
            StringComparison.Ordinal) &&
        item.Stack == request.FishingRewardStackBefore &&
        item.Quality == request.FishingRewardQuality;

    private static bool FishingItemGrabRewardRequestIsComplete(
        TrainingExecutionRequest request) =>
        request.FishingRewardMenuSource.HasValue &&
        !string.IsNullOrWhiteSpace(request.FishingRewardMenuContextType) &&
        request.FishingRewardMenuSlotIndex.HasValue &&
        !string.IsNullOrWhiteSpace(request.FishingRewardQualifiedItemId) &&
        request.FishingRewardStackBefore > 0 &&
        request.FishingRewardQuality.HasValue &&
        request.FishingRewardPendingCountBefore > 0 &&
        request.FishingRewardRequiresDiscard.HasValue &&
        (request.FishingRewardRequiresDiscard != true ||
         request.FishingRewardDiscardSlotIndex.HasValue &&
         !string.IsNullOrWhiteSpace(
             request.FishingRewardDiscardQualifiedItemId) &&
         request.FishingRewardDiscardStackBefore > 0 &&
         request.FishingRewardDiscardQuality.HasValue &&
         !string.IsNullOrWhiteSpace(
             request.FishingRewardDiscardAuthorization));

    private TrainingExecutionResult BlockedFishingItemGrabReward(
        TrainingExecutionRequest request,
        string requested,
        string reason) =>
        BlockedWithPrimitive(
            request,
            "claim_fishing_item_grab_reward",
            requested,
            FishingItemGrabRewardObservedEffect(),
            reason);

    private static string FishingItemGrabRewardRequestedEffect(
        TrainingExecutionRequest request) =>
        "menu_source=" +
        (request.FishingRewardMenuSource?.ToString() ?? "missing") +
        ";reward_slot=" +
        (request.FishingRewardMenuSlotIndex?.ToString() ?? "missing") +
        ";reward=" + request.FishingRewardQualifiedItemId + "x" +
        (request.FishingRewardStackBefore?.ToString() ?? "missing") +
        ";requires_discard=" +
        (request.FishingRewardRequiresDiscard?.ToString().ToLowerInvariant() ??
         "missing") +
        ";fresh_snapshot_replan_required=true";

    private static string FishingItemGrabRewardObservedEffect() =>
        "active_menu=" +
        (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";fishing_item_grab_pending=" +
        (Game1.activeClickableMenu is ItemGrabMenu menu &&
         menu.context is FishingRod
            ? FishingItemGrabPendingCount(menu).ToString()
            : "none") +
        ";held_item=" +
        (Game1.activeClickableMenu is ItemGrabMenu active
            ? active.heldItem?.QualifiedItemId ?? "none"
            : "none");
}
