using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteQuestNpcInteract(TrainingExecutionRequest request)
    {
        var requestedEffect = QuestRequestedEffect(request);
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            return QuestBlocked(request, requestedEffect, "request=invalid", genericReasons.ToArray());
        }
        if (request.QuestFamily is not ("ordinary_quest" or "special_order") ||
            request.QuestInteractionKind is not ("report" or "offer_item") ||
            string.IsNullOrWhiteSpace(request.SocialNpcName) ||
            string.IsNullOrWhiteSpace(request.LocationId))
        {
            return QuestBlocked(request, requestedEffect, "quest_request=missing_typed_identity", "quest_typed_identity_required");
        }
        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, request.LocationId, StringComparison.Ordinal))
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_location_id_mismatch");
        }
        if (Game1.activeClickableMenu is not null)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_npc_interact_menu_must_be_clear");
        }
        if (!request.SocialObservedNpcTileX.HasValue || !request.SocialObservedNpcTileY.HasValue)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_observed_npc_tile_required");
        }

        var npc = Game1.currentLocation.characters.FirstOrDefault(character =>
            string.Equals(character.Name, request.SocialNpcName, StringComparison.Ordinal));
        if (npc is null || !npc.IsVillager || npc.IsMonster || npc.IsInvisible || npc.isSleeping.Value || !npc.CanSocialize)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_target_npc_not_interactable");
        }
        var npcTile = npc.TilePoint;
        if (npcTile.X != request.SocialObservedNpcTileX.Value ||
            npcTile.Y != request.SocialObservedNpcTileY.Value)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_target_npc_tile_drifted");
        }
        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - npcTile.X) + Math.Abs(playerTile.Y - npcTile.Y) != 1)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_player_not_adjacent_to_npc");
        }
        var actionTargetRectangle = new XnaRectangle(npcTile.X * 64, npcTile.Y * 64, 64, 64);
        if (!npc.GetBoundingBox().Intersects(actionTargetRectangle))
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_npc_action_rectangle_drifted");
        }

        Item? offeredItem = null;
        int? offeredSlot = null;
        if (request.QuestInteractionKind == "offer_item")
        {
            offeredSlot = request.SocialGiftSlotIndex;
            if (!offeredSlot.HasValue || offeredSlot.Value < 0 || offeredSlot.Value >= Game1.player.Items.Count)
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_offer_slot_invalid");
            }
            offeredItem = Game1.player.Items[offeredSlot.Value];
            if (offeredItem is null ||
                offeredItem.Stack <= 0 ||
                string.IsNullOrWhiteSpace(request.SocialGiftQualifiedItemId) ||
                !string.Equals(offeredItem.QualifiedItemId, request.SocialGiftQualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_offer_item_identity_drifted");
            }
        }
        else if (Game1.player.ActiveObject is not null)
        {
            return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_report_active_object_must_be_cleared");
        }

        Quest? ordinaryQuest = null;
        SpecialOrder? specialOrder = null;
        OrderObjective? specialObjective = null;
        if (request.QuestFamily == "ordinary_quest")
        {
            ordinaryQuest = Game1.player.questLog.SingleOrDefault(quest =>
                string.Equals(quest.id.Value, request.QuestId, StringComparison.Ordinal) &&
                string.Equals(quest.GetType().Name, request.QuestRuntimeType, StringComparison.Ordinal));
            if (ordinaryQuest is null)
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "quest_live_identity_not_found");
            }
            var liveCurrent = OrdinaryQuestCurrentCount(ordinaryQuest);
            var liveTarget = OrdinaryQuestTargetCount(ordinaryQuest);
            if (request.QuestExpectedCurrentCount != liveCurrent ||
                request.QuestExpectedTargetCount != liveTarget)
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(ordinaryQuest), "quest_progress_projection_drifted");
            }
        }
        else
        {
            specialOrder = Game1.player.team.specialOrders.SingleOrDefault(order =>
                string.Equals(order.questKey.Value, request.QuestKey, StringComparison.Ordinal));
            if (specialOrder is null ||
                !request.QuestObjectiveIndex.HasValue ||
                request.QuestObjectiveIndex.Value < 0 ||
                request.QuestObjectiveIndex.Value >= specialOrder.objectives.Count)
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(), "special_order_live_objective_not_found");
            }
            specialObjective = specialOrder.objectives[request.QuestObjectiveIndex.Value];
            if (!string.Equals(specialObjective.GetType().Name, "DeliverObjective", StringComparison.Ordinal) ||
                request.QuestExpectedCurrentCount != specialObjective.GetCount() ||
                request.QuestExpectedTargetCount != specialObjective.GetMaxCount())
            {
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(order: specialOrder, objective: specialObjective), "special_order_objective_projection_drifted");
            }
        }

        var probeAccepted = false;
        if (request.QuestInteractionKind == "offer_item" && offeredItem is not null)
        {
            var receiver = FirstNativeQuestOfferReceiver(npc, offeredItem);
            probeAccepted = request.QuestFamily == "ordinary_quest"
                ? ReferenceEquals(receiver.Quest, ordinaryQuest)
                : ReferenceEquals(receiver.Order, specialOrder);
        }
        else if (ordinaryQuest is not null)
        {
            probeAccepted = ordinaryQuest.OnNpcSocialized(npc, probe: true);
        }
        if (!probeAccepted)
        {
            return QuestBlocked(
                request,
                requestedEffect,
                ordinaryQuest is not null ? QuestObservedEffect(ordinaryQuest) : QuestObservedEffect(order: specialOrder, objective: specialObjective),
                "native_quest_probe_rejected");
        }

        var beforePresent = ordinaryQuest is not null
            ? Game1.player.questLog.Contains(ordinaryQuest)
            : specialOrder is not null && Game1.player.team.specialOrders.Contains(specialOrder);
        var beforeCompleted = ordinaryQuest?.completed.Value ??
            (specialOrder is not null && specialOrder.questState.Value == SpecialOrderStatus.Complete);
        var beforeProgress = ordinaryQuest is not null
            ? OrdinaryQuestCurrentCount(ordinaryQuest)
            : specialObjective?.GetCount();
        var beforeFingerprint = ordinaryQuest is not null
            ? OrdinaryQuestFingerprint(ordinaryQuest)
            : SpecialOrderFingerprint(specialOrder, specialObjective);
        var beforeSelectedSlot = Game1.player.CurrentToolIndex;

        if (offeredSlot.HasValue)
        {
            Game1.player.CurrentToolIndex = offeredSlot.Value;
            if (Game1.player.ActiveObject is null ||
                !string.Equals(Game1.player.ActiveObject.QualifiedItemId, offeredItem?.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                Game1.player.CurrentToolIndex = beforeSelectedSlot;
                return QuestBlocked(request, requestedEffect, QuestObservedEffect(ordinaryQuest, specialOrder, specialObjective), "quest_offer_active_object_not_selected");
            }
        }

        var startTicks = Game1.ticks;
        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        Game1.player.faceDirection(DirectionTo(playerTile, npcTile));
        var viewportRect = new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
        var handled = Game1.currentLocation.checkAction(
            new TileLocation(npcTile.X, npcTile.Y),
            viewportRect,
            Game1.player);
        var completedAt = DateTimeOffset.UtcNow.ToString("O");
        var actualTicks = Math.Max(0, Game1.ticks - startTicks);

        var afterPresent = ordinaryQuest is not null
            ? Game1.player.questLog.Contains(ordinaryQuest)
            : specialOrder is not null && Game1.player.team.specialOrders.Contains(specialOrder);
        var afterCompleted = ordinaryQuest?.completed.Value ??
            (specialOrder is not null && specialOrder.questState.Value == SpecialOrderStatus.Complete);
        var afterProgress = ordinaryQuest is not null
            ? OrdinaryQuestCurrentCount(ordinaryQuest)
            : specialObjective?.GetCount();
        var afterFingerprint = ordinaryQuest is not null
            ? OrdinaryQuestFingerprint(ordinaryQuest)
            : SpecialOrderFingerprint(specialOrder, specialObjective);
        var progressed = beforePresent != afterPresent ||
            beforeCompleted != afterCompleted ||
            beforeProgress != afterProgress ||
            !string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal);
        var verified = handled && progressed;
        var reasons = verified
            ? new[] { "native_checkAction_handled", "matching_quest_progress_changed" }
            : new[] { handled ? "native_handled_without_matching_quest_progress" : "native_checkAction_not_handled" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = request.LocationId,
            TargetTileX = npcTile.X,
            TargetTileY = npcTile.Y,
            ActualTicks = actualTicks,
            FailureCategory = verified ? string.Empty : "observed_mismatch",
            TrainingImpactScope = verified ? string.Empty : "executor_calibration",
            StartedAt = startedAt,
            CompletedAt = completedAt,
            PrimitiveKind = "quest_npc_interact",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = requestedEffect,
            ObservedEffect = QuestObservedEffect(ordinaryQuest, specialOrder, specialObjective),
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestId = request.QuestId,
            QuestKey = request.QuestKey,
            QuestObjectiveIndex = request.QuestObjectiveIndex,
            QuestProgressBefore = beforeProgress,
            QuestProgressAfter = afterProgress,
            QuestTargetCount = request.QuestExpectedTargetCount,
            QuestPresentBefore = beforePresent,
            QuestPresentAfter = afterPresent,
            QuestCompletedBefore = beforeCompleted,
            QuestCompletedAfter = afterCompleted
        };
    }

    private static TrainingExecutionResult QuestBlocked(
        TrainingExecutionRequest request,
        string requestedEffect,
        string observedEffect,
        params string[] reasons)
    {
        var result = BlockedWithPrimitive(request, "quest_npc_interact", requestedEffect, observedEffect, reasons);
        result.QuestCandidateId = request.QuestCandidateId;
        result.QuestFamily = request.QuestFamily;
        result.QuestId = request.QuestId;
        result.QuestKey = request.QuestKey;
        result.QuestObjectiveIndex = request.QuestObjectiveIndex;
        result.QuestTargetCount = request.QuestExpectedTargetCount;
        return result;
    }

    private static string QuestRequestedEffect(TrainingExecutionRequest request)
    {
        return "quest_candidate_id=" + request.QuestCandidateId +
            ";quest_family=" + request.QuestFamily +
            ";quest_id=" + request.QuestId +
            ";quest_key=" + request.QuestKey +
            ";objective_index=" + request.QuestObjectiveIndex +
            ";interaction_kind=" + request.QuestInteractionKind +
            ";expected_current=" + request.QuestExpectedCurrentCount +
            ";expected_target=" + request.QuestExpectedTargetCount;
    }

    private static string QuestObservedEffect(
        Quest? quest = null,
        SpecialOrder? order = null,
        OrderObjective? objective = null)
    {
        if (quest is not null)
        {
            return "quest_id=" + quest.id.Value +
                ";runtime_type=" + quest.GetType().Name +
                ";present=" + Game1.player.questLog.Contains(quest).ToString().ToLowerInvariant() +
                ";completed=" + quest.completed.Value.ToString().ToLowerInvariant() +
                ";current=" + OrdinaryQuestCurrentCount(quest) +
                ";target=" + OrdinaryQuestTargetCount(quest);
        }
        if (order is not null && objective is not null)
        {
            return "quest_key=" + order.questKey.Value +
                ";objective_runtime_type=" + objective.GetType().Name +
                ";present=" + Game1.player.team.specialOrders.Contains(order).ToString().ToLowerInvariant() +
                ";state=" + order.questState.Value +
                ";current=" + objective.GetCount() +
                ";target=" + objective.GetMaxCount();
        }
        return "quest=unresolved";
    }

    private static int OrdinaryQuestCurrentCount(Quest quest)
    {
        return quest switch
        {
            SlayMonsterQuest value => value.numberKilled.Value,
            FishingQuest value => value.numberFished.Value,
            ResourceCollectionQuest value => value.numberCollected.Value,
            SocializeQuest value => value.total.Value - value.whoToGreet.Count,
            _ => 0
        };
    }

    private static (SpecialOrder? Order, Quest? Quest) FirstNativeQuestOfferReceiver(NPC npc, Item item)
    {
        foreach (var order in Game1.player.team.specialOrders)
        {
            if (order.onItemDelivered is null)
            {
                continue;
            }
            foreach (var callback in order.onItemDelivered.GetInvocationList()
                         .Cast<Func<Farmer, NPC, Item, bool, int>>())
            {
                if (callback(Game1.player, npc, item, true) > 0)
                {
                    return (order, null);
                }
            }
        }

        for (var index = Game1.player.questLog.Count - 1; index >= 0; index--)
        {
            var quest = Game1.player.questLog[index];
            if (!quest.completed.Value && quest.OnItemOfferedToNpc(npc, item, probe: true))
            {
                return (null, quest);
            }
        }
        return (null, null);
    }

    private static int OrdinaryQuestTargetCount(Quest quest)
    {
        return quest switch
        {
            ItemDeliveryQuest value => value.number.Value,
            SlayMonsterQuest value => value.numberToKill.Value,
            FishingQuest value => value.numberToFish.Value,
            ResourceCollectionQuest value => value.number.Value,
            SocializeQuest value => value.total.Value,
            _ => 0
        };
    }

    private static string OrdinaryQuestFingerprint(Quest quest)
    {
        return quest.GetType().Name + "|" +
            quest.completed.Value + "|" +
            quest.destroy.Value + "|" +
            OrdinaryQuestCurrentCount(quest) + "|" +
            (quest is SocializeQuest socialize ? string.Join(",", socialize.whoToGreet.OrderBy(value => value, StringComparer.Ordinal)) : string.Empty);
    }

    private static string SpecialOrderFingerprint(SpecialOrder? order, OrderObjective? objective)
    {
        return order is null || objective is null
            ? string.Empty
            : order.questState.Value + "|" + objective.GetType().Name + "|" + objective.GetCount() + "|" + objective.IsComplete();
    }
}
