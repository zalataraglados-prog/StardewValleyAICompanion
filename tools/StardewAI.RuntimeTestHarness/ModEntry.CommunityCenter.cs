using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartCommunityCenterDonation(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.CommunityCenterNoteTileX.HasValue || !request.CommunityCenterNoteTileY.HasValue ||
            !request.InventorySlotIndex.HasValue || !request.BundleId.HasValue || !request.BundleAreaId.HasValue || !request.BundleIngredientIndex.HasValue ||
            !request.ExpectedItemQuality.HasValue || !request.RequiredStack.HasValue || !request.ExpectedStackBefore.HasValue || !request.ExpectedStackAfter.HasValue ||
            !request.InventoryItemTotalBefore.HasValue || !request.InventoryItemTotalAfter.HasValue ||
            !request.BundleRequiredSlotCount.HasValue || !request.ExpectedBundleCompletedCountBefore.HasValue ||
            !request.ExpectedBundleCompletedCountAfter.HasValue || !request.ExpectedBundleCompleteAfter.HasValue ||
            !request.ExpectedBundleRewardAvailableAfter.HasValue || !request.ExpectedCompleteBundleCountAfter.HasValue ||
            !request.CompletesArea.HasValue || !request.ExpectedAreaCompleteAfter.HasValue ||
            !request.ExpectedAreaCompletionMailPendingAfter.HasValue || !request.ExpectedBulletinThankYouPendingAfter.HasValue ||
            !request.ExpectedAllAreasCompleteAfter.HasValue || string.IsNullOrWhiteSpace(request.AreaCompletionMailId) ||
            string.IsNullOrWhiteSpace(request.NewlyAppearingNoteAreaIdsJson) ||
            string.IsNullOrWhiteSpace(request.BundleDataKey) || string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_typed_projection_required"));
            return;
        }
        if (activeCommunityCenterDonation is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_player_busy"));
            return;
        }
        if (Game1.currentLocation is not CommunityCenter communityCenter ||
            !string.Equals(communityCenter.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_target_location_mismatch"));
            return;
        }
        if (!Game1.player.hasOrWillReceiveMail("canReadJunimoText"))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_junimo_text_not_readable"));
            return;
        }

        var jojaLocked = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember");
        var ccLocked = Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete") || Game1.MasterPlayer.hasCompletedCommunityCenter();
        var liveRoute = jojaLocked && ccLocked ? "conflicting_irreversible_flags" : jojaLocked ? "joja_locked" : ccLocked ? "community_center_locked" : "undecided";
        if (request.RouteState != liveRoute || liveRoute is not ("undecided" or "community_center_locked"))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_route_state_drifted"));
            return;
        }

        var interactionTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var noteTile = new Point(request.CommunityCenterNoteTileX.Value, request.CommunityCenterNoteTileY.Value);
        var standTile = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (request.BundleAreaId.Value < 0 || request.BundleAreaId.Value >= communityCenter.bundleMutexes.Count ||
            request.BundleAreaId.Value >= communityCenter.areasComplete.Count)
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_area_id_out_of_range"));
            return;
        }
        var liveNoteTile = CommunityCenterNoteTileRuntime(communityCenter, request.BundleAreaId.Value);
        var liveInteractionTile = CommunityCenterInteractionTileRuntime(communityCenter, request.BundleAreaId.Value, liveNoteTile);
        if (liveNoteTile != noteTile || liveInteractionTile != interactionTile || !AreAdjacent(interactionTile, standTile) || !communityCenter.shouldNoteAppearInArea(request.BundleAreaId.Value) ||
            !communityCenter.isJunimoNoteAtArea(request.BundleAreaId.Value) ||
            communityCenter.bundleMutexes[request.BundleAreaId.Value].IsLocked() || !IsTileOnMap(communityCenter, standTile) ||
            !IsTileWalkable(communityCenter, standTile) || IsTileOccupiedByCharacter(communityCenter, standTile))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_note_or_mutex_drifted"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        if (!TryReadLiveCommunityCenterBundle(communityCenter, request, item, out var completedCount, out var projectionFailure))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_bundle_or_inventory_projection_drifted:" + projectionFailure));
            return;
        }
        if (!CommunityCenterOutcomeProjectionMatches(communityCenter, request, out var outcomeFailure))
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_outcome_projection_drifted:" + outcomeFailure));
            return;
        }

        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(communityCenter, Game1.player.TilePoint, standTile, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(CommunityCenterDonationBlocked(request, "community_center_donation_path_unavailable:" + pathReason));
            return;
        }

        activeCommunityCenterDonation = new ActiveCommunityCenterDonation(
            pending, communityCenter, noteTile, interactionTile, standTile, path, maxMovement, slot,
            item!.QualifiedItemId, item.Stack, request.BundleId.Value, request.BundleAreaId.Value,
            request.BundleIngredientIndex.Value, completedCount, request.InventoryItemTotalBefore.Value,
            communityCenter.bundleRewards.TryGetValue(request.BundleId.Value, out var rewardAvailable) && rewardAvailable,
            communityCenter.areasComplete[request.BundleAreaId.Value],
            HasPendingCommunityCenterMail(Game1.player, request.AreaCompletionMailId),
            HasPendingCommunityCenterMail(Game1.player, "ccBulletinThankYou"),
            CommunityCenterCompleteBundleCount(communityCenter),
            communityCenter.areAllAreasComplete());
    }

    private void TickCommunityCenterDonation()
    {
        var active = activeCommunityCenterDonation;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.CommunityCenter) || active.ElapsedTicks > 4200)
        {
            CompleteCommunityCenterDonationBlocked(active, "community_center_donation_world_location_or_timeout");
            return;
        }

        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_path_exhausted");
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
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_movement_stuck_or_blocked");
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
            if (!TryReadLiveCommunityCenterBundle(active.CommunityCenter, request, item, out var completedCount, out var projectionFailure) ||
                completedCount != active.CompletedCountBefore || active.CommunityCenter.bundleMutexes[active.AreaId].IsLocked())
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_preopen_projection_drifted:" + projectionFailure);
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.InteractionTile));
            active.CommunityCenter.checkBundle(active.AreaId);
            active.OpenIssued = true;
            return;
        }

        if (active.ExitIssued)
        {
            active.SettlementTicks++;
            if (Game1.activeClickableMenu is null && !Game1.freezeControls && !Game1.isViewportOnCustomPath() &&
                CommunityCenterDonationPostconditionsMatch(active))
            {
                CompleteCommunityCenterDonation(active);
            }
            else if (active.SettlementTicks > 3600)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_settlement_timeout_or_mismatch");
            }
            return;
        }

        if (Game1.activeClickableMenu is not JunimoNoteMenu menu)
        {
            if (Game1.activeClickableMenu is not null || ++active.OpenWaitTicks > 240)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_menu_open_failed");
            }
            return;
        }
        if (menu.whichArea != active.AreaId)
        {
            CompleteCommunityCenterDonationBlocked(active, "community_center_donation_area_menu_drifted");
            return;
        }

        if (!active.BundleClickIssued)
        {
            var bundle = menu.bundles.FirstOrDefault(row => row.bundleIndex == active.BundleId);
            if (bundle is null || !bundle.canBeClicked() || !JunimoNoteMenu.canClick)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_bundle_button_unavailable");
                return;
            }
            menu.receiveLeftClick(bundle.bounds.Center.X, bundle.bounds.Center.Y);
            active.BundleClickIssued = true;
            if (!menu.specificBundlePage || menu.currentPageBundle?.bundleIndex != active.BundleId)
            {
                CompleteCommunityCenterDonationBlocked(active, "community_center_donation_bundle_click_had_no_immediate_effect");
                return;
            }
            active.BundlePageObservedOpen = true;
        }

        if (!menu.specificBundlePage || menu.currentPageBundle?.bundleIndex != active.BundleId)
        {
            if (active.IngredientClickIssued && active.RemainderReturnClickIssued)
            {
                active.BackClickIssued = true;
            }
            else
            {
                CompleteCommunityCenterDonationBlocked(
                    active,
                    "community_center_donation_bundle_page_open_failed:specific=" + menu.specificBundlePage +
                    ":current=" + (menu.currentPageBundle?.bundleIndex.ToString() ?? "none") +
                    ":can_click=" + JunimoNoteMenu.canClick +
                    ":scrambled=" + menu.scrambledText +
                    ":complete=" + (menu.currentPageBundle?.complete.ToString() ?? "unavailable") +
                    ":completion_timer=" + (menu.currentPageBundle?.completionTimer.ToString() ?? "unavailable") +
                    ":previously_open=" + active.BundlePageObservedOpen +
                    ":contains=" + (menu.bundles.FirstOrDefault(row => row.bundleIndex == active.BundleId)?.containsPoint(
                        menu.bundles.First(row => row.bundleIndex == active.BundleId).bounds.Center.X,
                        menu.bundles.First(row => row.bundleIndex == active.BundleId).bounds.Center.Y).ToString() ?? "unavailable"));
                return;
            }
        }
        if (!active.BackClickIssued)
        {
            var requestNow = active.Pending.Request;
            var pageBundle = menu.currentPageBundle!;
            if (!active.InventoryClickIssued)
            {
                var item = active.InventorySlotIndex < Game1.player.Items.Count ? Game1.player.Items[active.InventorySlotIndex] : null;
                if (item is null || pageBundle.GetBundleIngredientDescriptionIndexForItem(item) != active.IngredientIndex ||
                    active.InventorySlotIndex >= menu.inventory.inventory.Count)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_candidate_drifted");
                    return;
                }
                var component = menu.inventory.inventory[active.InventorySlotIndex];
                menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
                active.InventoryClickIssued = true;
                if (menu.heldItem?.QualifiedItemId != active.QualifiedItemId || menu.heldItem.Stack != active.StackBefore)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_inventory_pickup_failed");
                    return;
                }
            }

            if (!active.IngredientClickIssued)
            {
                var ingredientSlot = menu.ingredientSlots.FirstOrDefault(row => row.item is null && pageBundle.canAcceptThisItem(menu.heldItem, row));
                if (ingredientSlot is null)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_ingredient_slot_unavailable");
                    return;
                }
                menu.receiveLeftClick(ingredientSlot.bounds.Center.X, ingredientSlot.bounds.Center.Y);
                active.IngredientClickIssued = true;
                if (!CommunityCenterDonationBitsMatch(active) ||
                    CommunityCenterDonationOwnedItemTotal(active, menu.heldItem) != requestNow.InventoryItemTotalAfter)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_native_ingredient_click_failed");
                    return;
                }
            }

            if (!active.RemainderReturnClickIssued)
            {
                if (menu.heldItem is not null)
                {
                    if (menu.heldItem.QualifiedItemId != active.QualifiedItemId || menu.heldItem.Stack != requestNow.ExpectedStackAfter ||
                        active.InventorySlotIndex >= menu.inventory.inventory.Count)
                    {
                        CompleteCommunityCenterDonationBlocked(active, "community_center_donation_remainder_drifted");
                        return;
                    }
                    var component = menu.inventory.inventory[active.InventorySlotIndex];
                    menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
                }
                active.RemainderReturnClickIssued = true;
                if (menu.heldItem is not null || CommunityCenterDonationInventoryItemTotal(active) != requestNow.InventoryItemTotalAfter ||
                    requestNow.ExpectedBundleCompleteAfter != true && CommunityCenterDonationInventoryStack(active) != requestNow.ExpectedStackAfter)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_remainder_return_failed");
                    return;
                }
            }

            if (menu.specificBundlePage)
            {
                if (!menu.isReadyToCloseMenuOrBundle())
                {
                    if (++active.SettlementTicks > 900)
                    {
                        CompleteCommunityCenterDonationBlocked(active, "community_center_donation_bundle_animation_timeout");
                    }
                    return;
                }
                if (!active.BackClickIssued)
                {
                    menu.receiveLeftClick(menu.backButton.bounds.Center.X, menu.backButton.bounds.Center.Y);
                    active.BackClickIssued = true;
                }
                return;
            }
        }

        if (!active.ExitIssued)
        {
            if (!menu.isReadyToCloseMenuOrBundle())
            {
                if (++active.SettlementTicks > 1020)
                {
                    CompleteCommunityCenterDonationBlocked(active, "community_center_donation_menu_not_ready_to_exit");
                }
                return;
            }
            menu.exitThisMenu();
            active.ExitIssued = true;
            return;
        }

    }

    private static bool TryReadLiveCommunityCenterBundle(
        CommunityCenter communityCenter,
        TrainingExecutionRequest request,
        Item? item,
        out int completedCount,
        out string failure)
    {
        completedCount = 0;
        failure = string.Empty;
        if (!request.BundleId.HasValue || !request.BundleAreaId.HasValue || !request.BundleIngredientIndex.HasValue ||
            !request.BundleRequiredSlotCount.HasValue || !request.ExpectedBundleCompletedCountBefore.HasValue ||
            string.IsNullOrWhiteSpace(request.BundleDataKey) || !Game1.netWorldState.Value.BundleData.TryGetValue(request.BundleDataKey, out var raw) ||
            !communityCenter.bundles.TryGetValue(request.BundleId.Value, out var bits))
        {
            failure = "bundle_request_or_live_row_missing";
            return false;
        }
        var keyParts = request.BundleDataKey.Split('/');
        var fields = raw.Split('/');
        if (keyParts.Length < 2 || fields.Length < Bundle.FieldCount || keyParts[0] != request.BundleAreaName ||
            !int.TryParse(keyParts[1], out var keyBundleId) || keyBundleId != request.BundleId.Value ||
            CommunityCenter.getAreaNumberFromName(keyParts[0]) != request.BundleAreaId.Value)
        {
            failure = "bundle_data_key_or_area_mismatch";
            return false;
        }
        var parts = ArgUtility.SplitBySpace(fields[Bundle.IngredientsIndex]);
        if (parts.Length % 3 != 0 || bits.Length < parts.Length / 3)
        {
            failure = "bundle_ingredient_shape_mismatch";
            return false;
        }
        var ingredients = new List<BundleIngredientDescription>();
        for (var index = 0; index < parts.Length / 3; index++)
        {
            if (!int.TryParse(parts[index * 3 + 1], out var stack) || stack < 1 ||
                !int.TryParse(parts[index * 3 + 2], out var quality) || quality < 0)
            {
                failure = "bundle_ingredient_value_invalid";
                return false;
            }
            ingredients.Add(new BundleIngredientDescription(parts[index * 3], stack, quality, bits[index]));
        }
        completedCount = ingredients.Count(row => row.completed);
        var requiredSlots = ArgUtility.GetInt(fields, Bundle.NumberOfSlotsIndex, ingredients.Count);
        var matcher = new Bundle(fields[Bundle.NameIndex], fields[Bundle.DisplayNameIndex], ingredients, bits, fields[Bundle.RewardIndex]);
        var selectedIndex = item is null ? -1 : matcher.GetBundleIngredientDescriptionIndexForItem(item);
        if (item is null || item.QualifiedItemId != request.QualifiedItemId || item.ItemId != request.ItemId ||
            item.GetType().FullName != request.TargetRuntimeType || item.Quality != request.ExpectedItemQuality)
        {
            failure = "inventory_item_identity_quality_mismatch";
            return false;
        }
        if (item.Stack != request.ExpectedStackBefore || request.ExpectedStackAfter != item.Stack - request.RequiredStack ||
            CommunityCenterDonationInventoryItemTotal(request.QualifiedItemId) != request.InventoryItemTotalBefore ||
            request.InventoryItemTotalAfter != request.InventoryItemTotalBefore - request.RequiredStack)
        {
            failure = "inventory_stack_or_total_mismatch";
            return false;
        }
        if (requiredSlots != request.BundleRequiredSlotCount || completedCount != request.ExpectedBundleCompletedCountBefore)
        {
            failure = "bundle_required_or_completed_count_mismatch";
            return false;
        }
        if (selectedIndex != request.BundleIngredientIndex || selectedIndex < 0 || selectedIndex >= ingredients.Count ||
            ingredients[selectedIndex].stack != request.RequiredStack || ingredients[selectedIndex].quality > item.Quality)
        {
            failure = "bundle_native_matcher_or_requirement_mismatch";
            return false;
        }
        var completes = completedCount + 1 >= requiredSlots;
        var expectedAfter = completes ? ingredients.Count : completedCount + 1;
        if (request.ExpectedBundleCompleteAfter != completes || request.ExpectedBundleCompletedCountAfter != expectedAfter)
        {
            failure = "bundle_completion_projection_mismatch:completed=" + completedCount +
                ":required=" + requiredSlots +
                ":expected_complete=" + request.ExpectedBundleCompleteAfter +
                ":actual_complete=" + completes +
                ":expected_count=" + request.ExpectedBundleCompletedCountAfter +
                ":actual_count=" + expectedAfter;
            return false;
        }
        return true;
    }

    private static bool CommunityCenterDonationBitsMatch(ActiveCommunityCenterDonation active)
    {
        var request = active.Pending.Request;
        if (!active.CommunityCenter.bundles.TryGetValue(active.BundleId, out var bits) || active.IngredientIndex >= bits.Length)
        {
            return false;
        }
        var rewardSettled = active.CommunityCenter.bundleRewards.TryGetValue(active.BundleId, out var rewardAvailable) && rewardAvailable;
        var ingredientCount = CommunityCenterBundleIngredientCount(request);
        return ingredientCount > 0 && bits[active.IngredientIndex] && bits.Take(ingredientCount).Count(value => value) == request.ExpectedBundleCompletedCountAfter &&
            (!request.ExpectedBundleCompleteAfter.GetValueOrDefault() || rewardSettled);
    }

    private static int CommunityCenterBundleIngredientCount(TrainingExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BundleDataKey) ||
            !Game1.netWorldState.Value.BundleData.TryGetValue(request.BundleDataKey, out var raw))
        {
            return 0;
        }
        var fields = raw.Split('/');
        return fields.Length < Bundle.FieldCount ? 0 : ArgUtility.SplitBySpace(fields[Bundle.IngredientsIndex]).Length / 3;
    }

    private static int CommunityCenterDonationInventoryStack(ActiveCommunityCenterDonation active)
    {
        var item = active.InventorySlotIndex < Game1.player.Items.Count ? Game1.player.Items[active.InventorySlotIndex] : null;
        return item?.QualifiedItemId == active.QualifiedItemId ? item.Stack : 0;
    }

    private static int CommunityCenterDonationInventoryItemTotal(ActiveCommunityCenterDonation active) =>
        CommunityCenterDonationInventoryItemTotal(active.QualifiedItemId);

    private static int CommunityCenterDonationInventoryItemTotal(string qualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item?.QualifiedItemId == qualifiedItemId)
            .Sum(item => item?.Stack ?? 0);
    }

    private static int CommunityCenterDonationOwnedItemTotal(ActiveCommunityCenterDonation active, Item? heldItem)
    {
        return CommunityCenterDonationInventoryItemTotal(active) +
            (heldItem?.QualifiedItemId == active.QualifiedItemId ? heldItem.Stack : 0);
    }

    private static bool CommunityCenterDonationPostconditionsMatch(ActiveCommunityCenterDonation active)
    {
        var request = active.Pending.Request;
        int[] newlyAppearingAreas;
        try
        {
            newlyAppearingAreas = JsonSerializer.Deserialize<int[]>(request.NewlyAppearingNoteAreaIdsJson) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            return false;
        }
        return CommunityCenterDonationBitsMatch(active) &&
            CommunityCenterDonationInventoryItemTotal(active) == request.InventoryItemTotalAfter &&
            active.CommunityCenter.bundleRewards.TryGetValue(active.BundleId, out var rewardAvailable) &&
            rewardAvailable == request.ExpectedBundleRewardAvailableAfter &&
            CommunityCenterCompleteBundleCount(active.CommunityCenter) == request.ExpectedCompleteBundleCountAfter &&
            active.CommunityCenter.areasComplete[active.AreaId] == request.ExpectedAreaCompleteAfter &&
            HasPendingCommunityCenterMail(Game1.player, request.AreaCompletionMailId) == request.ExpectedAreaCompletionMailPendingAfter &&
            HasPendingCommunityCenterMail(Game1.player, "ccBulletinThankYou") == request.ExpectedBulletinThankYouPendingAfter &&
            active.CommunityCenter.areAllAreasComplete() == request.ExpectedAllAreasCompleteAfter &&
            newlyAppearingAreas.All(active.CommunityCenter.isJunimoNoteAtArea) &&
            !active.CommunityCenter.bundleMutexes[active.AreaId].IsLocked();
    }

    private void CompleteCommunityCenterDonation(ActiveCommunityCenterDonation active)
    {
        activeCommunityCenterDonation = null;
        StopAllMovement();
        var request = active.Pending.Request;
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
            PrimitiveKind = "donate_community_center_item",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "CommunityCenter.checkBundle_completed",
                "JunimoNoteMenu.receiveLeftClick_bundle_completed",
                "JunimoNoteMenu.receiveLeftClick_inventory_completed",
                "JunimoNoteMenu.receiveLeftClick_ingredient_slot_completed",
                "JunimoNoteMenu.exitThisMenu_completed"
            },
            RequestedEffect = "community_center.bundle=" + active.BundleId + ":ingredient=" + active.IngredientIndex + ":completed=true",
            ObservedEffect = CommunityCenterDonationObservedEffect(active),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 300,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.CommunityCenter.NameOrUniqueName,
            TargetTileX = active.InteractionTile.X,
            TargetTileY = active.InteractionTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "world_progress.community_center.bundle_rows[" + active.BundleId + "].ingredients[" + active.IngredientIndex + "].completed", Before = "false", After = "true" },
                new SimulatedFactChange { Path = "world_progress.community_center.bundle_rows[" + active.BundleId + "].completed_ingredient_count", Before = active.CompletedCountBefore.ToString(), After = request.ExpectedBundleCompletedCountAfter?.ToString() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.bundle_rewards[" + active.BundleId + "]", Before = active.RewardAvailableBefore.ToString().ToLowerInvariant(), After = request.ExpectedBundleRewardAvailableAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.complete_bundle_count", Before = active.CompleteBundleCountBefore.ToString(), After = request.ExpectedCompleteBundleCountAfter?.ToString() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.areas_complete[" + active.AreaId + "]", Before = active.AreaCompleteBefore.ToString().ToLowerInvariant(), After = request.ExpectedAreaCompleteAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "player.mail_for_tomorrow." + request.AreaCompletionMailId, Before = active.AreaMailPendingBefore.ToString().ToLowerInvariant(), After = request.ExpectedAreaCompletionMailPendingAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "player.mail_for_tomorrow.ccBulletinThankYou", Before = active.BulletinThankYouPendingBefore.ToString().ToLowerInvariant(), After = request.ExpectedBulletinThankYouPendingAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.all_areas_complete", Before = active.AllAreasCompleteBefore.ToString().ToLowerInvariant(), After = request.ExpectedAllAreasCompleteAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "player.inventory.qualified_item_total[" + active.QualifiedItemId + "]", Before = active.InventoryItemTotalBefore.ToString(), After = CommunityCenterDonationInventoryItemTotal(active).ToString() }
            }
        });
    }

    private void CompleteCommunityCenterDonationBlocked(ActiveCommunityCenterDonation active, string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is JunimoNoteMenu menu)
        {
            if (menu.heldItem is not null && active.InventorySlotIndex >= 0 && active.InventorySlotIndex < menu.inventory.inventory.Count)
            {
                var component = menu.inventory.inventory[active.InventorySlotIndex];
                menu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
            }
            if (menu.heldItem is null)
            {
                menu.exitThisMenu();
            }
        }
        activeCommunityCenterDonation = null;
        active.Pending.Completion.SetResult(CommunityCenterDonationBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult CommunityCenterDonationBlocked(TrainingExecutionRequest request, string reason)
    {
        return BlockedWithPrimitive(
            request,
            "donate_community_center_item",
            "community_center.bundle_ingredient.completed=true",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"),
            reason);
    }

    private static string CommunityCenterDonationObservedEffect(ActiveCommunityCenterDonation active)
    {
        var bits = active.CommunityCenter.bundles.TryGetValue(active.BundleId, out var value) ? value : Array.Empty<bool>();
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";bundle=" + active.BundleId +
            ";completed_count=" + bits.Count(row => row) +
            ";ingredient_completed=" + (active.IngredientIndex < bits.Length && bits[active.IngredientIndex]).ToString().ToLowerInvariant() +
            ";inventory_item_total=" + CommunityCenterDonationInventoryItemTotal(active);
    }

    private static Point? CommunityCenterNoteTileRuntime(CommunityCenter communityCenter, int areaId)
    {
        var method = typeof(CommunityCenter).GetMethod("getNotePosition", BindingFlags.Instance | BindingFlags.NonPublic);
        return method?.Invoke(communityCenter, new object[] { areaId }) is Point point && point != Point.Zero ? point : null;
    }

    private static Point? CommunityCenterInteractionTileRuntime(CommunityCenter communityCenter, int areaId, Point? noteTile)
    {
        if (areaId != 5)
        {
            return noteTile;
        }
        var buildings = communityCenter.Map?.GetLayer("Buildings");
        if (buildings is null)
        {
            return null;
        }
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                if (communityCenter.getTileIndexAt(x, y, "Buildings") == 1799)
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }
}
