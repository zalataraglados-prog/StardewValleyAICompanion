using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string CommunityCenterRewardNoteMode = "junimo_note_present_button";
    private const string CommunityCenterRewardChestMode = "missed_rewards_chest";

    private void StartCommunityCenterRewardClaim(PendingExecution pending)
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
            !request.BundleId.HasValue || !request.BundleAreaId.HasValue ||
            !request.ExpectedItemQuality.HasValue || !request.RequiredStack.HasValue ||
            !request.InventoryItemTotalBefore.HasValue || !request.InventoryItemTotalAfter.HasValue ||
            request.ExpectedBundleRewardAvailableAfter != false ||
            string.IsNullOrWhiteSpace(request.BundleDataKey) ||
            string.IsNullOrWhiteSpace(request.BundleAreaName) ||
            string.IsNullOrWhiteSpace(request.ItemId) ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
            request.RewardClaimMode is not (CommunityCenterRewardNoteMode or CommunityCenterRewardChestMode))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_typed_projection_required"));
            return;
        }
        if (activeCommunityCenterRewardClaim is not null ||
            activeCommunityCenterDonation is not null ||
            activeCommunityCenterVaultPayment is not null ||
            activeCommunityCenterFirstNote is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_player_busy"));
            return;
        }
        if (Game1.currentLocation is not CommunityCenter communityCenter ||
            !string.Equals(
                communityCenter.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_target_location_mismatch"));
            return;
        }

        var jojaLocked = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember");
        var ccLocked = Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete") ||
            Game1.MasterPlayer.hasCompletedCommunityCenter();
        var liveRoute = jojaLocked && ccLocked
            ? "conflicting_irreversible_flags"
            : jojaLocked
                ? "joja_locked"
                : ccLocked
                    ? "community_center_locked"
                    : "undecided";
        if (request.RouteState != liveRoute ||
            liveRoute is not ("undecided" or "community_center_locked"))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_route_state_drifted"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!AreAdjacent(target, stand) || !IsTileOnMap(communityCenter, stand) ||
            !IsTileWalkable(communityCenter, stand) ||
            IsTileOccupiedByCharacter(communityCenter, stand))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_endpoint_or_stand_drifted"));
            return;
        }
        if (!CommunityCenterRewardEndpointMatches(
                communityCenter,
                request,
                target,
                out var endpointFailure))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_endpoint_drifted:" + endpointFailure));
            return;
        }
        if (!TryReadLiveCommunityCenterReward(
                communityCenter,
                request,
                out var reward,
                out var rewardFailure))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_projection_drifted:" + rewardFailure));
            return;
        }
        if (!Game1.player.couldInventoryAcceptThisItem(reward))
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_inventory_unavailable"));
            return;
        }

        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            communityCenter,
            Game1.player.TilePoint,
            stand,
            maxMovement,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(CommunityCenterRewardBlocked(
                request,
                "community_center_bundle_reward_path_unavailable:" + pathReason));
            return;
        }

        activeCommunityCenterRewardClaim = new ActiveCommunityCenterRewardClaim(
            pending,
            communityCenter,
            target,
            stand,
            path,
            maxMovement,
            request.BundleId.Value,
            request.BundleAreaId.Value,
            request.RewardClaimMode,
            request.QualifiedItemId,
            request.InventoryItemTotalBefore.Value);
    }

    private void TickCommunityCenterRewardClaim()
    {
        var active = activeCommunityCenterRewardClaim;
        if (active is null)
            return;

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.CommunityCenter) ||
            active.ElapsedTicks > 1800)
        {
            CompleteCommunityCenterRewardBlocked(
                active,
                "community_center_bundle_reward_world_location_or_timeout");
            return;
        }

        if (!active.EntryIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteCommunityCenterRewardBlocked(
                    active,
                    "community_center_bundle_reward_path_exhausted");
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
                    CompleteCommunityCenterRewardBlocked(
                        active,
                        "community_center_bundle_reward_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteCommunityCenterRewardBlocked(
                    active,
                    "community_center_bundle_reward_movement_stuck_or_blocked");
                return;
            }
            if (playerTile == next)
                active.PathIndex++;
            return;
        }

        StopAllMovement();
        if (!active.EntryIssued)
        {
            var request = active.Pending.Request;
            var endpointMatches = CommunityCenterRewardEndpointMatches(
                active.CommunityCenter,
                request,
                active.InteractionTile,
                out var endpointFailure);
            var rewardMatches = TryReadLiveCommunityCenterReward(
                active.CommunityCenter,
                request,
                out _,
                out var rewardFailure);
            if (!endpointMatches || !rewardMatches)
            {
                CompleteCommunityCenterRewardBlocked(
                    active,
                    "community_center_bundle_reward_preopen_drifted:" +
                    endpointFailure + ":" + rewardFailure);
                return;
            }
            Game1.player.faceDirection(DirectionTo(
                Game1.player.TilePoint,
                active.InteractionTile));
            if (active.ClaimMode == CommunityCenterRewardNoteMode)
            {
                active.CommunityCenter.checkBundle(active.AreaId);
            }
            else
            {
                active.CommunityCenter.performAction(
                    new[] { "MissedRewards" },
                    Game1.player,
                    new xTile.Dimensions.Location(
                        active.InteractionTile.X,
                        active.InteractionTile.Y));
            }
            active.EntryIssued = true;
            return;
        }

        if (Game1.activeClickableMenu is JunimoNoteMenu noteMenu)
        {
            if (active.ClaimMode != CommunityCenterRewardNoteMode ||
                noteMenu.whichArea != active.AreaId)
            {
                CompleteCommunityCenterRewardBlocked(
                    active,
                    "community_center_bundle_reward_note_menu_drifted");
                return;
            }
            if (!active.PresentButtonClicked)
            {
                if (noteMenu.presentButton is null || !JunimoNoteMenu.canClick)
                {
                    if (++active.MenuWaitTicks > 240)
                    {
                        CompleteCommunityCenterRewardBlocked(
                            active,
                            "community_center_bundle_reward_present_button_unavailable");
                    }
                    return;
                }
                noteMenu.receiveLeftClick(
                    noteMenu.presentButton.bounds.Center.X,
                    noteMenu.presentButton.bounds.Center.Y);
                active.PresentButtonClicked = true;
                active.MenuWaitTicks = 0;
                return;
            }
            if (active.RewardMenuExitIssued && !active.ParentExitIssued)
            {
                noteMenu.exitThisMenu();
                active.ParentExitIssued = true;
                return;
            }
            CompleteCommunityCenterRewardBlocked(
                active,
                "community_center_bundle_reward_item_menu_open_failed");
            return;
        }

        if (Game1.activeClickableMenu is ItemGrabMenu rewardMenu)
        {
            if (!active.RewardClicked)
            {
                var matches = rewardMenu.ItemsToGrabMenu.actualInventory
                    .Select((item, index) => new { item, index })
                    .Where(entry => entry.item is not null &&
                                    entry.item.SpecialVariable == active.BundleId &&
                                    CommunityCenterRewardItemMatches(
                                        entry.item,
                                        active.Pending.Request))
                    .ToArray();
                if (matches.Length != 1 ||
                    matches[0].index < 0 ||
                    matches[0].index >= rewardMenu.ItemsToGrabMenu.inventory.Count)
                {
                    CompleteCommunityCenterRewardBlocked(
                        active,
                        "community_center_bundle_reward_item_slot_drifted");
                    return;
                }
                var component = rewardMenu.ItemsToGrabMenu.inventory[matches[0].index];
                rewardMenu.receiveLeftClick(
                    component.bounds.Center.X,
                    component.bounds.Center.Y);
                active.RewardClicked = true;
                if (rewardMenu.heldItem is not null ||
                    !CommunityCenterRewardPostconditionsMatch(active))
                {
                    CompleteCommunityCenterRewardBlocked(
                        active,
                        "community_center_bundle_reward_native_click_failed");
                    return;
                }
                rewardMenu.exitThisMenu();
                active.RewardMenuExitIssued = true;
                return;
            }
            CompleteCommunityCenterRewardBlocked(
                active,
                "community_center_bundle_reward_menu_did_not_exit");
            return;
        }

        if (Game1.activeClickableMenu is not null)
        {
            CompleteCommunityCenterRewardBlocked(
                active,
                "community_center_bundle_reward_unexpected_menu");
            return;
        }

        if (!active.RewardClicked)
        {
            if (++active.MenuWaitTicks > 240)
            {
                CompleteCommunityCenterRewardBlocked(
                    active,
                    "community_center_bundle_reward_entry_menu_open_failed");
            }
            return;
        }

        active.SettlementTicks++;
        var mutexReleased = active.ClaimMode == CommunityCenterRewardChestMode
            ? !active.CommunityCenter.missedRewardsChest.Value.mutex.IsLocked()
            : !active.CommunityCenter.bundleMutexes[active.AreaId].IsLocked();
        if (mutexReleased && CommunityCenterRewardPostconditionsMatch(active))
        {
            CompleteCommunityCenterReward(active);
        }
        else if (active.SettlementTicks > 240)
        {
            CompleteCommunityCenterRewardBlocked(
                active,
                "community_center_bundle_reward_native_settlement_timeout_or_mismatch");
        }
    }

    private static bool CommunityCenterRewardEndpointMatches(
        CommunityCenter communityCenter,
        TrainingExecutionRequest request,
        Point target,
        out string failure)
    {
        failure = string.Empty;
        if (!request.BundleAreaId.HasValue || request.BundleAreaId.Value < 0 ||
            request.BundleAreaId.Value >= communityCenter.bundleMutexes.Count ||
            !request.BundleId.HasValue)
        {
            failure = "bundle_or_area_id_invalid";
            return false;
        }
        if (request.RewardClaimMode == CommunityCenterRewardNoteMode)
        {
            var noteTile = CommunityCenterNoteTileRuntime(
                communityCenter,
                request.BundleAreaId.Value);
            var interaction = CommunityCenterInteractionTileRuntime(
                communityCenter,
                request.BundleAreaId.Value,
                noteTile);
            if (!Game1.player.hasOrWillReceiveMail("canReadJunimoText") ||
                interaction != target ||
                !communityCenter.shouldNoteAppearInArea(request.BundleAreaId.Value) ||
                !communityCenter.isJunimoNoteAtArea(request.BundleAreaId.Value) ||
                communityCenter.bundleMutexes[request.BundleAreaId.Value].IsLocked())
            {
                failure = "junimo_note_endpoint_unavailable";
                return false;
            }
            return true;
        }
        if (request.RewardClaimMode != CommunityCenterRewardChestMode ||
            !communityCenter.missedRewardsChestVisible.Value ||
            communityCenter.missedRewardsChest.Value.mutex.IsLocked() ||
            !string.Equals(
                communityCenter.doesTileHaveProperty(
                    target.X,
                    target.Y,
                    "Action",
                    "Buildings"),
                "MissedRewards",
                StringComparison.Ordinal))
        {
            failure = "missed_rewards_endpoint_unavailable";
            return false;
        }
        return true;
    }

    private static bool TryReadLiveCommunityCenterReward(
        CommunityCenter communityCenter,
        TrainingExecutionRequest request,
        out Item reward,
        out string failure)
    {
        reward = null!;
        failure = string.Empty;
        if (!request.BundleId.HasValue || !request.BundleAreaId.HasValue ||
            !Game1.netWorldState.Value.BundleData.TryGetValue(
                request.BundleDataKey,
                out var raw))
        {
            failure = "bundle_data_missing";
            return false;
        }
        var key = request.BundleDataKey.Split('/');
        var fields = raw.Split('/');
        if (key.Length < 2 || !int.TryParse(key[1], out var bundleId) ||
            bundleId != request.BundleId.Value ||
            key[0] != request.BundleAreaName ||
            CommunityCenter.getAreaNumberFromName(key[0]) != request.BundleAreaId.Value ||
            fields.Length <= Bundle.RewardIndex ||
            !communityCenter.bundleRewards.TryGetValue(bundleId, out var available) ||
            !available)
        {
            failure = "bundle_identity_or_reward_flag_drifted";
            return false;
        }
        var rewards = new List<Item>();
        JunimoNoteMenu.GetBundleRewards(request.BundleAreaId.Value, rewards);
        var matches = rewards.Where(item =>
            item.SpecialVariable == bundleId &&
            CommunityCenterRewardItemMatches(item, request)).ToArray();
        if (matches.Length != 1 ||
            CommunityCenterRewardInventoryTotal(request.QualifiedItemId) !=
                request.InventoryItemTotalBefore ||
            request.InventoryItemTotalAfter !=
                request.InventoryItemTotalBefore + request.RequiredStack)
        {
            failure = "reward_item_or_inventory_projection_drifted";
            return false;
        }
        reward = matches[0];
        return true;
    }

    private static bool CommunityCenterRewardItemMatches(
        Item item,
        TrainingExecutionRequest request) =>
        item.ItemId == request.ItemId &&
        item.QualifiedItemId == request.QualifiedItemId &&
        (item.GetType().FullName ?? string.Empty) == request.TargetRuntimeType &&
        item.Quality == request.ExpectedItemQuality &&
        item.Stack == request.RequiredStack &&
        !item.IsRecipe &&
        item.QualifiedItemId is not ("(O)102" or "(O)326" or "(O)434");

    private static int CommunityCenterRewardInventoryTotal(string qualifiedItemId) =>
        Game1.player.Items
            .Where(item => item?.QualifiedItemId == qualifiedItemId)
            .Sum(item => item?.Stack ?? 0);

    private static bool CommunityCenterRewardPostconditionsMatch(
        ActiveCommunityCenterRewardClaim active)
    {
        var request = active.Pending.Request;
        return active.CommunityCenter.bundleRewards.TryGetValue(
                   active.BundleId,
                   out var available) &&
               !available &&
               CommunityCenterRewardInventoryTotal(active.QualifiedItemId) ==
                   request.InventoryItemTotalAfter;
    }

    private void CompleteCommunityCenterReward(
        ActiveCommunityCenterRewardClaim active)
    {
        activeCommunityCenterRewardClaim = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            SchemaVersion = "training_execution_result.v1",
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "claim_community_center_bundle_reward",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                active.ClaimMode == CommunityCenterRewardNoteMode
                    ? "CommunityCenter.checkBundle_and_JunimoNoteMenu.presentButton_completed"
                    : "CommunityCenter.MissedRewards_action_completed",
                "ItemGrabMenu.receiveLeftClick_exact_bundle_reward_completed",
                "native_rewardGrabbed_cleared_bundle_reward_flag",
                "native_inventory_receipt_verified"
            },
            RequestedEffect = "community_center.bundle=" + active.BundleId +
                ":reward_available=false;inventory_item_total=" +
                request.InventoryItemTotalAfter,
            ObservedEffect = "bundle=" + active.BundleId +
                ";reward_available=false;qualified_item_id=" + active.QualifiedItemId +
                ";inventory_item_total=" +
                CommunityCenterRewardInventoryTotal(active.QualifiedItemId),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 180,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.CommunityCenter.NameOrUniqueName,
            TargetTileX = active.InteractionTile.X,
            TargetTileY = active.InteractionTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "world_progress.community_center.bundle_rewards[" + active.BundleId + "]",
                    Before = "true",
                    After = "false"
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory.qualified_item_total[" + active.QualifiedItemId + "]",
                    Before = active.InventoryItemTotalBefore.ToString(),
                    After = CommunityCenterRewardInventoryTotal(active.QualifiedItemId).ToString()
                }
            }
        });
    }

    private void CompleteCommunityCenterRewardBlocked(
        ActiveCommunityCenterRewardClaim active,
        string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is ItemGrabMenu itemGrabMenu &&
            itemGrabMenu.heldItem is null)
        {
            itemGrabMenu.exitThisMenu();
        }
        if (Game1.activeClickableMenu is JunimoNoteMenu noteMenu)
        {
            noteMenu.exitThisMenu();
        }
        activeCommunityCenterRewardClaim = null;
        active.Pending.Completion.SetResult(
            CommunityCenterRewardBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult CommunityCenterRewardBlocked(
        TrainingExecutionRequest request,
        string reason) =>
        BlockedWithPrimitive(
            request,
            "claim_community_center_bundle_reward",
            "community_center.bundle_reward_available=false;inventory_receipt=exact",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"),
            reason);
}
