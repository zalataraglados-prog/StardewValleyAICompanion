using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartJojaDevelopment(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        var membership = request.OptionId == "executor.purchase_joja_membership";
        var expectedContract = membership
            ? "JojaMart.checkAction_JoinJoja_then_signUpForJoja_then_answerDialogue_JojaSignUp_Yes"
            : "JojaMart.checkAction_JoinJoja_then_viewJojaNote_then_JojaCDMenu.receiveLeftClick_checkbox";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ExpectedMoneyBefore.HasValue || !request.Price.HasValue || !request.ExpectedMoneyAfter.HasValue ||
            request.ExpectedMoneyAfter.Value != request.ExpectedMoneyBefore.Value - request.Price.Value ||
            request.PurchaseKind != (membership ? "membership" : "project") || request.JoinActionRaw != "JoinJoja" || request.NativeContract != expectedContract ||
            membership && (request.Price.Value != JojaMart.JojaMembershipPrice || request.ExpectedMailForTomorrow != "JojaMember" || request.RequiredEventId != "611439" ||
                !request.ExpectedGreetingBefore.HasValue || request.ExpectedGreetingAfter != true) ||
            !membership && (!request.ButtonNumber.HasValue || request.ButtonNumber.Value is < 0 or > 4 || string.IsNullOrWhiteSpace(request.ProjectId) ||
                string.IsNullOrWhiteSpace(request.CcMailId) || string.IsNullOrWhiteSpace(request.JojaMailId)))
        {
            pending.Completion.SetResult(JojaBlocked(request, "joja_development_typed_projection_required"));
            return;
        }
        if (activeJojaDevelopment is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(JojaBlocked(request, "joja_development_player_busy"));
            return;
        }
        if (Game1.currentLocation is not JojaMart mart || !string.Equals(mart.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(JojaBlocked(request, "joja_development_target_location_mismatch"));
            return;
        }
        var actionTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var standTile = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!AreAdjacent(actionTile, standTile) || mart.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings") != "JoinJoja" ||
            !IsTileOnMap(mart, standTile) || !IsTileWalkable(mart, standTile) || IsTileOccupiedByCharacter(mart, standTile))
        {
            pending.Completion.SetResult(JojaBlocked(request, "joja_development_action_or_stand_tile_drifted"));
            return;
        }
        if (!JojaLivePreconditionsMatch(request, membership, mart, out var liveReason))
        {
            pending.Completion.SetResult(JojaBlocked(request, liveReason));
            return;
        }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(mart, Game1.player.TilePoint, standTile, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(JojaBlocked(request, "joja_development_path_unavailable:" + pathReason));
            return;
        }
        activeJojaDevelopment = new ActiveJojaDevelopment(pending, mart, actionTile, standTile, path, maxMovement);
    }

    private void TickJojaDevelopment()
    {
        var active = activeJojaDevelopment;
        if (active is null)
        {
            return;
        }
        try
        {
            TickJojaDevelopmentCore(active);
        }
        catch (Exception ex)
        {
            CompleteJojaBlocked(active, "joja_development_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void TickJojaDevelopmentCore(ActiveJojaDevelopment active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mart) || active.ElapsedTicks > 4200)
        {
            CompleteJojaBlocked(active, "joja_development_world_location_or_timeout");
            return;
        }
        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteJojaBlocked(active, "joja_development_path_exhausted");
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
            var tile = Game1.player.TilePoint;
            if (tile != active.LastObservedTile)
            {
                active.StuckTicks = 0;
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, tile);
                active.LastObservedTile = tile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteJojaBlocked(active, "joja_development_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteJojaBlocked(active, "joja_development_movement_stuck_or_blocked");
                return;
            }
            if (tile == next)
            {
                active.PathIndex++;
            }
            return;
        }

        StopAllMovement();
        var request = active.Pending.Request;
        var membership = request.OptionId == "executor.purchase_joja_membership";
        if (!active.OpenIssued)
        {
            if (!JojaLivePreconditionsMatch(request, membership, active.Mart, out var liveReason, active.GreetingCompleted))
            {
                CompleteJojaBlocked(active, liveReason);
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.ActionTile));
            var handled = active.Mart.checkAction(
                new xTile.Dimensions.Location(active.ActionTile.X, active.ActionTile.Y),
                new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            if (!handled && Game1.activeClickableMenu is not DialogueBox)
            {
                CompleteJojaBlocked(active, "joja_development_join_action_not_handled");
                return;
            }
            active.OpenIssued = true;
            active.DialogueCooldown = 8;
            return;
        }

        if (active.PurchaseIssued)
        {
            active.SettlementTicks++;
            if (!JojaPostconditionsMatch(request, membership))
            {
                if (active.SettlementTicks > 1260)
                {
                    CompleteJojaBlocked(active, "joja_development_native_settlement_timeout_or_mismatch");
                }
                return;
            }
            if (Game1.activeClickableMenu is DialogueBox confirmation)
            {
                if (confirmation.isQuestion)
                {
                    CompleteJojaBlocked(active, "joja_development_unexpected_post_purchase_question");
                    return;
                }
                AdvanceJojaDialogue(active, confirmation);
                return;
            }
            if (Game1.activeClickableMenu is null)
            {
                CompleteJoja(active);
            }
            return;
        }

        if (membership)
        {
            TickJojaMembershipDialogue(active);
        }
        else
        {
            TickJojaProjectMenu(active);
        }
    }

    private void TickJojaMembershipDialogue(ActiveJojaDevelopment active)
    {
        if (active.GreetingRequired && !active.GreetingCompleted)
        {
            if (!Game1.player.mailReceived.Contains("JojaGreeting"))
            {
                CompleteJojaBlocked(active, "joja_membership_native_greeting_not_observed");
                return;
            }
            if (Game1.activeClickableMenu is DialogueBox greeting)
            {
                if (greeting.isQuestion)
                {
                    CompleteJojaBlocked(active, "joja_membership_greeting_unexpected_question");
                    return;
                }
                AdvanceJojaDialogue(active, greeting);
                return;
            }
            if (Game1.activeClickableMenu is not null)
            {
                CompleteJojaBlocked(active, "joja_membership_greeting_menu_drifted");
                return;
            }
            active.GreetingCompleted = true;
            active.OpenIssued = false;
            active.DialogueCooldown = 8;
            return;
        }

        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            CompleteJojaBlocked(active, "joja_membership_expected_dialogue_missing");
            return;
        }
        if (!menu.isQuestion)
        {
            AdvanceJojaDialogue(active, menu);
            return;
        }
        if (!active.OfferResponseChosen)
        {
            if (menu.characterDialogue is null || menu.responses is null || menu.responses.Length < 1 || !menu.characterDialogue.chooseResponse(menu.responses[0]))
            {
                CompleteJojaBlocked(active, "joja_membership_offer_response_failed");
                return;
            }
            active.OfferResponseChosen = true;
            active.DialogueCooldown = 8;
            return;
        }
        if (menu.characterDialogue is not null || active.Mart.lastQuestionKey != "JojaSignUp")
        {
            CompleteJojaBlocked(active, "joja_membership_confirmation_question_drifted");
            return;
        }
        var yes = menu.responses?.FirstOrDefault(response => response.responseKey == "Yes");
        if (yes is null || !active.Mart.answerDialogue(yes))
        {
            CompleteJojaBlocked(active, "joja_membership_native_confirmation_failed");
            return;
        }
        active.PurchaseIssued = true;
        active.SettlementTicks = 0;
    }

    private void TickJojaProjectMenu(ActiveJojaDevelopment active)
    {
        if (!active.OfferResponseChosen)
        {
            if (Game1.activeClickableMenu is not DialogueBox dialogue)
            {
                CompleteJojaBlocked(active, "joja_project_expected_dialogue_missing");
                return;
            }
            if (!dialogue.isQuestion)
            {
                AdvanceJojaDialogue(active, dialogue);
                return;
            }
            if (dialogue.characterDialogue is null || dialogue.responses is null || dialogue.responses.Length < 1 ||
                !dialogue.characterDialogue.chooseResponse(dialogue.responses[0]))
            {
                CompleteJojaBlocked(active, "joja_project_form_response_failed");
                return;
            }
            active.OfferResponseChosen = true;
            active.DialogueCooldown = 8;
            return;
        }
        if (Game1.activeClickableMenu is not JojaCDMenu menu)
        {
            CompleteJojaBlocked(active, "joja_project_native_menu_open_failed");
            return;
        }
        var request = active.Pending.Request;
        var button = request.ButtonNumber!.Value;
        if (menu.checkboxes.Count != 5 || button >= menu.checkboxes.Count || menu.checkboxes[button].name == "complete" ||
            menu.getPriceFromButtonNumber(button) != request.Price || Game1.player.Money != request.ExpectedMoneyBefore)
        {
            CompleteJojaBlocked(active, "joja_project_menu_projection_drifted");
            return;
        }
        var checkbox = menu.checkboxes[button];
        menu.receiveLeftClick(checkbox.bounds.Center.X, checkbox.bounds.Center.Y);
        if (!JojaPostconditionsMatch(request, membership: false))
        {
            CompleteJojaBlocked(active, "joja_project_native_checkbox_click_failed");
            return;
        }
        active.PurchaseIssued = true;
        active.SettlementTicks = 0;
    }

    private static void AdvanceJojaDialogue(ActiveJojaDevelopment active, DialogueBox menu)
    {
        if (active.DialogueCooldown > 0)
        {
            active.DialogueCooldown--;
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0)
        {
            return;
        }
        menu.receiveLeftClick(menu.xPositionOnScreen + menu.width / 2, menu.yPositionOnScreen + menu.height / 2);
        active.DialogueCooldown = 8;
    }

    private static bool JojaLivePreconditionsMatch(TrainingExecutionRequest request, bool membership, JojaMart mart, out string reason, bool greetingCompleted = false)
    {
        reason = string.Empty;
        var hostJoja = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember");
        var hostCc = Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete") || Game1.MasterPlayer.hasCompletedCommunityCenter();
        var route = hostJoja && hostCc ? "conflicting_irreversible_flags" : hostJoja ? "joja_locked" : hostCc ? "community_center_locked" : "undecided";
        if (Game1.player.Money != request.ExpectedMoneyBefore)
        {
            reason = "joja_development_money_drifted";
            return false;
        }
        if (membership)
        {
            var greetingReceived = Game1.player.mailReceived.Contains("JojaGreeting");
            var greetingMatches = greetingCompleted ? greetingReceived : request.ExpectedGreetingBefore == greetingReceived;
            if (route != "undecided" || !Game1.IsMasterGame || Game1.player.mailReceived.Contains("JojaMember") || JojaMailPending("JojaMember") ||
                !Game1.player.eventsSeen.Contains("611439") || !greetingMatches || request.ExpectedGreetingAfter != true)
            {
                reason = "joja_membership_live_preconditions_drifted";
                return false;
            }
            return true;
        }
        if (route != "joja_locked" || !Game1.player.mailReceived.Contains("JojaMember") || Game1.player.eventsSeen.Contains("502261") ||
            JojaProjectOrderPending() || !request.ButtonNumber.HasValue ||
            !TryGetJojaProject(request.ButtonNumber.Value, out var project) || project.ProjectId != request.ProjectId || project.CcMail != request.CcMailId ||
            project.JojaMail != request.JojaMailId || project.Price != request.Price || Utility.doesAnyFarmerHaveOrWillReceiveMail(project.CcMail))
        {
            reason = "joja_project_live_preconditions_drifted";
            return false;
        }
        return true;
    }

    private static bool JojaPostconditionsMatch(TrainingExecutionRequest request, bool membership)
    {
        return Game1.player.Money == request.ExpectedMoneyAfter && (membership
            ? Game1.player.mailReceived.Contains("JojaGreeting") && JojaMailPending("JojaMember")
            : JojaMailPending(request.CcMailId) && JojaMailPending(request.JojaMailId));
    }

    private static bool JojaProjectOrderPending() =>
        new[] { "jojaVault", "jojaBoilerRoom", "jojaCraftsRoom", "jojaPantry", "jojaFishTank" }.Any(JojaMailPending);

    private static bool JojaMailPending(string mailId) => Game1.player.mailForTomorrow.Any(value =>
        string.Equals(value, mailId, StringComparison.Ordinal) || string.Equals(value, mailId + "%&NL&%", StringComparison.Ordinal));

    private static bool TryGetJojaProject(int button, out JojaProjectRuntime project)
    {
        project = button switch
        {
            0 => new("vault", "ccVault", "jojaVault", 40000),
            1 => new("boiler_room", "ccBoilerRoom", "jojaBoilerRoom", 15000),
            2 => new("crafts_room", "ccCraftsRoom", "jojaCraftsRoom", 25000),
            3 => new("pantry", "ccPantry", "jojaPantry", 35000),
            4 => new("fish_tank", "ccFishTank", "jojaFishTank", 20000),
            _ => default
        };
        return button is >= 0 and <= 4;
    }

    private void CompleteJoja(ActiveJojaDevelopment active)
    {
        activeJojaDevelopment = null;
        StopAllMovement();
        var request = active.Pending.Request;
        var membership = request.OptionId == "executor.purchase_joja_membership";
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
            PrimitiveKind = membership ? "purchase_joja_membership" : "purchase_joja_project",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = membership
                ? new[] { "JojaMart.checkAction_completed", "Dialogue.chooseResponse_signUpForJoja_completed", "JojaMart.answerDialogue_JojaSignUp_Yes_completed" }
                : new[] { "JojaMart.checkAction_completed", "Dialogue.chooseResponse_viewJojaNote_completed", "JojaCDMenu.receiveLeftClick_checkbox_completed" },
            RequestedEffect = membership ? "mail_for_tomorrow.JojaMember=true" : "mail_for_tomorrow." + request.CcMailId + "=true;mail_for_tomorrow." + request.JojaMailId + "=true",
            ObservedEffect = JojaObservedEffect(request),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 300,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Mart.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            ChangedFacts = membership
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.money", Before = request.ExpectedMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                    new SimulatedFactChange { Path = "quests.mail_received.JojaGreeting", Before = request.ExpectedGreetingBefore.ToString()!.ToLowerInvariant(), After = Game1.player.mailReceived.Contains("JojaGreeting").ToString().ToLowerInvariant() },
                    new SimulatedFactChange { Path = "quests.mail_for_tomorrow.JojaMember", Before = "false", After = JojaMailPending("JojaMember").ToString().ToLowerInvariant() }
                }
                : new[]
                {
                    new SimulatedFactChange { Path = "player.money", Before = request.ExpectedMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                    new SimulatedFactChange { Path = "quests.mail_for_tomorrow." + request.CcMailId, Before = "false", After = JojaMailPending(request.CcMailId).ToString().ToLowerInvariant() },
                    new SimulatedFactChange { Path = "quests.mail_for_tomorrow." + request.JojaMailId, Before = "false", After = JojaMailPending(request.JojaMailId).ToString().ToLowerInvariant() }
                }
        });
    }

    private void CompleteJojaBlocked(ActiveJojaDevelopment active, string reason)
    {
        StopAllMovement();
        activeJojaDevelopment = null;
        active.Pending.Completion.SetResult(JojaBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult JojaBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, request.OptionId == "executor.purchase_joja_membership" ? "purchase_joja_membership" : "purchase_joja_project",
            request.OptionId == "executor.purchase_joja_membership" ? "mail_for_tomorrow.JojaMember=true" : "mail_for_tomorrow." + request.CcMailId + "=true;mail_for_tomorrow." + request.JojaMailId + "=true",
            JojaObservedEffect(request), reason);

    private static string JojaObservedEffect(TrainingExecutionRequest request) =>
        "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";money=" + Game1.player.Money +
        ";JojaMember_pending=" + JojaMailPending("JojaMember").ToString().ToLowerInvariant() +
        ";cc_mail_pending=" + JojaMailPending(request.CcMailId).ToString().ToLowerInvariant() +
        ";joja_mail_pending=" + JojaMailPending(request.JojaMailId).ToString().ToLowerInvariant();

    private readonly record struct JojaProjectRuntime(string ProjectId, string CcMail, string JojaMail, int Price);
}
