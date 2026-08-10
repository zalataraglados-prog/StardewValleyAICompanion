using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Museum;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartMuseumDonation(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.DonationTileX.HasValue || !request.DonationTileY.HasValue ||
            !request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue || !request.ExpectedStackAfter.HasValue ||
            !request.ExpectedDonatedCountBefore.HasValue || !request.ExpectedDonatedCountAfter.HasValue ||
            !request.MuseumTotalDonatableItems.HasValue || !request.ExpectedCollectionCompleteAfter.HasValue ||
            !request.ExpectedCompleteCollectionAchievementAfter.HasValue || !request.FieldGuideQuestPresentBefore.HasValue ||
            !request.FieldGuideQuestCompletedBefore.HasValue || !request.ExpectedFieldGuideQuestCompletedAfter.HasValue ||
            string.IsNullOrWhiteSpace(request.PendingRewardIdsBeforeJson) || string.IsNullOrWhiteSpace(request.PendingRewardIdsAfterJson) ||
            string.IsNullOrWhiteSpace(request.NewlyPendingRewardIdsJson) || string.IsNullOrWhiteSpace(request.AutoAppliedRewardIdsJson) ||
            string.IsNullOrWhiteSpace(request.AutoAppliedRewardActionsJson) || request.RewardProjectionStatus != "ready" ||
            !request.RustyKeyDonationThreshold.HasValue || !request.ReachesRustyKeyThreshold.HasValue ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", "request=missing_typed_projection", "museum_donation_typed_projection_required"));
            return;
        }
        if (activeMuseumDonation is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", MuseumDonationObservedEffect(), "museum_donation_player_busy"));
            return;
        }
        if (Game1.currentLocation is not LibraryMuseum museum ||
            !string.Equals(museum.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", MuseumDonationObservedEffect(), "museum_donation_target_location_mismatch"));
            return;
        }

        var actionTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var standTile = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var donationTile = new Point(request.DonationTileX.Value, request.DonationTileY.Value);
        var action = museum.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings");
        var mutex = MuseumMutex(museum);
        if (!string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "Gunther", StringComparison.OrdinalIgnoreCase) ||
            mutex is null || mutex.IsLocked() || !AreAdjacent(standTile, actionTile) ||
            !IsTileOnMap(museum, standTile) || !IsTileWalkable(museum, standTile) || IsTileOccupiedByCharacter(museum, standTile) ||
            !museum.isTileSuitableForMuseumPiece(donationTile.X, donationTile.Y))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", MuseumDonationObservedEffect(), "museum_donation_endpoint_or_display_tile_drifted"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var donatedCount = museum.museumPieces.Count();
        var total = LibraryMuseum.totalArtifacts;
        var expectedReachesThreshold = donatedCount < request.RustyKeyDonationThreshold.Value && donatedCount + 1 >= request.RustyKeyDonationThreshold.Value;
        var rewards = DataLoader.MuseumRewards(Game1.content);
        rewards.TryGetValue("museum60", out var rustyKeyReward);
        var liveRewardActions = rustyKeyReward?.RewardActions;
        var liveRewardAction = liveRewardActions?.Count == 1 ? liveRewardActions[0] : string.Empty;
        var fieldGuideQuest = Game1.player.questLog.FirstOrDefault(quest => quest.id.Value == "24");
        var rewardProjectionMatches = TryProjectRuntimeMuseumRewards(
            museum,
            Game1.player,
            rewards,
            item,
            out var rewardProjection) &&
            request.PendingRewardIdsBeforeJson == JsonSerializer.Serialize(rewardProjection.PendingRewardIdsBefore) &&
            request.PendingRewardIdsAfterJson == JsonSerializer.Serialize(rewardProjection.PendingRewardIdsAfter) &&
            request.NewlyPendingRewardIdsJson == JsonSerializer.Serialize(rewardProjection.NewlyPendingRewardIds) &&
            request.AutoAppliedRewardIdsJson == JsonSerializer.Serialize(rewardProjection.AutoAppliedRewardIds) &&
            request.AutoAppliedRewardActionsJson == JsonSerializer.Serialize(rewardProjection.AutoAppliedRewardActions);
        if (item is not StardewValley.Object || item.GetType().FullName != request.TargetRuntimeType ||
            item.QualifiedItemId != request.QualifiedItemId || item.ItemId != request.ItemId ||
            item.Stack != request.ExpectedStackBefore.Value || request.ExpectedStackAfter.Value != item.Stack - 1 ||
            !LibraryMuseum.IsItemSuitableForDonation(item.QualifiedItemId) ||
            donatedCount != request.ExpectedDonatedCountBefore.Value || request.ExpectedDonatedCountAfter.Value != donatedCount + 1 ||
            total != request.MuseumTotalDonatableItems.Value || request.ExpectedCollectionCompleteAfter.Value != (donatedCount + 1 >= total) ||
            request.ExpectedCompleteCollectionAchievementAfter.Value != (Game1.player.achievements.Contains(5) || donatedCount + 1 >= total) ||
            request.FieldGuideQuestPresentBefore.Value != (fieldGuideQuest is not null) ||
            request.FieldGuideQuestCompletedBefore.Value != IsMuseumFieldGuideQuestCompleted(fieldGuideQuest) ||
            request.ExpectedFieldGuideQuestCompletedAfter.Value != (fieldGuideQuest is not null) ||
            !rewardProjectionMatches ||
            request.ReachesRustyKeyThreshold.Value != expectedReachesThreshold ||
            string.IsNullOrWhiteSpace(liveRewardAction) || request.RustyKeyRewardAction != liveRewardAction ||
            expectedReachesThreshold && liveRewardAction != "MarkEventSeen Host 295672")
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", MuseumDonationObservedEffect(), "museum_donation_projection_drifted"));
            return;
        }

        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(museum, Game1.player.TilePoint, standTile, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "donate_museum_item", "museum.donated_count+=1", MuseumDonationObservedEffect(), "museum_donation_path_unavailable:" + pathReason));
            return;
        }

        activeMuseumDonation = new ActiveMuseumDonation(
            pending,
            museum,
            actionTile,
            standTile,
            donationTile,
            path,
            maxMovement,
            slot,
            item.QualifiedItemId,
            item.Stack,
            donatedCount,
            Game1.player.achievements.Contains(5),
            Game1.player.mailReceived.Contains("museum60"),
            Game1.MasterPlayer.eventsSeen.Contains("295672"),
            fieldGuideQuest,
            request.PendingRewardIdsBeforeJson);
    }

    private void TickMuseumDonation()
    {
        var active = activeMuseumDonation;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Museum) || active.ElapsedTicks > 3600)
        {
            CompleteMuseumDonationBlocked(active, "museum_donation_world_location_or_timeout");
            return;
        }

        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            var playerTile = Game1.player.TilePoint;
            if (playerTile != active.LastObservedTile)
            {
                active.StuckTicks = 0;
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
                active.LastObservedTile = playerTile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteMuseumDonationBlocked(active, "museum_donation_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_movement_stuck_or_blocked");
                return;
            }
            if (playerTile == next)
            {
                active.PathIndex++;
            }
            return;
        }

        StopAllMovement();
        if (!active.OpenIssued)
        {
            var request = active.Pending.Request;
            var item = active.InventorySlotIndex < Game1.player.Items.Count ? Game1.player.Items[active.InventorySlotIndex] : null;
            if (item?.QualifiedItemId != active.QualifiedItemId || item.Stack != active.StackBefore ||
                active.Museum.museumPieces.Count() != active.DonatedCountBefore ||
                MuseumMutex(active.Museum)?.IsLocked() != false)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_preopen_projection_drifted");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.ActionTile));
            active.Museum.OpenDonationMenu();
            active.OpenIssued = true;
            return;
        }

        if (active.CloseIssued)
        {
            active.SettlementTicks++;
            var settled = Game1.activeClickableMenu is null && MuseumDonationPostconditionsMatch(active);
            if (settled)
            {
                CompleteMuseumDonation(active);
            }
            else if (Game1.activeClickableMenu is not null and not MuseumMenu)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_exit_replaced_by_other_menu");
            }
            else if (active.SettlementTicks > 300)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_settlement_timeout_or_mismatch");
            }
            return;
        }

        if (Game1.activeClickableMenu is not MuseumMenu menu)
        {
            active.OpenWaitTicks++;
            if (Game1.activeClickableMenu is not null || active.OpenWaitTicks > 180)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_menu_open_failed");
            }
            return;
        }

        if (menu.state != MuseumMenu.placingInMuseumState || menu.fadeTimer > 0)
        {
            if (++active.MenuReadyWaitTicks > 240)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_menu_fade_timeout");
            }
            return;
        }

        if (!active.InventoryClickIssued)
        {
            if (active.InventorySlotIndex < 0 || active.InventorySlotIndex >= menu.inventory.inventory.Count)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_inventory_component_missing");
                return;
            }
            var component = menu.inventory.inventory[active.InventorySlotIndex];
            menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
            active.InventoryClickIssued = true;
            if (menu.heldItem?.QualifiedItemId != active.QualifiedItemId || menu.heldItem.Stack != 1)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_inventory_pickup_failed");
            }
            return;
        }

        if (!active.DonationClickIssued)
        {
            var x = (int)Utility.ModifyCoordinateForUIScale(active.DonationTile.X * 64 - Game1.viewport.X + 32);
            var y = (int)Utility.ModifyCoordinateForUIScale(active.DonationTile.Y * 64 - Game1.viewport.Y + 32);
            menu.receiveLeftClick(x, y);
            active.DonationClickIssued = true;
            if (menu.heldItem is not null || active.Museum.museumPieces.Count() != active.DonatedCountBefore + 1)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_display_click_failed");
            }
            return;
        }

        if (!active.CloseIssued)
        {
            if (!menu.readyToClose() || menu.okButton is null)
            {
                if (++active.SettlementTicks > 180)
                {
                    CompleteMuseumDonationBlocked(active, "museum_donation_menu_not_ready_to_close");
                }
                return;
            }
            menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
            if (menu.state != MuseumMenu.exitingState || menu.fadeTimer <= 0)
            {
                CompleteMuseumDonationBlocked(active, "museum_donation_native_close_click_failed");
                return;
            }
            active.CloseIssued = true;
            return;
        }
    }

    private static bool MuseumDonationPostconditionsMatch(ActiveMuseumDonation active)
    {
        var request = active.Pending.Request;
        var item = active.InventorySlotIndex < Game1.player.Items.Count ? Game1.player.Items[active.InventorySlotIndex] : null;
        var stackAfter = item?.QualifiedItemId == active.QualifiedItemId ? item.Stack : 0;
        var reachesThreshold = request.ReachesRustyKeyThreshold == true;
        var rewards = DataLoader.MuseumRewards(Game1.content);
        var pendingAfterMatches = TryProjectRuntimeMuseumRewards(
            active.Museum,
            Game1.player,
            rewards,
            candidateItem: null,
            out var rewardProjection) &&
            request.PendingRewardIdsAfterJson == JsonSerializer.Serialize(rewardProjection.PendingRewardIdsBefore);
        var questCompletedAfter = IsMuseumFieldGuideQuestCompleted(active.FieldGuideQuestBefore);
        var autoRewardsApplied = AutoAppliedMuseumRewardsMatch(request, rewards);
        return active.Museum.museumPieces.Count() == request.ExpectedDonatedCountAfter &&
            stackAfter == request.ExpectedStackAfter &&
            Game1.player.achievements.Contains(5) == request.ExpectedCompleteCollectionAchievementAfter &&
            questCompletedAfter == request.ExpectedFieldGuideQuestCompletedAfter &&
            pendingAfterMatches &&
            autoRewardsApplied &&
            (!reachesThreshold ||
                Game1.player.mailReceived.Contains("museum60") &&
                Game1.MasterPlayer.eventsSeen.Contains("295672"));
    }

    private static bool AutoAppliedMuseumRewardsMatch(
        TrainingExecutionRequest request,
        Dictionary<string, MuseumRewards> rewards)
    {
        string[] ids;
        string[] actions;
        try
        {
            ids = JsonSerializer.Deserialize<string[]>(request.AutoAppliedRewardIdsJson) ?? Array.Empty<string>();
            actions = JsonSerializer.Deserialize<string[]>(request.AutoAppliedRewardActionsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return false;
        }
        if (actions.Any(action => action != "MarkEventSeen Host 295672"))
        {
            return false;
        }
        foreach (var id in ids)
        {
            if (!rewards.TryGetValue(id, out var reward) || reward.RewardItemId is not null ||
                reward.FlagOnCompletion && !Game1.player.mailReceived.Contains(id))
            {
                return false;
            }
        }
        return !actions.Contains("MarkEventSeen Host 295672", StringComparer.Ordinal) ||
            Game1.MasterPlayer.eventsSeen.Contains("295672");
    }

    private void CompleteMuseumDonation(ActiveMuseumDonation active)
    {
        activeMuseumDonation = null;
        StopAllMovement();
        var request = active.Pending.Request;
        var item = active.InventorySlotIndex < Game1.player.Items.Count ? Game1.player.Items[active.InventorySlotIndex] : null;
        var stackAfter = item?.QualifiedItemId == active.QualifiedItemId ? item.Stack : 0;
        var rewards = DataLoader.MuseumRewards(Game1.content);
        TryProjectRuntimeMuseumRewards(active.Museum, Game1.player, rewards, candidateItem: null, out var rewardProjection);
        var pendingRewardIdsAfterJson = JsonSerializer.Serialize(rewardProjection.PendingRewardIdsBefore);
        var fieldGuideQuestCompletedAfter = IsMuseumFieldGuideQuestCompleted(active.FieldGuideQuestBefore);
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
            PrimitiveKind = "donate_museum_item",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "LibraryMuseum.OpenDonationMenu_completed",
                "MuseumMenu.receiveLeftClick_inventory_completed",
                "MuseumMenu.receiveLeftClick_display_completed",
                "Game1.exitActiveMenu_reward_settlement_completed"
            },
            RequestedEffect = "museum.donated_count=" + request.ExpectedDonatedCountAfter,
            ObservedEffect = MuseumDonationObservedEffect(),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 240,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Museum.NameOrUniqueName,
            TargetTileX = active.DonationTile.X,
            TargetTileY = active.DonationTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "world_progress.museum.donated_count", Before = active.DonatedCountBefore.ToString(), After = active.Museum.museumPieces.Count().ToString() },
                new SimulatedFactChange { Path = "player.inventory[" + active.InventorySlotIndex + "].stack", Before = active.StackBefore.ToString(), After = stackAfter.ToString() },
                new SimulatedFactChange { Path = "player.achievements.5", Before = active.AchievementBefore.ToString().ToLowerInvariant(), After = Game1.player.achievements.Contains(5).ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "quests.quest_24.completed", Before = (active.Pending.Request.FieldGuideQuestCompletedBefore == true).ToString().ToLowerInvariant(), After = fieldGuideQuestCompletedAfter.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "world_progress.museum.pending_reward_ids", Before = active.PendingRewardIdsBeforeJson, After = pendingRewardIdsAfterJson },
                new SimulatedFactChange { Path = "quests.mail_received.museum60", Before = active.RewardClaimedBefore.ToString().ToLowerInvariant(), After = Game1.player.mailReceived.Contains("museum60").ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "player.events_seen.295672", Before = active.PrerequisiteEventBefore.ToString().ToLowerInvariant(), After = Game1.MasterPlayer.eventsSeen.Contains("295672").ToString().ToLowerInvariant() }
            }
        });
    }

    private void CompleteMuseumDonationBlocked(ActiveMuseumDonation active, string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is MuseumMenu menu)
        {
            menu.exitThisMenuNoSound();
        }
        else
        {
            var mutex = MuseumMutex(active.Museum);
            if (mutex?.IsLockHeld() == true)
            {
                mutex.ReleaseLock();
            }
        }
        activeMuseumDonation = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "donate_museum_item",
            "museum.donated_count+=1",
            MuseumDonationObservedEffect(),
            reason));
    }

    private static string MuseumDonationObservedEffect()
    {
        var museum = Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum;
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";donated_count=" + (museum?.museumPieces.Count().ToString() ?? "unavailable") +
            ";achievement_5=" + (Game1.player?.achievements.Contains(5).ToString().ToLowerInvariant() ?? "unavailable") +
            ";museum60=" + (Game1.player?.mailReceived.Contains("museum60").ToString().ToLowerInvariant() ?? "unavailable") +
            ";event_295672=" + (Game1.MasterPlayer?.eventsSeen.Contains("295672").ToString().ToLowerInvariant() ?? "unavailable");
    }

    private static NetMutex? MuseumMutex(LibraryMuseum museum)
    {
        return typeof(LibraryMuseum)
            .GetField("mutex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(museum) as NetMutex;
    }

    private static bool IsMuseumFieldGuideQuestCompleted(Quest? quest)
    {
        return quest is not null && quest.completed.Value;
    }

    private static bool TryProjectRuntimeMuseumRewards(
        LibraryMuseum museum,
        Farmer player,
        Dictionary<string, MuseumRewards> rewards,
        Item? candidateItem,
        out RuntimeMuseumRewardProjection projection)
    {
        projection = new RuntimeMuseumRewardProjection();
        try
        {
            var beforeCounts = museum.GetDonatedByContextTag(rewards);
            var afterCounts = beforeCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            if (candidateItem is not null)
            {
                foreach (var tag in afterCounts.Keys.ToArray())
                {
                    if (tag.Length == 0 || ItemContextTagManager.HasBaseTag(candidateItem.ItemId, tag))
                    {
                        afterCounts[tag]++;
                    }
                }
            }
            var pendingBefore = RuntimePendingMuseumRewardIds(museum, player, rewards, beforeCounts);
            var pendingAfter = RuntimePendingMuseumRewardIds(museum, player, rewards, afterCounts);
            var autoRewards = rewards
                .Where(pair => pair.Value.RewardItemId is null)
                .Where(pair => museum.CanCollectReward(pair.Value, pair.Key, player, afterCounts))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            projection = new RuntimeMuseumRewardProjection
            {
                PendingRewardIdsBefore = pendingBefore,
                PendingRewardIdsAfter = pendingAfter,
                NewlyPendingRewardIds = pendingAfter.Except(pendingBefore, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                AutoAppliedRewardIds = autoRewards.Select(pair => pair.Key).ToArray(),
                AutoAppliedRewardActions = autoRewards.SelectMany(pair => pair.Value.RewardActions ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string[] RuntimePendingMuseumRewardIds(
        LibraryMuseum museum,
        Farmer player,
        Dictionary<string, MuseumRewards> rewards,
        Dictionary<string, int> counts)
    {
        return rewards
            .Where(pair => pair.Value.RewardItemId is not null)
            .Where(pair => museum.CanCollectReward(pair.Value, pair.Key, player, counts))
            .Where(pair =>
            {
                var item = ItemRegistry.Create(pair.Value.RewardItemId!, pair.Value.RewardItemCount);
                return !player.mailReceived.Contains(museum.getRewardItemKey(item));
            })
            .Select(pair => pair.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class RuntimeMuseumRewardProjection
    {
        public string[] PendingRewardIdsBefore { get; init; } = Array.Empty<string>();
        public string[] PendingRewardIdsAfter { get; init; } = Array.Empty<string>();
        public string[] NewlyPendingRewardIds { get; init; } = Array.Empty<string>();
        public string[] AutoAppliedRewardIds { get; init; } = Array.Empty<string>();
        public string[] AutoAppliedRewardActions { get; init; } = Array.Empty<string>();
    }
}
