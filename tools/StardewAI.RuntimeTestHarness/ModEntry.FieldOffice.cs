using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FieldOfficeNativeContract =
        "FieldOfficeDesk_mutex_then_Safari_Donate_then_FieldOfficeMenu_inventory_and_exact_piece_holder_then_native_ok_exit";

    private void StartFieldOfficeDonation(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!FieldOfficeRequestIsTyped(request) || activeFieldOfficeDonation is not null || HasActiveExecutorOperation() ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FieldOfficeBlocked(request,
                FieldOfficeRequestIsTyped(request) ? "field_office_donation_player_busy" : "field_office_donation_typed_projection_required"));
            return;
        }
        if (Game1.currentLocation is not IslandFieldOffice office ||
            !string.Equals(office.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            office.getSafariGuy() is null || office.safariGuyMutex.IsLocked())
        {
            pending.Completion.SetResult(FieldOfficeBlocked(request, "field_office_donation_location_professor_or_mutex_unavailable"));
            return;
        }

        var action = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        if (!AreAdjacent(action, stand) || office.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != request.FieldOfficeDeskActionRaw ||
            request.FieldOfficeDeskActionRaw != "FieldOfficeDesk" || !IsTileOnMap(office, stand) ||
            !IsTileWalkable(office, stand) || IsTileOccupiedByCharacter(office, stand) ||
            !FieldOfficeLiveProjectionMatches(request, office))
        {
            pending.Completion.SetResult(FieldOfficeBlocked(request, "field_office_donation_endpoint_or_projection_drifted"));
            return;
        }
        var path = TryBuildTilePath(office, Game1.player.TilePoint, stand, request.MaxMovementTiles ?? 512,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FieldOfficeBlocked(request, "field_office_donation_path_unavailable:" + pathReason));
            return;
        }
        activeFieldOfficeDonation = new ActiveFieldOfficeDonation(pending, office, action, stand, path);
    }

    private void TickFieldOfficeDonation()
    {
        var active = activeFieldOfficeDonation;
        if (active is null)
            return;
        try
        {
            if (++active.ElapsedTicks > 3600 || !ReferenceEquals(Game1.currentLocation, active.Office))
            {
                CompleteFieldOfficeDonationBlocked(active, "field_office_donation_world_location_or_timeout");
                return;
            }
            if (!active.DeskIssued && Game1.player.TilePoint != active.StandTile)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_path_exhausted");
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
                if (Game1.player.TilePoint != active.LastTile)
                {
                    active.LastTile = Game1.player.TilePoint;
                    active.StuckTicks = 0;
                }
                else if (++active.StuckTicks > 60)
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_movement_stuck");
                return;
            }

            StopAllMovement();
            if (active.Cooldown-- > 0)
                return;
            var request = active.Pending.Request;
            if (!active.DeskIssued)
            {
                if (!FieldOfficeLiveProjectionMatches(request, active.Office))
                {
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_preopen_projection_drifted");
                    return;
                }
                Game1.player.faceDirection(DirectionTo(active.StandTile, active.ActionTile));
                var handled = active.Office.checkAction(
                    new xTile.Dimensions.Location(active.ActionTile.X, active.ActionTile.Y),
                    new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                if (!handled)
                {
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_desk_action_not_handled");
                    return;
                }
                active.DeskIssued = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.DonateChosen)
            {
                if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion ||
                    active.Office.lastQuestionKey != "Safari")
                {
                    if (++active.QuestionWaitTicks <= 240)
                        return;
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_safari_question_missing");
                    return;
                }
                var response = dialogue.responses?.FirstOrDefault(value => value.responseKey == "Donate");
                if (response is null || !active.Office.answerDialogue(response) || Game1.activeClickableMenu is not FieldOfficeMenu)
                {
                    CompleteFieldOfficeDonationBlocked(active, "field_office_donation_native_donate_response_failed");
                    return;
                }
                active.DonateChosen = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.ExitClicked && Game1.activeClickableMenu is not FieldOfficeMenu menu)
            {
                CompleteFieldOfficeDonationBlocked(active, "field_office_donation_native_menu_missing");
                return;
            }
            if (!active.ExitClicked)
            {
                var fieldOfficeMenu = (FieldOfficeMenu)Game1.activeClickableMenu;
                var slot = request.InventorySlotIndex!.Value;
                var piece = request.FieldOfficeTargetPieceIndex!.Value;
                if (!active.InventoryClicked)
                {
                    if (slot < 0 || slot >= fieldOfficeMenu.inventory.inventory.Count || !FieldOfficeLiveProjectionMatches(request, active.Office))
                    {
                        CompleteFieldOfficeDonationBlocked(active, "field_office_donation_menu_inventory_projection_drifted");
                        return;
                    }
                    var component = fieldOfficeMenu.inventory.inventory[slot];
                    fieldOfficeMenu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
                    active.InventoryClicked = true;
                    if (fieldOfficeMenu.heldItem?.QualifiedItemId != request.QualifiedItemId)
                        CompleteFieldOfficeDonationBlocked(active, "field_office_donation_native_inventory_pickup_failed");
                    return;
                }
                if (!active.PieceClicked)
                {
                    if (piece < 0 || piece >= fieldOfficeMenu.pieceHolders.Count || fieldOfficeMenu.pieceHolders[piece].item is not null ||
                        fieldOfficeMenu.pieceHolders[piece].label != request.ItemId)
                    {
                        CompleteFieldOfficeDonationBlocked(active, "field_office_donation_piece_holder_drifted");
                        return;
                    }
                    var holder = fieldOfficeMenu.pieceHolders[piece];
                    fieldOfficeMenu.receiveLeftClick(holder.bounds.Center.X, holder.bounds.Center.Y);
                    active.PieceClicked = true;
                    if (!active.Office.piecesDonated[piece])
                        CompleteFieldOfficeDonationBlocked(active, "field_office_donation_native_piece_click_failed");
                    return;
                }
                if (!active.RemainderReturned)
                {
                    if (fieldOfficeMenu.heldItem is not null)
                    {
                        var component = fieldOfficeMenu.inventory.inventory[slot];
                        fieldOfficeMenu.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y);
                        if (fieldOfficeMenu.heldItem is not null)
                        {
                            CompleteFieldOfficeDonationBlocked(active, "field_office_donation_remainder_return_failed");
                            return;
                        }
                    }
                    active.RemainderReturned = true;
                    return;
                }
                if (fieldOfficeMenu.okButton is null || !fieldOfficeMenu.readyToClose())
                    return;
                fieldOfficeMenu.receiveLeftClick(fieldOfficeMenu.okButton.bounds.Center.X, fieldOfficeMenu.okButton.bounds.Center.Y);
                active.ExitClicked = true;
                active.Cooldown = 8;
                return;
            }

            if (Game1.activeClickableMenu is DialogueBox donationDialogue)
            {
                if (++active.DialogueAdvanceTicks % 12 == 0)
                    donationDialogue.receiveLeftClick(
                        donationDialogue.xPositionOnScreen + donationDialogue.width / 2,
                        donationDialogue.yPositionOnScreen + donationDialogue.height / 2);
                return;
            }
            if (Game1.activeClickableMenu is not null || active.Office.safariGuyMutex.IsLocked())
                return;
            if (!FieldOfficePostconditionsMatch(request, active.Office))
            {
                CompleteFieldOfficeDonationBlocked(active, "field_office_donation_native_settlement_mismatch");
                return;
            }
            CompleteFieldOfficeDonation(active);
        }
        catch (Exception ex)
        {
            CompleteFieldOfficeDonationBlocked(active, "field_office_donation_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static bool FieldOfficeRequestIsTyped(TrainingExecutionRequest request) =>
        request.OptionId == "executor.donate_field_office_piece" && request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue && request.InventorySlotIndex.HasValue &&
        request.ExpectedStackBefore.HasValue && request.ExpectedStackAfter.HasValue &&
        request.FieldOfficeTargetPieceIndex is >= 0 and < 11 && !string.IsNullOrWhiteSpace(request.FieldOfficeTargetPieceKind) &&
        !string.IsNullOrWhiteSpace(request.FieldOfficeTargetSetKind) && request.FieldOfficeDonatedPieceCountBefore.HasValue &&
        request.FieldOfficeDonatedPieceCountAfter == request.FieldOfficeDonatedPieceCountBefore + 1 &&
        request.FieldOfficeCompletesSet.HasValue && !string.IsNullOrWhiteSpace(request.FieldOfficeNewRewardItemsJson) &&
        !string.IsNullOrWhiteSpace(request.FieldOfficeRewardsBeforeJson) && !string.IsNullOrWhiteSpace(request.FieldOfficeRewardsAfterJson) &&
        request.FieldOfficeCollectedNutBefore.HasValue && request.FieldOfficeFinaleReadyAfter.HasValue &&
        request.FieldOfficePlantsRestoredLeftBefore.HasValue && request.FieldOfficePlantsRestoredRightBefore.HasValue &&
        request.FieldOfficeFinaleReceivedBefore.HasValue && request.FieldOfficeGoldenWalnutsFoundBefore.HasValue &&
        request.FieldOfficeProjectionStatus == "exact_locked_base_1.6.15" && request.NativeContract == FieldOfficeNativeContract &&
        !string.IsNullOrWhiteSpace(request.ItemId) && !string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
        !string.IsNullOrWhiteSpace(request.TargetRuntimeType);

    private static bool FieldOfficeLiveProjectionMatches(TrainingExecutionRequest request, IslandFieldOffice office)
    {
        var slot = request.InventorySlotIndex!.Value;
        var piece = request.FieldOfficeTargetPieceIndex!.Value;
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        return item?.QualifiedItemId == request.QualifiedItemId && item.ItemId == request.ItemId &&
            (item.GetType().FullName ?? item.GetType().Name) == request.TargetRuntimeType &&
            item.Stack == request.ExpectedStackBefore && request.ExpectedStackAfter == item.Stack - 1 &&
            piece < office.piecesDonated.Count && !office.piecesDonated[piece] &&
            office.piecesDonated.Count(value => value) == request.FieldOfficeDonatedPieceCountBefore &&
            FieldOfficeRewardJson(office) == request.FieldOfficeRewardsBeforeJson &&
            Game1.netWorldState.Value.GoldenWalnutsFound == request.FieldOfficeGoldenWalnutsFoundBefore &&
            office.plantsRestoredLeft.Value == request.FieldOfficePlantsRestoredLeftBefore &&
            office.plantsRestoredRight.Value == request.FieldOfficePlantsRestoredRightBefore &&
            Game1.player.hasOrWillReceiveMail("fieldOfficeFinale") == request.FieldOfficeFinaleReceivedBefore &&
            FieldOfficeItemMatchesPiece(request.QualifiedItemId, piece);
    }

    private static bool FieldOfficePostconditionsMatch(TrainingExecutionRequest request, IslandFieldOffice office)
    {
        var slot = request.InventorySlotIndex!.Value;
        var piece = request.FieldOfficeTargetPieceIndex!.Value;
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var stackAfter = item?.QualifiedItemId == request.QualifiedItemId ? item.Stack : 0;
        var allPieces = office.piecesDonated.All(value => value);
        var finaleReady = allPieces && office.plantsRestoredLeft.Value && office.plantsRestoredRight.Value;
        var nutMatches = string.IsNullOrWhiteSpace(request.FieldOfficeCollectedNutKey) ||
            Game1.player.team.collectedNutTracker.Contains(request.FieldOfficeCollectedNutKey);
        return office.piecesDonated[piece] && office.piecesDonated.Count(value => value) == request.FieldOfficeDonatedPieceCountAfter &&
            stackAfter == request.ExpectedStackAfter && FieldOfficeRewardJson(office) == request.FieldOfficeRewardsAfterJson &&
            finaleReady == request.FieldOfficeFinaleReadyAfter && nutMatches &&
            Game1.netWorldState.Value.GoldenWalnutsFound == request.FieldOfficeGoldenWalnutsFoundBefore &&
            office.plantsRestoredLeft.Value == request.FieldOfficePlantsRestoredLeftBefore &&
            office.plantsRestoredRight.Value == request.FieldOfficePlantsRestoredRightBefore;
    }

    private static bool FieldOfficeItemMatchesPiece(string qualifiedItemId, int piece) => piece switch
    {
        0 or 2 => qualifiedItemId == "(O)823",
        1 => qualifiedItemId == "(O)824",
        3 => qualifiedItemId == "(O)822",
        4 => qualifiedItemId == "(O)821",
        5 => qualifiedItemId == "(O)820",
        6 or 7 => qualifiedItemId == "(O)826",
        8 => qualifiedItemId == "(O)825",
        9 => qualifiedItemId == "(O)827",
        10 => qualifiedItemId == "(O)828",
        _ => false
    };

    private static string FieldOfficeRewardJson(IslandFieldOffice office) => JsonSerializer.Serialize(
        office.uncollectedRewards.Select(item => new
        {
            qualified_item_id = item.QualifiedItemId,
            stack = item.Stack,
            quality = item.Quality
        }).ToArray());

    private void CompleteFieldOfficeDonation(ActiveFieldOfficeDonation active)
    {
        StopAllMovement();
        activeFieldOfficeDonation = null;
        var request = active.Pending.Request;
        var slot = request.InventorySlotIndex!.Value;
        var item = slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var stack = item?.QualifiedItemId == request.QualifiedItemId ? item.Stack : 0;
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
            PrimitiveKind = "donate_field_office_piece",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_FieldOfficeDesk_mutex_completed",
                "native_Safari_Donate_response_completed",
                "native_FieldOfficeMenu_inventory_and_exact_holder_click_completed",
                "piece_stack_reward_nut_and_finale_readiness_verified"
            },
            RequestedEffect = "field_office_piece=" + request.FieldOfficeTargetPieceIndex + ":donated=true",
            ObservedEffect = FieldOfficeObservedEffect(active.Office, slot),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 600,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Office.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "world_progress.island_field_office.pieces[" + request.FieldOfficeTargetPieceIndex + "].donated", Before = "false", After = "true" },
                new SimulatedFactChange { Path = "world_progress.island_field_office.donated_piece_count", Before = request.FieldOfficeDonatedPieceCountBefore.ToString()!, After = active.Office.piecesDonated.Count(value => value).ToString() },
                new SimulatedFactChange { Path = "player.inventory[" + slot + "].stack", Before = request.ExpectedStackBefore.ToString()!, After = stack.ToString() },
                new SimulatedFactChange { Path = "world_progress.island_field_office.uncollected_rewards", Before = request.FieldOfficeRewardsBeforeJson, After = FieldOfficeRewardJson(active.Office) }
            }
        });
    }

    private void CompleteFieldOfficeDonationBlocked(ActiveFieldOfficeDonation active, string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is FieldOfficeMenu menu)
            menu.exitThisMenuNoSound();
        if (active.Office.safariGuyMutex.IsLockHeld())
            active.Office.safariGuyMutex.ReleaseLock();
        activeFieldOfficeDonation = null;
        active.Pending.Completion.SetResult(FieldOfficeBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult FieldOfficeBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "donate_field_office_piece",
            "field_office_piece=" + request.FieldOfficeTargetPieceIndex + ":donated=true",
            FieldOfficeObservedEffect(Game1.getLocationFromName("IslandFieldOffice") as IslandFieldOffice, request.InventorySlotIndex ?? -1),
            reason);

    private static string FieldOfficeObservedEffect(IslandFieldOffice? office, int slot) =>
        "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";donated_count=" + (office?.piecesDonated.Count(value => value).ToString() ?? "unavailable") +
        ";slot=" + slot + ";rewards=" + (office is null ? "unavailable" : FieldOfficeRewardJson(office));
}
