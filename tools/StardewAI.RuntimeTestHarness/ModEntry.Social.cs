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
    private TrainingExecutionResult ExecuteSocialInteract(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", reasons.ToArray());
        }

        var npcName = request.SocialNpcName;
        var actionKind = request.SocialActionKind;
        if (string.IsNullOrWhiteSpace(npcName))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_name_required");
        }
        if (actionKind != "talk" && actionKind != "gift")
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_action_kind_talk_or_gift_required");
        }

        if (string.IsNullOrWhiteSpace(request.LocationId))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_location_id_required");
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_location_id_mismatch");
        }

        if (Game1.activeClickableMenu is not null)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_interact_menu_must_be_clear");
        }

        if (!request.SocialObservedNpcTileX.HasValue || !request.SocialObservedNpcTileY.HasValue)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_coordinates_required");
        }

        var npc = Game1.currentLocation.characters
            .FirstOrDefault(character => string.Equals(character.Name, npcName, StringComparison.Ordinal));
        if (npc is null)
        {
            return BuildSocialBlockedResult(request, false, null, "social_interact", "social_npc_not_in_current_location");
        }

        var npcTile = npc.TilePoint;
        if (npcTile.X != request.SocialObservedNpcTileX.Value ||
            npcTile.Y != request.SocialObservedNpcTileY.Value)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_moved_from_observed_tile");
        }

        if (!npc.IsVillager)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_not_ordinary_villager");
        }

        if (npc.IsMonster)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_is_monster");
        }

        if (npc.IsInvisible)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_invisible");
        }

        if (npc.isSleeping.Value)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_sleeping");
        }

        if (!npc.CanSocialize)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_cannot_socialize");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - npcTile.X) + Math.Abs(playerTile.Y - npcTile.Y) != 1)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_player_not_adjacent_to_npc");
        }

        var actionTargetRectangle = new XnaRectangle(npcTile.X * 64, npcTile.Y * 64, 64, 64);
        if (!npc.GetBoundingBox().Intersects(actionTargetRectangle))
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_not_intersecting_action_target_rectangle");
        }

        if (actionKind == "talk" && Game1.player.ActiveObject is not null)
        {
            return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_talk_active_object_must_be_cleared_first");
        }

        var beforeNpcLocation = Game1.currentLocation.NameOrUniqueName;
        var beforeNpcTile = npc.TilePoint;
        var beforeNpcVisible = !npc.IsInvisible;
        var beforeNpcSleeping = npc.isSleeping.Value;
        var beforeNpcOrdinary = npc.IsVillager && !npc.IsMonster;
        var beforePlayerTile = Game1.player.TilePoint;
        var beforeFacing = Game1.player.FacingDirection;
        var beforeSelectedSlot = Game1.player.CurrentToolIndex;
        var beforeMenuOpen = Game1.activeClickableMenu is not null;
        var beforeMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var beforeDialogueOpen = Game1.dialogueUp;
        var beforeDialogueSpeakerName = Game1.currentSpeaker?.Name ?? string.Empty;
        var beforeCurrentDialogue = npc.CurrentDialogue;
        var beforeDialogueCount = beforeCurrentDialogue?.Count ?? 0;
        var beforeDialogueKey = beforeCurrentDialogue is not null && beforeCurrentDialogue.Count > 0 ? beforeCurrentDialogue.Peek().TranslationKey : string.Empty;

        var beforeTalkedToToday = false;
        var beforeGiftsToday = 0;
        var beforeGiftsThisWeek = 0;
        var beforePoints = 0;
        var beforeFriendshipRowExists = false;
        if (Game1.player.friendshipData.TryGetValue(npcName, out var friendshipEntry))
        {
            beforeFriendshipRowExists = true;
            beforeTalkedToToday = friendshipEntry.TalkedToToday;
            beforeGiftsToday = friendshipEntry.GiftsToday;
            beforeGiftsThisWeek = friendshipEntry.GiftsThisWeek;
            beforePoints = friendshipEntry.Points;
        }

        int? beforeGiftStack = null;
        string beforeGiftItemId = string.Empty;
        int? beforeGiftQuality = null;
        int? beforeGiftSlot = null;

        if (actionKind == "gift")
        {
            var slotIndex = request.SocialGiftSlotIndex ?? -1;
            if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_slot_index_invalid");
            }

            var item = Game1.player.Items[slotIndex];
            if (item is null)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_slot_empty");
            }

            if (!string.IsNullOrWhiteSpace(request.SocialGiftQualifiedItemId) &&
                !string.Equals(item.QualifiedItemId, request.SocialGiftQualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_item_id_mismatch");
            }

            if (item.Stack <= 0)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_stack_empty");
            }

            if (!npc.CanReceiveGifts())
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_npc_cannot_receive_gifts");
            }

            var isStardropTea = string.Equals(item.QualifiedItemId, "(O)StardropTea", StringComparison.Ordinal);
            var isSpouse = string.Equals(Game1.player.spouse, npcName, StringComparison.Ordinal);
            var isBirthday = npc.isBirthday();
            var dailyExhausted = beforeGiftsToday >= 1;
            var weeklyExhausted = beforeGiftsThisWeek >= 2;
            if (dailyExhausted && !isStardropTea)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_daily_limit_exhausted");
            }
            if (weeklyExhausted && !isSpouse && !isBirthday && !isStardropTea)
            {
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_weekly_limit_exhausted");
            }

            beforeGiftStack = item.Stack;
            beforeGiftItemId = item.QualifiedItemId;
            beforeGiftQuality = (item as StardewValley.Object)?.Quality;
            beforeGiftSlot = slotIndex;
        }

        var startTicks = Game1.ticks;
        var startedAt = DateTimeOffset.UtcNow.ToString("O");

        if (actionKind == "gift" && beforeGiftSlot.HasValue)
        {
            Game1.player.CurrentToolIndex = beforeGiftSlot.Value;

            var item = Game1.player.Items[beforeGiftSlot.Value];
            if (Game1.player.ActiveObject is null ||
                Game1.player.ActiveObject.QualifiedItemId != item?.QualifiedItemId ||
                Game1.player.ActiveObject.Stack != (item?.Stack ?? 0))
            {
                Game1.player.CurrentToolIndex = beforeSelectedSlot;
                return BuildSocialBlockedResult(request, true, npc, "social_interact", "social_gift_active_object_not_selected");
            }
        }

        Game1.player.faceDirection(DirectionTo(beforePlayerTile, npcTile));

        var viewportRect = new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(npcTile.X, npcTile.Y),
            viewportRect,
            Game1.player);

        var endTicks = Game1.ticks;
        var actualTicks = Math.Max(0, endTicks - startTicks);
        var completedAt = DateTimeOffset.UtcNow.ToString("O");

        var afterFacing = Game1.player.FacingDirection;
        var afterSelectedSlot = Game1.player.CurrentToolIndex;
        var afterMenuOpen = Game1.activeClickableMenu is not null;
        var afterMenuType = Game1.activeClickableMenu?.GetType().Name ?? "none";
        var afterDialogueOpen = Game1.dialogueUp;
        var afterDialogueSpeakerName = Game1.currentSpeaker?.Name ?? string.Empty;

        var afterNpcMember = Game1.currentLocation.characters
            .FirstOrDefault(character => string.Equals(character.Name, npcName, StringComparison.Ordinal));
        var afterNpcPresent = afterNpcMember is not null;
        var afterNpcLocation = afterNpcPresent ? Game1.currentLocation.NameOrUniqueName : string.Empty;
        var afterNpcTile = afterNpcMember is not null ? afterNpcMember.TilePoint : (Point?)null;
        var afterNpcVisible = afterNpcMember is not null ? !afterNpcMember.IsInvisible : (bool?)null;
        var afterNpcSleeping = afterNpcMember is not null ? afterNpcMember.isSleeping.Value : (bool?)null;
        var afterNpcOrdinary = afterNpcMember is not null ? afterNpcMember.IsVillager && !afterNpcMember.IsMonster : (bool?)null;
        var afterPlayerTile = Game1.player.TilePoint;
        var afterCurrentDialogue = afterNpcMember?.CurrentDialogue;
        var afterDialogueCount = afterCurrentDialogue?.Count;
        var afterDialogueKey = afterCurrentDialogue is not null && afterCurrentDialogue.Count > 0
            ? afterCurrentDialogue.Peek().TranslationKey : string.Empty;

        var afterTalkedToToday = false;
        var afterGiftsToday = 0;
        var afterGiftsThisWeek = 0;
        var afterPoints = 0;
        var afterFriendshipRowExists = false;
        if (Game1.player.friendshipData.TryGetValue(npcName, out var afterFriendshipEntry))
        {
            afterFriendshipRowExists = true;
            afterTalkedToToday = afterFriendshipEntry.TalkedToToday;
            afterGiftsToday = afterFriendshipEntry.GiftsToday;
            afterGiftsThisWeek = afterFriendshipEntry.GiftsThisWeek;
            afterPoints = afterFriendshipEntry.Points;
        }

        int? afterGiftStack = null;
        string afterGiftItemId = string.Empty;
        int? afterGiftQuality = null;
        int? afterGiftSlot = null;

        if (actionKind == "gift" && beforeGiftSlot.HasValue && beforeGiftSlot.Value < Game1.player.Items.Count)
        {
            var afterItem = Game1.player.Items[beforeGiftSlot.Value];
            afterGiftStack = afterItem?.Stack;
            afterGiftItemId = afterItem?.QualifiedItemId ?? string.Empty;
            afterGiftQuality = (afterItem as StardewValley.Object)?.Quality;
            afterGiftSlot = beforeGiftSlot.Value;
        }

        if (actionKind == "talk" && !handled)
        {
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "blocked",
                "observed_mismatch", new[] { "native_checkAction_not_handled_for_talk" },
                "native_checkAction_not_handled_for_talk", "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "talk" && handled)
        {
            var hasTalkChange = afterTalkedToToday != beforeTalkedToToday;
            var hasDialogueChange = afterDialogueCount != beforeDialogueCount ||
                !string.Equals(afterDialogueKey, beforeDialogueKey, StringComparison.Ordinal) ||
                afterDialogueOpen != beforeDialogueOpen ||
                !string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal);
            var hasFriendshipChange = afterPoints != beforePoints;
            var socialTransitionObserved = hasTalkChange || hasDialogueChange || hasFriendshipChange ||
                afterMenuOpen != beforeMenuOpen ||
                afterMenuType != beforeMenuType;

            if (!socialTransitionObserved)
            {
                return BuildSocialInteractResult(request, handled, npcName,
                    beforeNpcLocation, afterNpcLocation,
                    beforeNpcTile, afterNpcTile,
                    beforeNpcVisible, afterNpcVisible,
                    beforeNpcSleeping, afterNpcSleeping,
                    beforeNpcOrdinary, afterNpcOrdinary,
                    afterNpcPresent,
                    beforePlayerTile, afterPlayerTile,
                    beforeFacing, afterFacing,
                    beforeSelectedSlot, afterSelectedSlot,
                    beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                    beforeDialogueOpen, afterDialogueOpen,
                    beforeDialogueCount, afterDialogueCount,
                    beforeDialogueKey, afterDialogueKey,
                    beforeDialogueSpeakerName, afterDialogueSpeakerName,
                    beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                    beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                    beforeFriendshipRowExists, afterFriendshipRowExists,
                    beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                    beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                    true, "blocked",
                    "observed_mismatch", new[] { "native_handled_but_no_social_transition_observed" },
                    "native_handled_but_no_social_transition_observed", "executor_calibration",
                    startedAt, completedAt, actualTicks);
            }

            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "applied",
                "verified", new[] { "native_talk_handled", "observable_social_transition" },
                string.Empty, string.Empty,
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "gift" && !handled)
        {
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, "blocked",
                "observed_mismatch", new[] { "native_checkAction_not_handled_for_gift" },
                "native_checkAction_not_handled_for_gift", "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        if (actionKind == "gift")
        {
            bool itemConsumed;
            if (!afterGiftStack.HasValue)
            {
                itemConsumed = beforeGiftStack.HasValue && beforeGiftStack.Value == 1;
            }
            else
            {
                itemConsumed = beforeGiftStack.HasValue &&
                    string.Equals(afterGiftItemId, beforeGiftItemId, StringComparison.Ordinal) &&
                    afterGiftStack.Value == beforeGiftStack.Value - 1;
            }

            if (!itemConsumed && afterGiftStack.HasValue && afterGiftStack.Value > 0 &&
                beforeGiftSlot.HasValue && beforeGiftSlot.Value < Game1.player.Items.Count)
            {
                var slotItem = Game1.player.Items[beforeGiftSlot.Value];
                if (slotItem is not null &&
                    !string.Equals(slotItem.QualifiedItemId, beforeGiftItemId, StringComparison.Ordinal))
                {
                    itemConsumed = false;
                }
            }

            var hasDialogueChange = afterDialogueCount != beforeDialogueCount ||
                !string.Equals(afterDialogueKey, beforeDialogueKey, StringComparison.Ordinal) ||
                afterDialogueOpen != beforeDialogueOpen ||
                !string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal);
            var hasFriendshipChange = afterPoints != beforePoints;
            var hasGiftCounterChange = afterGiftsToday != beforeGiftsToday || afterGiftsThisWeek != beforeGiftsThisWeek;
            var hasSocialEffect = hasDialogueChange || hasFriendshipChange || hasGiftCounterChange ||
                afterMenuOpen != beforeMenuOpen || afterMenuType != beforeMenuType;

            if (!itemConsumed && handled)
            {
                return BuildSocialInteractResult(request, handled, npcName,
                    beforeNpcLocation, afterNpcLocation,
                    beforeNpcTile, afterNpcTile,
                    beforeNpcVisible, afterNpcVisible,
                    beforeNpcSleeping, afterNpcSleeping,
                    beforeNpcOrdinary, afterNpcOrdinary,
                    afterNpcPresent,
                    beforePlayerTile, afterPlayerTile,
                    beforeFacing, afterFacing,
                    beforeSelectedSlot, afterSelectedSlot,
                    beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                    beforeDialogueOpen, afterDialogueOpen,
                    beforeDialogueCount, afterDialogueCount,
                    beforeDialogueKey, afterDialogueKey,
                    beforeDialogueSpeakerName, afterDialogueSpeakerName,
                    beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                    beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                    beforeFriendshipRowExists, afterFriendshipRowExists,
                    beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                    beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                    true, "blocked",
                    "observed_mismatch", new[] { "native_handled_but_gift_item_not_consumed" },
                    "native_handled_but_gift_item_not_consumed", "executor_calibration",
                    startedAt, completedAt, actualTicks);
            }

            var verified = handled && itemConsumed && hasSocialEffect;
            return BuildSocialInteractResult(request, handled, npcName,
                beforeNpcLocation, afterNpcLocation,
                beforeNpcTile, afterNpcTile,
                beforeNpcVisible, afterNpcVisible,
                beforeNpcSleeping, afterNpcSleeping,
                beforeNpcOrdinary, afterNpcOrdinary,
                afterNpcPresent,
                beforePlayerTile, afterPlayerTile,
                beforeFacing, afterFacing,
                beforeSelectedSlot, afterSelectedSlot,
                beforeMenuOpen, afterMenuOpen, beforeMenuType, afterMenuType,
                beforeDialogueOpen, afterDialogueOpen,
                beforeDialogueCount, afterDialogueCount,
                beforeDialogueKey, afterDialogueKey,
                beforeDialogueSpeakerName, afterDialogueSpeakerName,
                beforePoints, afterPoints, beforeTalkedToToday, afterTalkedToToday,
                beforeGiftsToday, afterGiftsToday, beforeGiftsThisWeek, afterGiftsThisWeek,
                beforeFriendshipRowExists, afterFriendshipRowExists,
                beforeGiftStack, afterGiftStack, beforeGiftItemId, afterGiftItemId,
                beforeGiftQuality, afterGiftQuality, beforeGiftSlot, afterGiftSlot,
                true, verified ? "applied" : "blocked",
                verified ? "verified" : "observed_mismatch",
                verified ? new[] { "native_gift_handled", "exact_one_item_consumed", "observable_social_effect" }
                    : new[] { "native_gift_handled_but_incomplete_verification" },
                verified ? string.Empty : "native_gift_handled_but_incomplete_verification",
                "executor_calibration",
                startedAt, completedAt, actualTicks);
        }

        return BuildSocialBlockedResult(request, true, null, "social_interact", "social_unexpected_state_after_interact");
    }

    private static TrainingExecutionResult BuildSocialBlockedResult(
        TrainingExecutionRequest request, bool npcResolved, NPC? npc, string primitiveKind, params string[] reasons)
    {
        var safePlayer = Context.IsWorldReady ? Game1.player : null;
        var safeLocation = Context.IsWorldReady ? Game1.currentLocation : null;
        var allReasons = new List<string>(reasons);

        if (!Context.IsWorldReady)
        {
            allReasons.Insert(0, "world_not_ready");
        }

        var npcName = request.SocialNpcName ?? string.Empty;

        var beforePoints = 0;
        var beforeTalkedToToday = false;
        var beforeGiftsToday = 0;
        var beforeGiftsThisWeek = 0;
        bool? beforeFriendshipRowExists = null;
        if (npcResolved && !string.IsNullOrWhiteSpace(npcName) && safePlayer is not null &&
            safePlayer.friendshipData.TryGetValue(npcName, out var beforeFriendshipEntry))
        {
            beforeFriendshipRowExists = true;
            beforePoints = beforeFriendshipEntry.Points;
            beforeTalkedToToday = beforeFriendshipEntry.TalkedToToday;
            beforeGiftsToday = beforeFriendshipEntry.GiftsToday;
            beforeGiftsThisWeek = beforeFriendshipEntry.GiftsThisWeek;
        }
        else if (npcResolved && !string.IsNullOrWhiteSpace(npcName) && safePlayer is not null)
        {
            beforeFriendshipRowExists = false;
        }

        int? beforeGiftStack = null;
        string beforeGiftItemId = string.Empty;
        int? beforeGiftQuality = null;
        int? beforeGiftSlot = null;
        if (request.SocialActionKind == "gift" && request.SocialGiftSlotIndex.HasValue && safePlayer is not null)
        {
            var slotIndex = request.SocialGiftSlotIndex.Value;
            if (slotIndex >= 0 && slotIndex < safePlayer.Items.Count)
            {
                var item = safePlayer.Items[slotIndex];
                if (item is not null)
                {
                    beforeGiftStack = item.Stack;
                    beforeGiftItemId = item.QualifiedItemId;
                    beforeGiftQuality = (item as StardewValley.Object)?.Quality;
                    beforeGiftSlot = slotIndex;
                }
            }
        }

        var blockedCurrentDialogue = npc is not null ? npc.CurrentDialogue : null;
        var beforeDialogueCount = blockedCurrentDialogue?.Count;
        var beforeDialogueKey = beforeDialogueCount.GetValueOrDefault(0) > 0 && blockedCurrentDialogue is not null
            ? blockedCurrentDialogue.Peek().TranslationKey : string.Empty;

        var safePlayerTile = safePlayer?.TilePoint;
        var safeLocationName = safeLocation?.NameOrUniqueName ?? string.Empty;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ActualTicks = 0,
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = allReasons.ToArray(),
            RequestedEffect = SocialInteractRequestedEffect(request),
            ObservedEffect = SocialInteractObservedEffect(),
            BlockReasons = allReasons.ToArray(),
            FailureCategory = allReasons.Count > 0 ? allReasons[0] : string.Empty,
            TrainingImpactScope = "executor_calibration",
            SocialActionKind = request.SocialActionKind ?? string.Empty,
            SocialNpcName = npcName,
            SocialNativeHandled = false,
            SocialNpcPresentBefore = npcResolved ? true : null,
            SocialNpcPresentAfter = npcResolved ? true : null,
            SocialNpcLocationBefore = npcResolved && safeLocation is not null ? (npc?.currentLocation?.NameOrUniqueName ?? safeLocationName) : string.Empty,
            SocialNpcLocationAfter = npcResolved && safeLocation is not null ? (npc?.currentLocation?.NameOrUniqueName ?? safeLocationName) : string.Empty,
            SocialNpcTileXBefore = npcResolved && npc is not null ? npc.TilePoint.X : null,
            SocialNpcTileYBefore = npcResolved && npc is not null ? npc.TilePoint.Y : null,
            SocialNpcTileXAfter = npcResolved && npc is not null ? npc.TilePoint.X : null,
            SocialNpcTileYAfter = npcResolved && npc is not null ? npc.TilePoint.Y : null,
            SocialNpcVisibleBefore = npcResolved && npc is not null ? !npc.IsInvisible : null,
            SocialNpcVisibleAfter = npcResolved && npc is not null ? !npc.IsInvisible : null,
            SocialNpcSleepingBefore = npcResolved && npc is not null ? npc.isSleeping.Value : null,
            SocialNpcSleepingAfter = npcResolved && npc is not null ? npc.isSleeping.Value : null,
            SocialNpcOrdinaryBefore = npcResolved && npc is not null ? npc.IsVillager && !npc.IsMonster : null,
            SocialNpcOrdinaryAfter = npcResolved && npc is not null ? npc.IsVillager && !npc.IsMonster : null,
            SocialPlayerTileXBefore = safePlayerTile?.X,
            SocialPlayerTileYBefore = safePlayerTile?.Y,
            SocialPlayerFacingBefore = safePlayer?.FacingDirection,
            SocialPlayerSelectedSlotBefore = safePlayer?.CurrentToolIndex,
            SocialPlayerTileXAfter = safePlayerTile?.X,
            SocialPlayerTileYAfter = safePlayerTile?.Y,
            SocialPlayerFacingAfter = safePlayer?.FacingDirection,
            SocialPlayerSelectedSlotAfter = safePlayer?.CurrentToolIndex,
            SocialMenuOpenBefore = Game1.activeClickableMenu is not null,
            SocialMenuTypeBefore = Game1.activeClickableMenu?.GetType().Name ?? "none",
            SocialDialogueOpenBefore = Game1.dialogueUp,
            SocialCurrentDialogueSpeakerNameBefore = Game1.currentSpeaker?.Name ?? string.Empty,
            SocialCurrentDialogueCountBefore = beforeDialogueCount,
            SocialCurrentDialogueKeyBefore = beforeDialogueKey,
            SocialMenuOpenAfter = Game1.activeClickableMenu is not null,
            SocialMenuTypeAfter = Game1.activeClickableMenu?.GetType().Name ?? "none",
            SocialDialogueOpenAfter = Game1.dialogueUp,
            SocialCurrentDialogueSpeakerNameAfter = Game1.currentSpeaker?.Name ?? string.Empty,
            SocialCurrentDialogueCountAfter = beforeDialogueCount,
            SocialCurrentDialogueKeyAfter = beforeDialogueKey,
            SocialFriendshipPointsBefore = beforeFriendshipRowExists == true ? beforePoints : null,
            SocialFriendshipPointsAfter = beforeFriendshipRowExists == true ? beforePoints : null,
            SocialTalkedToTodayBefore = beforeFriendshipRowExists == true ? beforeTalkedToToday : null,
            SocialTalkedToTodayAfter = beforeFriendshipRowExists == true ? beforeTalkedToToday : null,
            SocialGiftsTodayBefore = beforeFriendshipRowExists == true ? beforeGiftsToday : null,
            SocialGiftsTodayAfter = beforeFriendshipRowExists == true ? beforeGiftsToday : null,
            SocialGiftsThisWeekBefore = beforeFriendshipRowExists == true ? beforeGiftsThisWeek : null,
            SocialGiftsThisWeekAfter = beforeFriendshipRowExists == true ? beforeGiftsThisWeek : null,
            SocialFriendshipRowExistsBefore = beforeFriendshipRowExists,
            SocialFriendshipRowExistsAfter = beforeFriendshipRowExists,
            SocialGiftItemIdBefore = beforeGiftItemId,
            SocialGiftItemIdAfter = beforeGiftItemId,
            SocialGiftStackBefore = beforeGiftStack,
            SocialGiftStackAfter = beforeGiftStack,
            SocialGiftQualityBefore = beforeGiftQuality,
            SocialGiftQualityAfter = beforeGiftQuality,
            SocialGiftSlotBefore = beforeGiftSlot,
            SocialGiftSlotAfter = beforeGiftSlot
        };
    }

    private static TrainingExecutionResult BuildSocialInteractResult(
        TrainingExecutionRequest request, bool handled,
        string npcName,
        string beforeNpcLocation, string afterNpcLocation,
        Point beforeNpcTile, Point? afterNpcTile,
        bool beforeNpcVisible, bool? afterNpcVisible,
        bool beforeNpcSleeping, bool? afterNpcSleeping,
        bool beforeNpcOrdinary, bool? afterNpcOrdinary,
        bool afterNpcPresent,
        Point beforePlayerTile, Point afterPlayerTile,
        int beforeFacing, int afterFacing,
        int beforeSelectedSlot, int afterSelectedSlot,
        bool beforeMenuOpen, bool afterMenuOpen, string beforeMenuType, string afterMenuType,
        bool beforeDialogueOpen, bool afterDialogueOpen,
        int beforeDialogueCount, int? afterDialogueCount,
        string beforeDialogueKey, string afterDialogueKey,
        string beforeDialogueSpeakerName, string afterDialogueSpeakerName,
        int beforePoints, int afterPoints, bool beforeTalkedToToday, bool afterTalkedToToday,
        int beforeGiftsToday, int afterGiftsToday, int beforeGiftsThisWeek, int afterGiftsThisWeek,
        bool beforeFriendshipRowExists, bool afterFriendshipRowExists,
        int? beforeGiftStack, int? afterGiftStack, string beforeGiftItemId, string afterGiftItemId,
        int? beforeGiftQuality, int? afterGiftQuality, int? beforeGiftSlot, int? afterGiftSlot,
        bool includeChangedFacts, string status, string verificationStatus, string[] verificationReasons,
        string failureCategory, string trainingImpactScope,
        string startedAt, string completedAt, int actualTicks)
    {
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = status,
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ActualTicks = actualTicks,
            PrimitiveKind = "social_interact",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = SocialInteractRequestedEffect(request),
            ObservedEffect = SocialInteractObservedEffect(),
            BlockReasons = status == "blocked" ? verificationReasons : Array.Empty<string>(),
            FailureCategory = failureCategory,
            TrainingImpactScope = trainingImpactScope,
            SocialNpcName = npcName,
            SocialNpcPresentBefore = true,
            SocialNpcPresentAfter = afterNpcPresent,
            SocialNpcLocationBefore = beforeNpcLocation,
            SocialNpcLocationAfter = afterNpcPresent ? afterNpcLocation : string.Empty,
            SocialNpcTileXBefore = beforeNpcTile.X,
            SocialNpcTileYBefore = beforeNpcTile.Y,
            SocialNpcTileXAfter = afterNpcPresent ? afterNpcTile?.X : null,
            SocialNpcTileYAfter = afterNpcPresent ? afterNpcTile?.Y : null,
            SocialNpcVisibleBefore = beforeNpcVisible,
            SocialNpcVisibleAfter = afterNpcPresent ? afterNpcVisible : null,
            SocialNpcSleepingBefore = beforeNpcSleeping,
            SocialNpcSleepingAfter = afterNpcPresent ? afterNpcSleeping : null,
            SocialNpcOrdinaryBefore = beforeNpcOrdinary,
            SocialNpcOrdinaryAfter = afterNpcPresent ? afterNpcOrdinary : null,
            SocialPlayerTileXBefore = beforePlayerTile.X,
            SocialPlayerTileYBefore = beforePlayerTile.Y,
            SocialPlayerTileXAfter = afterPlayerTile.X,
            SocialPlayerTileYAfter = afterPlayerTile.Y,
            SocialPlayerFacingBefore = beforeFacing,
            SocialPlayerFacingAfter = afterFacing,
            SocialPlayerSelectedSlotBefore = beforeSelectedSlot,
            SocialPlayerSelectedSlotAfter = afterSelectedSlot,
            SocialActionKind = request.SocialActionKind,
            SocialNativeHandled = handled,
            SocialGiftItemIdBefore = beforeGiftItemId,
            SocialGiftItemIdAfter = afterGiftItemId,
            SocialGiftStackBefore = beforeGiftStack,
            SocialGiftStackAfter = afterGiftStack,
            SocialGiftQualityBefore = beforeGiftQuality,
            SocialGiftQualityAfter = afterGiftQuality,
            SocialGiftSlotBefore = beforeGiftSlot,
            SocialGiftSlotAfter = afterGiftSlot,
            SocialFriendshipPointsBefore = beforePoints,
            SocialFriendshipPointsAfter = afterPoints,
            SocialTalkedToTodayBefore = beforeTalkedToToday,
            SocialTalkedToTodayAfter = afterTalkedToToday,
            SocialGiftsTodayBefore = beforeGiftsToday,
            SocialGiftsTodayAfter = afterGiftsToday,
            SocialGiftsThisWeekBefore = beforeGiftsThisWeek,
            SocialGiftsThisWeekAfter = afterGiftsThisWeek,
            SocialMenuOpenBefore = beforeMenuOpen,
            SocialMenuOpenAfter = afterMenuOpen,
            SocialMenuTypeBefore = beforeMenuType,
            SocialMenuTypeAfter = afterMenuType,
            SocialDialogueOpenBefore = beforeDialogueOpen,
            SocialDialogueOpenAfter = afterDialogueOpen,
            SocialCurrentDialogueCountBefore = beforeDialogueCount,
            SocialCurrentDialogueCountAfter = afterNpcPresent ? afterDialogueCount : null,
            SocialCurrentDialogueKeyBefore = beforeDialogueKey,
            SocialCurrentDialogueKeyAfter = afterDialogueKey,
            SocialCurrentDialogueSpeakerNameBefore = beforeDialogueSpeakerName,
            SocialCurrentDialogueSpeakerNameAfter = afterDialogueSpeakerName,
            SocialFriendshipRowExistsBefore = beforeFriendshipRowExists,
            SocialFriendshipRowExistsAfter = afterFriendshipRowExists
        };

        if (includeChangedFacts)
        {
            var changedFacts = new List<SimulatedFactChange>();
            if (afterMenuOpen != beforeMenuOpen)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = beforeMenuOpen.ToString().ToLowerInvariant(), After = afterMenuOpen.ToString().ToLowerInvariant() });
            }
            if (!string.Equals(afterMenuType, beforeMenuType, StringComparison.Ordinal))
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenuType, After = afterMenuType });
            }
            if (afterDialogueOpen != beforeDialogueOpen)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.dialogue.is_open", Before = beforeDialogueOpen.ToString().ToLowerInvariant(), After = afterDialogueOpen.ToString().ToLowerInvariant() });
            }
            if (!string.Equals(afterDialogueSpeakerName, beforeDialogueSpeakerName, StringComparison.Ordinal))
            {
                changedFacts.Add(new SimulatedFactChange { Path = "menus.dialogue.speaker_name", Before = beforeDialogueSpeakerName, After = afterDialogueSpeakerName });
            }
            if (afterFacing != beforeFacing)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.facing_direction", Before = beforeFacing.ToString(), After = afterFacing.ToString() });
            }
            if (afterSelectedSlot != beforeSelectedSlot)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.current_tool_index", Before = beforeSelectedSlot.ToString(), After = afterSelectedSlot.ToString() });
            }
            if (afterPoints != beforePoints)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".points", Before = beforePoints.ToString(), After = afterPoints.ToString() });
            }
            if (afterTalkedToToday != beforeTalkedToToday)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".talked_to_today", Before = beforeTalkedToToday.ToString().ToLowerInvariant(), After = afterTalkedToToday.ToString().ToLowerInvariant() });
            }
            if (afterGiftsToday != beforeGiftsToday)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".gifts_today", Before = beforeGiftsToday.ToString(), After = afterGiftsToday.ToString() });
            }
            if (afterGiftsThisWeek != beforeGiftsThisWeek)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".gifts_this_week", Before = beforeGiftsThisWeek.ToString(), After = afterGiftsThisWeek.ToString() });
            }
            if (afterFriendshipRowExists != beforeFriendshipRowExists)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "npcs.friendships." + npcName + ".row_exists", Before = beforeFriendshipRowExists.ToString().ToLowerInvariant(), After = afterFriendshipRowExists.ToString().ToLowerInvariant() });
            }
            if (beforeGiftStack.HasValue && !afterGiftStack.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].stack", Before = beforeGiftStack.Value.ToString(), After = "null" });
                if (!string.IsNullOrWhiteSpace(beforeGiftItemId))
                {
                    changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].qualified_item_id", Before = beforeGiftItemId, After = string.Empty });
                }
            }
            else if (beforeGiftStack.HasValue && afterGiftStack.HasValue && afterGiftStack.Value != beforeGiftStack.Value)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].stack", Before = beforeGiftStack.Value.ToString(), After = afterGiftStack.Value.ToString() });
            }
            if (!string.IsNullOrWhiteSpace(beforeGiftItemId) && beforeGiftItemId != afterGiftItemId)
            {
                changedFacts.Add(new SimulatedFactChange { Path = "player.inventory[" + beforeGiftSlot + "].qualified_item_id", Before = beforeGiftItemId, After = afterGiftItemId });
            }
            if (changedFacts.Count > 0)
            {
                result.ChangedFacts = changedFacts.ToArray();
            }
        }

        return result;
    }

    private static string SocialInteractRequestedEffect(TrainingExecutionRequest request)
    {
        var kind = string.IsNullOrWhiteSpace(request.SocialActionKind) ? "missing" : request.SocialActionKind;
        var npcName = string.IsNullOrWhiteSpace(request.SocialNpcName) ? "missing" : request.SocialNpcName;
        var effect = "social.kind=" + kind + ";npc=" + npcName;
        if (kind == "gift")
        {
            effect += ";slot=" + (request.SocialGiftSlotIndex?.ToString() ?? "missing") +
                ";item=" + (string.IsNullOrWhiteSpace(request.SocialGiftQualifiedItemId) ? "missing" : request.SocialGiftQualifiedItemId);
        }
        return effect;
    }

    private static string SocialInteractObservedEffect()
    {
        var safePlayer = Context.IsWorldReady ? Game1.player : null;
        var safeLocation = Context.IsWorldReady ? Game1.currentLocation : null;

        return "location=" + (safeLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + (safePlayer?.TilePoint.X.ToString() ?? "none") + "," + (safePlayer?.TilePoint.Y.ToString() ?? "none") +
            ";menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";dialogue_up=" + Game1.dialogueUp.ToString().ToLowerInvariant();
    }
}
