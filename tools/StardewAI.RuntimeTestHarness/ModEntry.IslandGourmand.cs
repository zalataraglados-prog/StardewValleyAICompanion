using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string GourmandRuntimeNativeContract =
        "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward";

    private void StartGourmandRequest(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!GourmandRequestIsTyped(request) || activeGourmandRequest is not null ||
            HasActiveExecutorOperation() || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(GourmandBlocked(
                request,
                GourmandRequestIsTyped(request)
                    ? "gourmand_player_busy"
                    : "gourmand_typed_projection_required"));
            return;
        }
        if (!Game1.IsMasterGame || Game1.currentLocation is not IslandFarmCave cave ||
            !string.Equals(cave.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            cave.gourmand is null || GourmandMutex(cave) is not { } mutex || mutex.IsLocked())
        {
            pending.Completion.SetResult(GourmandBlocked(
                request,
                "gourmand_location_host_npc_or_mutex_unavailable"));
            return;
        }

        var action = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        if (!AreAdjacent(action, stand) ||
            cave.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != request.GourmandAction ||
            request.GourmandAction != "Gourmand" || !IsTileOnMap(cave, stand) ||
            !IsTileWalkable(cave, stand) || IsTileOccupiedByCharacter(cave, stand) ||
            !GourmandLiveProjectionMatches(request, cave))
        {
            pending.Completion.SetResult(GourmandBlocked(
                request,
                "gourmand_endpoint_or_projection_drifted"));
            return;
        }
        var path = TryBuildTilePath(
            cave,
            Game1.player.TilePoint,
            stand,
            request.MaxMovementTiles ?? 512,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(GourmandBlocked(
                request,
                "gourmand_path_unavailable:" + pathReason));
            return;
        }
        activeGourmandRequest = new ActiveGourmandRequest(pending, cave, action, stand, path);
    }

    private void TickGourmandRequest()
    {
        var active = activeGourmandRequest;
        if (active is null)
            return;
        try
        {
            CaptureGourmandWalnutDebris(active);
            if (++active.ElapsedTicks > 7200 ||
                (!active.ActionIssued && !ReferenceEquals(Game1.currentLocation, active.Cave)))
            {
                CompleteGourmandBlocked(active, "gourmand_world_location_or_timeout");
                return;
            }
            if (!active.ActionIssued && Game1.player.TilePoint != active.StandTile)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteGourmandBlocked(active, "gourmand_path_exhausted");
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
                    CompleteGourmandBlocked(active, "gourmand_movement_stuck");
                return;
            }

            StopAllMovement();
            if (active.Cooldown-- > 0)
                return;
            var request = active.Pending.Request;
            if (!active.ActionIssued)
            {
                if (!GourmandLiveProjectionMatches(request, active.Cave))
                {
                    CompleteGourmandBlocked(active, "gourmand_preopen_projection_drifted");
                    return;
                }
                Game1.player.faceDirection(DirectionTo(active.StandTile, active.ActionTile));
                var handled = active.Cave.checkAction(
                    new xTile.Dimensions.Location(active.ActionTile.X, active.ActionTile.Y),
                    new xTile.Dimensions.Rectangle(
                        Game1.viewport.X,
                        Game1.viewport.Y,
                        Game1.viewport.Width,
                        Game1.viewport.Height),
                    Game1.player);
                if (!handled)
                {
                    CompleteGourmandBlocked(active, "gourmand_action_not_handled");
                    return;
                }
                active.ActionIssued = true;
                active.Cooldown = 8;
                return;
            }

            if (active.Completing && active.ActionIssued &&
                (Game1.eventUp || Game1.currentLocation?.currentEvent is not null ||
                 !ReferenceEquals(Game1.currentLocation, active.Cave)))
                active.SawNativeEvent = true;

            if (active.Completing && !active.YesAnswered &&
                Game1.activeClickableMenu is DialogueBox question && question.isQuestion &&
                string.Equals(Game1.currentLocation?.lastQuestionKey, request.GourmandQuestionKey, StringComparison.Ordinal))
            {
                if (question.transitioning || question.safetyTimer > 0)
                    return;
                var responseIndex = Array.FindIndex(
                    question.responses,
                    response => response.responseKey == request.GourmandResponseKey);
                if (responseIndex < 0)
                {
                    CompleteGourmandBlocked(active, "gourmand_yes_response_missing");
                    return;
                }
                var response = StoryEventResponseCenter(question, responseIndex);
                question.performHoverAction(response.X, response.Y);
                if (question.selectedResponse != responseIndex)
                {
                    CompleteGourmandBlocked(active, "gourmand_yes_response_not_bound");
                    return;
                }
                question.receiveLeftClick(response.X, response.Y);
                active.YesAnswered = true;
                active.Cooldown = 8;
                return;
            }

            if (Game1.activeClickableMenu is DialogueBox dialogue)
            {
                if (++active.DialogueTicks % 12 != 0 || dialogue.transitioning ||
                    dialogue.safetyTimer > 0)
                    return;
                if (dialogue.characterIndexInDialogue < dialogue.getCurrentString().Length - 1)
                {
                    dialogue.receiveLeftClick(0, 0);
                    return;
                }
                if (dialogue.isQuestion)
                {
                    CompleteGourmandBlocked(active, "gourmand_event_unexpected_question");
                    return;
                }
                dialogue.receiveLeftClick(0, 0);
                return;
            }
            if (Game1.activeClickableMenu is not null)
                return;

            CaptureGourmandWalnutDebris(active);
            if (!GourmandPostconditionsMatch(active))
            {
                if (++active.SettlementWaitTicks <= 3600)
                    return;
                CompleteGourmandBlocked(active, "gourmand_native_settlement_mismatch");
                return;
            }
            CompleteGourmandRequestExecution(active);
        }
        catch (Exception ex)
        {
            CompleteGourmandBlocked(
                active,
                "gourmand_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void CompleteGourmandRequestExecution(ActiveGourmandRequest active)
    {
        StopAllMovement();
        activeGourmandRequest = null;
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
            PrimitiveKind = "advance_gourmand_request",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = active.Completing
                ? new[]
                {
                    "native_Gourmand_Yes_response_completed",
                    "native_crop_event_and_return_observed",
                    "native_five_walnut_debris_spawn_and_pickup_verified",
                    "request_tracker_and_collected_nut_receipt_verified"
                }
                : new[]
                {
                    "native_Gourmand_introduction_dialogues_completed",
                    "talkedToGourmand_pending_mail_verified",
                    "request_index_unchanged_verified"
                },
            RequestedEffect = GourmandRequestedEffect(request),
            ObservedEffect = GourmandObservedEffect(active.Cave),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = active.Completing ? 1800 : 600,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Cave.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            ChangedFacts = GourmandChangedFacts(request, active.Cave)
        });
    }

    private void CompleteGourmandBlocked(ActiveGourmandRequest active, string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is DialogueBox dialogue && !Game1.eventUp)
            dialogue.exitThisMenuNoSound();
        activeGourmandRequest = null;
        active.Pending.Completion.SetResult(GourmandBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult GourmandBlocked(
        TrainingExecutionRequest request,
        string reason) =>
        BlockedWithPrimitive(
            request,
            "advance_gourmand_request",
            GourmandRequestedEffect(request),
            GourmandObservedEffect(Game1.getLocationFromName("IslandFarmCave") as IslandFarmCave),
            reason);
}
