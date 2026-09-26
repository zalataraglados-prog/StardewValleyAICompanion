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
    private const int CommunityCenterVaultAreaId = 4;

    private void StartCommunityCenterVaultPayment(PendingExecution pending)
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
            !request.CommunityCenterNoteTileX.HasValue || !request.CommunityCenterNoteTileY.HasValue ||
            !request.BundleId.HasValue || request.BundleAreaId != CommunityCenterVaultAreaId ||
            request.BundleIngredientIndex != 0 || request.BundleRequiredSlotCount != 1 ||
            !request.Price.HasValue || request.Price.Value < 1 ||
            !request.ExpectedMoneyBefore.HasValue || !request.ExpectedMoneyAfter.HasValue ||
            request.ExpectedMoneyAfter != request.ExpectedMoneyBefore - request.Price ||
            request.ExpectedBundleCompletedCountBefore != 0 ||
            request.ExpectedBundleCompletedCountAfter != 1 ||
            request.ExpectedBundleCompleteAfter != true ||
            request.ExpectedBundleRewardAvailableAfter != true ||
            !request.ExpectedCompleteBundleCountAfter.HasValue ||
            !request.CompletesArea.HasValue || !request.ExpectedAreaCompleteAfter.HasValue ||
            !request.ExpectedAreaCompletionMailPendingAfter.HasValue ||
            !request.ExpectedBulletinThankYouPendingAfter.HasValue ||
            !request.ExpectedAllAreasCompleteAfter.HasValue ||
            string.IsNullOrWhiteSpace(request.NewlyAppearingNoteAreaIdsJson) ||
            string.IsNullOrWhiteSpace(request.BundleDataKey) ||
            request.BundleAreaName != "Vault" ||
            string.IsNullOrWhiteSpace(request.AreaCompletionMailId))
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_typed_projection_required"));
            return;
        }
        if (activeCommunityCenterVaultPayment is not null ||
            activeCommunityCenterDonation is not null ||
            activeCommunityCenterRewardClaim is not null ||
            activeCommunityCenterFirstNote is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_player_busy"));
            return;
        }
        if (Game1.currentLocation is not CommunityCenter communityCenter ||
            !string.Equals(
                communityCenter.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase) ||
            !Game1.player.hasOrWillReceiveMail("canReadJunimoText"))
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_location_or_junimo_text_unavailable"));
            return;
        }

        var jojaLocked = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember");
        var ccLocked = Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete") ||
            Game1.MasterPlayer.hasCompletedCommunityCenter();
        var liveRoute = jojaLocked && ccLocked
            ? "conflicting_irreversible_flags"
            : jojaLocked ? "joja_locked" : ccLocked ? "community_center_locked" : "undecided";
        if (request.RouteState != liveRoute || liveRoute is not ("undecided" or "community_center_locked"))
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_route_state_drifted"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var note = new Point(
            request.CommunityCenterNoteTileX.Value,
            request.CommunityCenterNoteTileY.Value);
        var liveNote = CommunityCenterNoteTileRuntime(communityCenter, CommunityCenterVaultAreaId);
        var liveInteraction = CommunityCenterInteractionTileRuntime(
            communityCenter,
            CommunityCenterVaultAreaId,
            liveNote);
        if (liveNote != note || liveInteraction != target || !AreAdjacent(target, stand) ||
            !communityCenter.shouldNoteAppearInArea(CommunityCenterVaultAreaId) ||
            !communityCenter.isJunimoNoteAtArea(CommunityCenterVaultAreaId) ||
            communityCenter.bundleMutexes[CommunityCenterVaultAreaId].IsLocked() ||
            !IsTileOnMap(communityCenter, stand) || !IsTileWalkable(communityCenter, stand) ||
            IsTileOccupiedByCharacter(communityCenter, stand))
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_note_or_mutex_drifted"));
            return;
        }
        var paymentProjectionMatches = TryReadLiveCommunityCenterVaultPayment(
            communityCenter,
            request,
            out var completedCount,
            out var projectionFailure);
        var outcomeProjectionMatches = CommunityCenterOutcomeProjectionMatches(
            communityCenter,
            request,
            out var outcomeFailure);
        if (!paymentProjectionMatches || !outcomeProjectionMatches)
        {
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_projection_drifted:" +
                projectionFailure + ":" + outcomeFailure));
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
            pending.Completion.SetResult(CommunityCenterVaultPaymentBlocked(
                request,
                "community_center_vault_payment_path_unavailable:" + pathReason));
            return;
        }

        activeCommunityCenterVaultPayment = new ActiveCommunityCenterVaultPayment(
            pending,
            communityCenter,
            target,
            stand,
            path,
            maxMovement,
            request.BundleId.Value,
            request.ExpectedMoneyBefore.Value,
            completedCount,
            communityCenter.bundleRewards.TryGetValue(
                request.BundleId.Value,
                out var rewardAvailable) && rewardAvailable,
            communityCenter.areasComplete[CommunityCenterVaultAreaId],
            HasPendingCommunityCenterMail(Game1.player, request.AreaCompletionMailId),
            CommunityCenterCompleteBundleCount(communityCenter),
            communityCenter.areAllAreasComplete());
    }

    private void TickCommunityCenterVaultPayment()
    {
        var active = activeCommunityCenterVaultPayment;
        if (active is null)
            return;

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.CommunityCenter) ||
            active.ElapsedTicks > 4200)
        {
            CompleteCommunityCenterVaultPaymentBlocked(
                active,
                "community_center_vault_payment_world_location_or_timeout");
            return;
        }
        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_path_exhausted");
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
                    CompleteCommunityCenterVaultPaymentBlocked(
                        active,
                        "community_center_vault_payment_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_movement_stuck_or_blocked");
                return;
            }
            if (playerTile == next)
                active.PathIndex++;
            return;
        }

        StopAllMovement();
        if (!active.OpenIssued)
        {
            if (!TryReadLiveCommunityCenterVaultPayment(
                    active.CommunityCenter,
                    active.Pending.Request,
                    out var completedCount,
                    out var failure) ||
                completedCount != active.CompletedCountBefore ||
                active.CommunityCenter.bundleMutexes[CommunityCenterVaultAreaId].IsLocked())
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_preopen_drifted:" + failure);
                return;
            }
            Game1.player.faceDirection(DirectionTo(
                Game1.player.TilePoint,
                active.InteractionTile));
            active.CommunityCenter.checkBundle(CommunityCenterVaultAreaId);
            active.OpenIssued = true;
            return;
        }

        if (active.ExitIssued)
        {
            active.SettlementTicks++;
            if (Game1.activeClickableMenu is null && !Game1.freezeControls &&
                !Game1.isViewportOnCustomPath() &&
                CommunityCenterVaultPaymentPostconditionsMatch(active))
            {
                CompleteCommunityCenterVaultPayment(active);
            }
            else if (active.SettlementTicks > 3600)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_native_settlement_timeout_or_mismatch");
            }
            return;
        }

        if (Game1.activeClickableMenu is not JunimoNoteMenu menu)
        {
            if (Game1.activeClickableMenu is not null || ++active.OpenWaitTicks > 240)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_native_menu_open_failed");
            }
            return;
        }
        if (menu.whichArea != CommunityCenterVaultAreaId)
        {
            CompleteCommunityCenterVaultPaymentBlocked(
                active,
                "community_center_vault_payment_area_menu_drifted");
            return;
        }
        if (!active.BundleClickIssued)
        {
            var bundle = menu.bundles.FirstOrDefault(row => row.bundleIndex == active.BundleId);
            if (bundle is null || !bundle.canBeClicked() || !JunimoNoteMenu.canClick)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_bundle_button_unavailable");
                return;
            }
            menu.receiveLeftClick(bundle.bounds.Center.X, bundle.bounds.Center.Y);
            active.BundleClickIssued = true;
            if (!menu.specificBundlePage || menu.currentPageBundle?.bundleIndex != active.BundleId)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_bundle_click_failed");
                return;
            }
        }
        if (!active.PurchaseClickIssued)
        {
            var request = active.Pending.Request;
            if (!menu.specificBundlePage || menu.currentPageBundle?.bundleIndex != active.BundleId ||
                menu.purchaseButton is null || !JunimoNoteMenu.canClick ||
                Game1.player.Money != request.ExpectedMoneyBefore)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_purchase_button_or_money_drifted");
                return;
            }
            menu.receiveLeftClick(
                menu.purchaseButton.bounds.Center.X,
                menu.purchaseButton.bounds.Center.Y);
            active.PurchaseClickIssued = true;
            if (!CommunityCenterVaultPaymentImmediateEffectsMatch(active))
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_native_purchase_click_failed");
                return;
            }
        }

        if (menu.specificBundlePage)
        {
            if (!menu.isReadyToCloseMenuOrBundle())
            {
                if (++active.SettlementTicks > 1020)
                {
                    CompleteCommunityCenterVaultPaymentBlocked(
                        active,
                        "community_center_vault_payment_bundle_animation_timeout");
                }
                return;
            }
            menu.receiveLeftClick(menu.backButton.bounds.Center.X, menu.backButton.bounds.Center.Y);
            active.BackClickIssued = true;
            return;
        }
        active.BackClickIssued = true;
        if (!menu.isReadyToCloseMenuOrBundle())
        {
            if (++active.SettlementTicks > 1200)
            {
                CompleteCommunityCenterVaultPaymentBlocked(
                    active,
                    "community_center_vault_payment_menu_not_ready_to_exit");
            }
            return;
        }
        menu.exitThisMenu();
        active.ExitIssued = true;
    }

    private static bool TryReadLiveCommunityCenterVaultPayment(
        CommunityCenter communityCenter,
        TrainingExecutionRequest request,
        out int completedCount,
        out string failure)
    {
        completedCount = 0;
        failure = string.Empty;
        if (!request.BundleId.HasValue ||
            !Game1.netWorldState.Value.BundleData.TryGetValue(request.BundleDataKey, out var raw) ||
            !communityCenter.bundles.TryGetValue(request.BundleId.Value, out var bits))
        {
            failure = "bundle_request_or_live_row_missing";
            return false;
        }
        var key = request.BundleDataKey.Split('/');
        var fields = raw.Split('/');
        var parts = fields.Length >= Bundle.FieldCount
            ? ArgUtility.SplitBySpace(fields[Bundle.IngredientsIndex])
            : Array.Empty<string>();
        if (key.Length < 2 || key[0] != "Vault" ||
            !int.TryParse(key[1], out var bundleId) || bundleId != request.BundleId.Value ||
            CommunityCenter.getAreaNumberFromName(key[0]) != CommunityCenterVaultAreaId ||
            parts.Length != 3 || parts[0] != "-1" ||
            !int.TryParse(parts[1], out var price) || price != request.Price ||
            !int.TryParse(parts[2], out var quality) || quality < 0 ||
            bits.Length < 1)
        {
            failure = "vault_bundle_identity_or_money_shape_mismatch";
            return false;
        }
        completedCount = bits.Take(1).Count(value => value);
        var requiredSlots = ArgUtility.GetInt(fields, Bundle.NumberOfSlotsIndex, 1);
        var rewardAvailable = communityCenter.bundleRewards.TryGetValue(
            bundleId,
            out var liveRewardAvailable) && liveRewardAvailable;
        if (requiredSlots != 1 || completedCount != 0 || rewardAvailable ||
            Game1.player.Money != request.ExpectedMoneyBefore ||
            request.ExpectedMoneyAfter != request.ExpectedMoneyBefore - price)
        {
            failure = "vault_completion_reward_or_money_drifted";
            return false;
        }
        return true;
    }

    private static bool CommunityCenterVaultPaymentImmediateEffectsMatch(
        ActiveCommunityCenterVaultPayment active)
    {
        var request = active.Pending.Request;
        return Game1.player.Money == request.ExpectedMoneyAfter &&
            active.CommunityCenter.bundles.TryGetValue(active.BundleId, out var bits) &&
            bits.Length > 0 && bits[0] &&
            active.CommunityCenter.bundleRewards.TryGetValue(active.BundleId, out var reward) &&
            reward;
    }

    private static bool CommunityCenterVaultPaymentPostconditionsMatch(
        ActiveCommunityCenterVaultPayment active)
    {
        var request = active.Pending.Request;
        int[] newNoteAreas;
        try
        {
            newNoteAreas = JsonSerializer.Deserialize<int[]>(
                request.NewlyAppearingNoteAreaIdsJson) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            return false;
        }
        return CommunityCenterVaultPaymentImmediateEffectsMatch(active) &&
            CommunityCenterCompleteBundleCount(active.CommunityCenter) ==
                request.ExpectedCompleteBundleCountAfter &&
            active.CommunityCenter.areasComplete[CommunityCenterVaultAreaId] ==
                request.ExpectedAreaCompleteAfter &&
            HasPendingCommunityCenterMail(Game1.player, request.AreaCompletionMailId) ==
                request.ExpectedAreaCompletionMailPendingAfter &&
            HasPendingCommunityCenterMail(Game1.player, "ccBulletinThankYou") ==
                request.ExpectedBulletinThankYouPendingAfter &&
            active.CommunityCenter.areAllAreasComplete() == request.ExpectedAllAreasCompleteAfter &&
            (!request.ExpectedAllAreasCompleteAfter.GetValueOrDefault() ||
                Game1.player.mailReceived.Contains("ccIsComplete")) &&
            newNoteAreas.All(active.CommunityCenter.isJunimoNoteAtArea) &&
            !active.CommunityCenter.bundleMutexes[CommunityCenterVaultAreaId].IsLocked();
    }

    private void CompleteCommunityCenterVaultPayment(
        ActiveCommunityCenterVaultPayment active)
    {
        activeCommunityCenterVaultPayment = null;
        StopAllMovement();
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
            PrimitiveKind = "pay_community_center_vault_bundle",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "CommunityCenter.checkBundle_completed",
                "JunimoNoteMenu.receiveLeftClick_exact_bundle_completed",
                "JunimoNoteMenu.receiveLeftClick_purchaseButton_completed",
                "native_money_bundle_reward_and_lifecycle_receipt_verified"
            },
            RequestedEffect = "community_center.bundle=" + active.BundleId +
                ":money_payment=" + request.Price,
            ObservedEffect = "bundle=" + active.BundleId +
                ";money=" + Game1.player.Money +
                ";reward_available=true",
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 240,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.CommunityCenter.NameOrUniqueName,
            TargetTileX = active.InteractionTile.X,
            TargetTileY = active.InteractionTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = active.MoneyBefore.ToString(), After = Game1.player.Money.ToString() },
                new SimulatedFactChange { Path = "world_progress.community_center.bundle_rows[" + active.BundleId + "].ingredients[0].completed", Before = "false", After = "true" },
                new SimulatedFactChange { Path = "world_progress.community_center.bundle_rewards[" + active.BundleId + "]", Before = active.RewardAvailableBefore.ToString().ToLowerInvariant(), After = "true" },
                new SimulatedFactChange { Path = "world_progress.community_center.complete_bundle_count", Before = active.CompleteBundleCountBefore.ToString(), After = request.ExpectedCompleteBundleCountAfter?.ToString() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.areas_complete[4]", Before = active.AreaCompleteBefore.ToString().ToLowerInvariant(), After = request.ExpectedAreaCompleteAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "player.mail_for_tomorrow." + request.AreaCompletionMailId, Before = active.AreaMailPendingBefore.ToString().ToLowerInvariant(), After = request.ExpectedAreaCompletionMailPendingAfter?.ToString().ToLowerInvariant() ?? "unavailable" },
                new SimulatedFactChange { Path = "world_progress.community_center.all_areas_complete", Before = active.AllAreasCompleteBefore.ToString().ToLowerInvariant(), After = request.ExpectedAllAreasCompleteAfter?.ToString().ToLowerInvariant() ?? "unavailable" }
            }
        });
    }

    private void CompleteCommunityCenterVaultPaymentBlocked(
        ActiveCommunityCenterVaultPayment active,
        string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is JunimoNoteMenu menu && menu.heldItem is null)
            menu.exitThisMenu();
        activeCommunityCenterVaultPayment = null;
        active.Pending.Completion.SetResult(
            CommunityCenterVaultPaymentBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult CommunityCenterVaultPaymentBlocked(
        TrainingExecutionRequest request,
        string reason) =>
        BlockedWithPrimitive(
            request,
            "pay_community_center_vault_bundle",
            "community_center.vault_bundle_complete=true;money_decreased_exactly",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";money=" + Game1.player.Money,
            reason);
}
