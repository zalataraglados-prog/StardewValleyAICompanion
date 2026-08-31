using System.Globalization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeMovieTheaterNativeContract =
        "NPC_ticket_native_invite_then_Town_Theater_Entrance_yes_then_optional_MovieTheater_Concessions_ShopMenu_then_Theater_Doors_mutex_ready_native_MovieTheaterScreening_event_and_week_friendship_receipt";

    private static readonly System.Reflection.FieldInfo? RuntimeMovieTheaterCurrentStateField =
        AccessTools.Field(typeof(MovieTheater), "currentState");

    private void StartMovieTheater(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!MovieTheaterRequestIsTyped(request))
        {
            pending.Completion.SetResult(MovieBlocked(request, null, "movie_theater_typed_request_required"));
            return;
        }
        if (activeMovieTheater is not null || HasActiveExecutorOperation() ||
            Game1.activeClickableMenu is not null || Game1.currentMinigame is not null ||
            Game1.dialogueUp || Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(MovieBlocked(request, null, "movie_theater_player_busy"));
            return;
        }

        var theater = Game1.getLocationFromName("MovieTheater") as MovieTheater;
        var location = Game1.currentLocation;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var guest = request.MovieGuestName == "__alone__"
            ? null
            : Game1.getCharacterFromName(request.MovieGuestName);
        var liveReasons = MovieTheaterLiveStartBlockReasons(request, location, theater, interaction, stand, guest);
        if (liveReasons.Count > 0)
        {
            pending.Completion.SetResult(MovieBlocked(request, null, liveReasons.ToArray()));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(MovieBlocked(request, null,
                "movie_theater_path_unavailable:" + pathReason));
            return;
        }

        activeMovieTheater = new ActiveMovieTheater(
            pending, location, theater!, interaction, stand, path, maxMovementTiles, guest);
    }

    private static bool MovieTheaterRequestIsTyped(TrainingExecutionRequest request)
    {
        var stage = request.MovieStage;
        var concessionKey = string.IsNullOrWhiteSpace(request.MovieConcessionId) ? "none" : request.MovieConcessionId;
        return (stage is "watch_movie_invite_guest" or "watch_movie_enter" or
                "watch_movie_concession" or "watch_movie_screening") &&
            request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
            request.StandTileX.HasValue && request.StandTileY.HasValue &&
            Math.Abs(request.TargetTileX.Value - request.StandTileX.Value) +
                Math.Abs(request.TargetTileY.Value - request.StandTileY.Value) == 1 &&
            !string.IsNullOrWhiteSpace(request.LocationId) &&
            !string.IsNullOrWhiteSpace(request.MovieProjectionFingerprint) &&
            !string.IsNullOrWhiteSpace(request.MovieId) &&
            !string.IsNullOrWhiteSpace(request.MovieGuestName) &&
            request.MovieObjectiveKey == request.MovieId + ":" + request.MovieGuestName + ":" + concessionKey &&
            request.MovieFriendshipEffective is >= 0 &&
            request.MovieConcessionFriendshipEffective is >= 0 &&
            request.NativeContract == RuntimeMovieTheaterNativeContract &&
            (stage != "watch_movie_invite_guest" ||
                (request.MovieGuestName != "__alone__" && request.MovieTicketSlotIndex is >= 0 &&
                 request.MovieTicketStackBefore is > 0)) &&
            (stage == "watch_movie_invite_guest" ||
                (!string.IsNullOrWhiteSpace(request.MovieActionRaw) &&
                 !string.IsNullOrWhiteSpace(request.MovieActionToken))) &&
            (stage != "watch_movie_concession" ||
                (request.MovieGuestName != "__alone__" && !string.IsNullOrWhiteSpace(request.MovieConcessionId))) &&
            (request.MovieGuestName != "__alone__" || string.IsNullOrWhiteSpace(request.MovieConcessionId));
    }

    private static List<string> MovieTheaterLiveStartBlockReasons(
        TrainingExecutionRequest request,
        GameLocation location,
        MovieTheater? theater,
        Point interaction,
        Point stand,
        NPC? guest)
    {
        var reasons = new List<string>();
        if (theater is null || RuntimeMovieTheaterCurrentStateField?.GetValue(theater) is not NetInt)
            reasons.Add("movie_theater_1_6_15_reflection_contract_unavailable");
        if (!Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater"))
            reasons.Add("movie_theater_not_unlocked");
        if (MovieTheater.GetMovieToday()?.Id != request.MovieId)
            reasons.Add("movie_theater_movie_id_drifted");
        if (Game1.isFestival() || Game1.timeOfDay is < 900 or > 2100 ||
            Game1.player.lastSeenMovieWeek.Value >= Game1.Date.TotalWeeks)
            reasons.Add("movie_theater_schedule_or_week_gate_drifted");
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
            reasons.Add("movie_theater_location_drifted");
        if (!AreAdjacent(stand, interaction) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("movie_theater_stand_tile_drifted");

        if (request.MovieStage == "watch_movie_invite_guest")
        {
            var slot = request.MovieTicketSlotIndex!.Value;
            if (guest is null || !ReferenceEquals(guest.currentLocation, location) || guest.TilePoint != interaction ||
                slot >= Game1.player.Items.Count || Game1.player.Items[slot]?.QualifiedItemId != "(O)809" ||
                Game1.player.Items[slot]?.Stack != request.MovieTicketStackBefore ||
                Game1.player.team.movieInvitations.Any(invitation => invitation.farmer == Game1.player))
                reasons.Add("movie_theater_guest_ticket_or_invitation_drifted");
        }
        else
        {
            var action = location.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
            if (!string.Equals(action, request.MovieActionRaw, StringComparison.Ordinal) ||
                !string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                    request.MovieActionToken, StringComparison.Ordinal))
                reasons.Add("movie_theater_action_tile_drifted");
        }

        if (request.MovieStage == "watch_movie_enter" &&
            (request.MovieActionToken != "Theater_Entrance" || CountInventoryItems("(O)809") < 1 ||
             Game1.player.team.movieMutex.IsLocked()))
            reasons.Add("movie_theater_entrance_not_ready");
        if (request.MovieStage == "watch_movie_concession" &&
            (request.MovieActionToken != "Concessions" || theater is null || guest is null ||
             !MovieInvitationMatches(request.MovieGuestName, requireFulfilled: true) ||
             theater.GetConcessionsDictionary().Keys.Any(character => character.Name == request.MovieGuestName)))
            reasons.Add("movie_theater_concession_not_ready");
        if (request.MovieStage == "watch_movie_screening" &&
            (request.MovieActionToken != "Theater_Doors" || theater is null ||
             ReadMovieTheaterCurrentState(theater) != 0 ||
             Game1.player.team.movieMutex.IsLocked() && !Game1.player.team.movieMutex.IsLockHeld() ||
             request.MovieGuestName != "__alone__" &&
                (!MovieInvitationMatches(request.MovieGuestName, requireFulfilled: true) ||
                 !MovieConcessionMatches(theater, request.MovieGuestName, request.MovieConcessionId))))
            reasons.Add("movie_theater_screening_not_ready");
        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private void TickMovieTheaterSafely()
    {
        var active = activeMovieTheater;
        if (active is null)
            return;
        try
        {
            TickMovieTheater(active);
        }
        catch (Exception ex)
        {
            StopAllMovement();
            activeMovieTheater = null;
            Monitor.Log($"Movie theater execution failed: {ex}", StardewModdingAPI.LogLevel.Error);
            active.Pending.Completion.SetResult(MovieBlocked(
                active.Pending.Request, active, "movie_theater_execution_exception:" + ex.GetType().Name));
        }
    }

    private void TickMovieTheater(ActiveMovieTheater active)
    {
        if (active.Phase == MovieRuntimePhase.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "movie_theater", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
                CompleteMovieTheaterBlocked(active, failure);
            else if (movement == NativeObjectMovementStatus.Ready)
                BeginMovieTheaterNativeStage(active);
            return;
        }

        active.ElapsedTicks++;
        active.PhaseTicks++;
        if (active.InputCooldown > 0)
            active.InputCooldown--;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_stage_timeout");
            return;
        }

        switch (active.Phase)
        {
            case MovieRuntimePhase.WaitQuestion:
                TickMovieTheaterQuestion(active);
                break;
            case MovieRuntimePhase.WaitWarp:
                TickMovieTheaterWarp(active);
                break;
            case MovieRuntimePhase.WaitConcessionShop:
                TickMovieTheaterConcessionShop(active);
                break;
            case MovieRuntimePhase.WaitConcessionReceipt:
            case MovieRuntimePhase.CloseInformationalDialogue:
                TickMovieTheaterInformationalDialogue(active);
                break;
            case MovieRuntimePhase.WaitScreeningStart:
            case MovieRuntimePhase.AdvanceScreening:
                TickMovieTheaterScreening(active);
                break;
            case MovieRuntimePhase.VerifyScreening:
                CompleteMovieTheaterScreening(active);
                break;
        }
    }

    private void BeginMovieTheaterNativeStage(ActiveMovieTheater active)
    {
        var request = active.Pending.Request;
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (request.MovieStage == "watch_movie_invite_guest")
        {
            Game1.player.CurrentToolIndex = request.MovieTicketSlotIndex!.Value;
        }
        active.NativeCheckActionHandled = active.Location.checkAction(
            new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
            Game1.viewport,
            Game1.player);
        if (!active.NativeCheckActionHandled)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_native_check_action_rejected");
            return;
        }

        if (request.MovieStage == "watch_movie_invite_guest")
        {
            active.NativeReceiptObserved =
                CountInventoryItems("(O)809") == active.TicketCountBefore - 1 &&
                MovieInvitationMatches(request.MovieGuestName, requireFulfilled: false);
            if (!active.NativeReceiptObserved)
            {
                CompleteMovieTheaterBlocked(active, "movie_theater_native_invitation_receipt_missing");
                return;
            }
            SetMoviePhase(active, MovieRuntimePhase.CloseInformationalDialogue);
            return;
        }
        if (request.MovieStage == "watch_movie_screening")
        {
            SetMoviePhase(active, MovieRuntimePhase.WaitScreeningStart);
            return;
        }
        SetMoviePhase(active, MovieRuntimePhase.WaitQuestion);
    }

    private void TickMovieTheaterQuestion(ActiveMovieTheater active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.PhaseTicks > 240)
                CompleteMovieTheaterBlocked(active, "movie_theater_native_question_timeout");
            return;
        }
        var expectedQuestion = active.Pending.Request.MovieStage == "watch_movie_enter"
            ? "EnterTheaterSpendTicket"
            : "Concession";
        if (!menu.isQuestion || Game1.currentLocation.lastQuestionKey != expectedQuestion)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_native_question_identity_mismatch");
            return;
        }
        if (!TryClickMovieDialogueResponse(menu, "Yes"))
            return;
        SetMoviePhase(active, active.Pending.Request.MovieStage == "watch_movie_enter"
            ? MovieRuntimePhase.WaitWarp
            : MovieRuntimePhase.WaitConcessionShop);
    }

    private void TickMovieTheaterWarp(ActiveMovieTheater active)
    {
        if (Game1.currentLocation is MovieTheater theater &&
            string.Equals(theater.NameOrUniqueName, "MovieTheater", StringComparison.Ordinal) &&
            CountInventoryItems("(O)809") == active.TicketCountBefore - 1)
        {
            active.NativeReceiptObserved = true;
            CompleteMovieTheaterStage(active, new[]
            {
                "native_Town_Theater_Entrance_action_opened_exact_spend_ticket_question",
                "native_EnterTheaterSpendTicket_Yes_consumed_one_ticket",
                "native_warp_entered_MovieTheater"
            });
            return;
        }
        if (active.PhaseTicks > 360)
            CompleteMovieTheaterBlocked(active, "movie_theater_native_entrance_warp_timeout");
    }

    private void TickMovieTheaterConcessionShop(ActiveMovieTheater active)
    {
        if (Game1.activeClickableMenu is not ShopMenu shop)
        {
            if (active.PhaseTicks > 240)
                CompleteMovieTheaterBlocked(active, "movie_theater_native_concession_shop_timeout");
            return;
        }
        if (shop.ShopId != "Concessions" || shop.onPurchase is null || shop.readOnly)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_concession_shop_contract_mismatch");
            return;
        }
        if (shop.safetyTimer > 0 || active.InputCooldown > 0)
            return;
        var request = active.Pending.Request;
        var index = shop.forSale.FindIndex(item => item is MovieConcession concession &&
            concession.Id == request.MovieConcessionId);
        if (index < 0 || shop.forSale[index] is not MovieConcession selected ||
            !shop.itemPriceAndStock.TryGetValue(selected, out var stock) ||
            stock.Price != selected.salePrice() || stock.Price <= 0 || Game1.player.Money < stock.Price)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_concession_item_or_price_drifted");
            return;
        }
        if (index < shop.currentItemIndex)
        {
            shop.receiveLeftClick(shop.upArrow.bounds.Center.X, shop.upArrow.bounds.Center.Y);
            active.InputCooldown = 2;
            return;
        }
        if (index >= shop.currentItemIndex + shop.forSaleButtons.Count)
        {
            shop.receiveLeftClick(shop.downArrow.bounds.Center.X, shop.downArrow.bounds.Center.Y);
            active.InputCooldown = 2;
            return;
        }
        active.ConcessionPrice = stock.Price;
        var row = shop.forSaleButtons[index - shop.currentItemIndex];
        shop.receiveLeftClick(row.bounds.Center.X, row.bounds.Center.Y);
        active.NativeReceiptObserved =
            Game1.player.Money == active.MoneyBefore - active.ConcessionPrice &&
            MovieConcessionMatches(active.Theater, request.MovieGuestName, request.MovieConcessionId);
        if (!active.NativeReceiptObserved)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_native_concession_purchase_receipt_missing");
            return;
        }
        SetMoviePhase(active, MovieRuntimePhase.WaitConcessionReceipt);
    }

    private void TickMovieTheaterInformationalDialogue(ActiveMovieTheater active)
    {
        if (Game1.activeClickableMenu is null)
        {
            CompleteMovieTheaterStage(active,
                active.Pending.Request.MovieStage == "watch_movie_invite_guest"
                    ? new[]
                    {
                        "native_NPC_active_movie_ticket_interaction_consumed_one_ticket",
                        "native_MovieTheater_Invite_created_exact_guest_invitation",
                        "native_invitation_dialogue_settled"
                    }
                    : new[]
                    {
                        "native_Concessions_action_and_yes_response_opened_callback_shop",
                        "exact_concession_row_purchased_through_native_ShopMenu",
                        "money_delta_and_GetConcessionsDictionary_receipts_verified"
                    });
            return;
        }
        if (Game1.activeClickableMenu is not DialogueBox dialogue || dialogue.isQuestion)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_unexpected_receipt_menu");
            return;
        }
        if (dialogue.transitioning || dialogue.safetyTimer > 0 || active.InputCooldown > 0)
            return;
        dialogue.receiveLeftClick(
            dialogue.xPositionOnScreen + dialogue.width / 2,
            dialogue.yPositionOnScreen + dialogue.height / 2);
        active.InputCooldown = 4;
    }

    private void TickMovieTheaterScreening(ActiveMovieTheater active)
    {
        var currentEvent = Game1.CurrentEvent;
        if (currentEvent is not null)
        {
            if (currentEvent.id != "MovieTheaterScreening")
            {
                CompleteMovieTheaterBlocked(active, "movie_theater_wrong_native_event_started:" + currentEvent.id);
                return;
            }
            active.SawScreeningEvent = true;
            active.Phase = MovieRuntimePhase.AdvanceScreening;
            if (Game1.activeClickableMenu is DialogueBox dialogue)
            {
                if (dialogue.isQuestion)
                {
                    CompleteMovieTheaterBlocked(active, "movie_theater_screening_unexpected_question");
                    return;
                }
                if (!dialogue.transitioning && dialogue.safetyTimer <= 0 && active.InputCooldown <= 0)
                {
                    dialogue.receiveLeftClick(
                        dialogue.xPositionOnScreen + dialogue.width / 2,
                        dialogue.yPositionOnScreen + dialogue.height / 2);
                    active.InputCooldown = 4;
                }
            }
            else if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not ReadyCheckDialog)
            {
                CompleteMovieTheaterBlocked(active, "movie_theater_screening_unexpected_menu:" +
                    Game1.activeClickableMenu.GetType().Name);
            }
            return;
        }

        if (active.SawScreeningEvent && Game1.player.lastSeenMovieWeek.Value >= Game1.Date.TotalWeeks)
        {
            active.SawRequestMovieEndReceipt = true;
            if (Game1.currentLocation is MovieTheater && !Game1.globalFade && !Game1.fadeToBlack)
                SetMoviePhase(active, MovieRuntimePhase.VerifyScreening);
            return;
        }
        if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not ReadyCheckDialog)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_start_unexpected_menu:" +
                Game1.activeClickableMenu.GetType().Name);
            return;
        }
        if (active.PhaseTicks > 1200 && !active.SawScreeningEvent)
            CompleteMovieTheaterBlocked(active, "movie_theater_native_screening_start_timeout");
    }

    private void CompleteMovieTheaterScreening(ActiveMovieTheater active)
    {
        var request = active.Pending.Request;
        var totalWeek = Game1.Date.TotalWeeks;
        var expectedFriendship = (request.MovieFriendshipEffective ?? 0) +
            (request.MovieConcessionFriendshipEffective ?? 0);
        var friendshipAfter = active.Guest is not null &&
            Game1.player.friendshipData.TryGetValue(active.Guest.Name, out var friendship)
                ? friendship.Points
                : (int?)null;
        var friendshipVerified = active.Guest is null
            ? expectedFriendship == 0
            : active.FriendshipBefore.HasValue && friendshipAfter.HasValue &&
              friendshipAfter.Value - active.FriendshipBefore.Value == expectedFriendship;
        var guestWeekVerified = active.Guest is null || active.Guest.lastSeenMovieWeek.Value >= totalWeek;
        var verified = active.NativeCheckActionHandled && active.SawScreeningEvent &&
            active.SawRequestMovieEndReceipt && Game1.player.lastSeenMovieWeek.Value >= totalWeek &&
            guestWeekVerified && friendshipVerified && Game1.CurrentEvent is null &&
            Game1.currentLocation is MovieTheater;
        if (!verified)
        {
            CompleteMovieTheaterBlocked(active, "movie_theater_native_screening_receipt_mismatch");
            return;
        }
        CompleteMovieTheaterStage(active, new[]
        {
            "native_Theater_Doors_mutex_and_ready_flow_started_screening",
            "native_MovieTheaterScreening_event_ran_without_skipEvent",
            "native_requestMovieEnd_updated_player_and_guest_week_receipts",
            "exact_movie_and_optional_concession_friendship_delta_verified",
            "native_event_cleanup_returned_player_to_MovieTheater_lobby"
        });
    }

    private void CompleteMovieTheaterStage(ActiveMovieTheater active, string[] reasons)
    {
        StopAllMovement();
        activeMovieTheater = null;
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
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "watch_movie",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = MovieRequestedEffect(request),
            ObservedEffect = MovieObservedEffect(active),
            BlockReasons = Array.Empty<string>(),
            ChangedFacts = MovieChangedFacts(active)
        });
    }

    private void CompleteMovieTheaterBlocked(ActiveMovieTheater active, params string[] reasons)
    {
        StopAllMovement();
        activeMovieTheater = null;
        active.Pending.Completion.SetResult(MovieBlocked(active.Pending.Request, active, reasons));
    }

    private static TrainingExecutionResult MovieBlocked(
        TrainingExecutionRequest request,
        ActiveMovieTheater? active,
        params string[] reasons) =>
        new()
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = active is not null,
            ActualTicks = active?.ElapsedTicks,
            StartedAt = active?.StartedAt ?? DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "watch_movie",
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = MovieRequestedEffect(request),
            ObservedEffect = MovieObservedEffect(active),
            BlockReasons = reasons,
            ChangedFacts = active is null ? Array.Empty<SimulatedFactChange>() : MovieChangedFacts(active)
        };

    private static bool TryClickMovieDialogueResponse(DialogueBox menu, string responseKey)
    {
        var index = Array.FindIndex(menu.responses,
            response => string.Equals(response.responseKey, responseKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || menu.transitioning || menu.safetyTimer > 0 ||
            menu.responseCC is null || index >= menu.responseCC.Count)
            return false;
        var bounds = menu.responseCC[index].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        return true;
    }

    private static void SetMoviePhase(ActiveMovieTheater active, MovieRuntimePhase phase)
    {
        active.Phase = phase;
        active.PhaseTicks = 0;
        active.InputCooldown = 0;
    }

    private static bool MovieInvitationMatches(string guestName, bool requireFulfilled) =>
        Game1.player.team.movieInvitations.Any(invitation =>
            invitation.farmer == Game1.player && invitation.invitedNPC?.Name == guestName &&
            (!requireFulfilled || invitation.fulfilled));

    private static bool MovieConcessionMatches(MovieTheater theater, string guestName, string concessionId)
    {
        if (string.IsNullOrWhiteSpace(concessionId))
            return true;
        return theater.GetConcessionsDictionary().Any(pair =>
            pair.Key.Name == guestName && pair.Value.Id == concessionId);
    }

    private static int ReadMovieTheaterCurrentState(MovieTheater theater) =>
        RuntimeMovieTheaterCurrentStateField?.GetValue(theater) is NetInt value ? value.Value : -1;

    private static string MovieRequestedEffect(TrainingExecutionRequest request) =>
        "stage=" + request.MovieStage +
        ";movie_id=" + request.MovieId +
        ";guest=" + request.MovieGuestName +
        ";concession=" + (string.IsNullOrWhiteSpace(request.MovieConcessionId) ? "none" : request.MovieConcessionId);

    private static string MovieObservedEffect(ActiveMovieTheater? active)
    {
        var guest = active?.Guest;
        var friendship = guest is not null && Game1.player.friendshipData.TryGetValue(guest.Name, out var row)
            ? row.Points.ToString(CultureInfo.InvariantCulture)
            : "none";
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";tickets=" + CountInventoryItems("(O)809").ToString(CultureInfo.InvariantCulture) +
            ";money=" + Game1.player.Money.ToString(CultureInfo.InvariantCulture) +
            ";player_last_seen_week=" + Game1.player.lastSeenMovieWeek.Value.ToString(CultureInfo.InvariantCulture) +
            ";guest_last_seen_week=" + (guest?.lastSeenMovieWeek.Value.ToString(CultureInfo.InvariantCulture) ?? "none") +
            ";friendship=" + friendship +
            ";event=" + (Game1.CurrentEvent?.id ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }

    private static SimulatedFactChange[] MovieChangedFacts(ActiveMovieTheater active)
    {
        var changes = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "player.inventory.(O)809.count",
                Before = active.TicketCountBefore.ToString(CultureInfo.InvariantCulture),
                After = CountInventoryItems("(O)809").ToString(CultureInfo.InvariantCulture)
            },
            new()
            {
                Path = "player.money",
                Before = active.MoneyBefore.ToString(CultureInfo.InvariantCulture),
                After = Game1.player.Money.ToString(CultureInfo.InvariantCulture)
            },
            new()
            {
                Path = "player.last_seen_movie_week",
                Before = active.PlayerLastSeenWeekBefore.ToString(CultureInfo.InvariantCulture),
                After = Game1.player.lastSeenMovieWeek.Value.ToString(CultureInfo.InvariantCulture)
            }
        };
        if (active.Guest is not null)
        {
            changes.Add(new SimulatedFactChange
            {
                Path = "npc." + active.Guest.Name + ".last_seen_movie_week",
                Before = active.GuestLastSeenWeekBefore?.ToString(CultureInfo.InvariantCulture) ?? "missing",
                After = active.Guest.lastSeenMovieWeek.Value.ToString(CultureInfo.InvariantCulture)
            });
            changes.Add(new SimulatedFactChange
            {
                Path = "player.friendship." + active.Guest.Name + ".points",
                Before = active.FriendshipBefore?.ToString(CultureInfo.InvariantCulture) ?? "missing",
                After = Game1.player.friendshipData.TryGetValue(active.Guest.Name, out var friendship)
                    ? friendship.Points.ToString(CultureInfo.InvariantCulture)
                    : "missing"
            });
        }
        return changes.ToArray();
    }
}
