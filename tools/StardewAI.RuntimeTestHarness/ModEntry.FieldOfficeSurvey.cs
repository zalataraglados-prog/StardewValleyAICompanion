using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FieldOfficeSurveyNativeContract =
        "FieldOfficeSurvey_then_Survey_Yes_then_exact_Correct_response_then_native_plant_nut_debris_and_finale";

    private void StartFieldOfficeSurvey(PendingExecution pending, bool intentionallyWrong = false)
    {
        var request = pending.Request;
        if (!intentionallyWrong)
        {
            var reasons = ValidateExecutionRequest(request);
            if (reasons.Count > 0)
            {
                pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
                return;
            }
        }
        if (!FieldOfficeSurveyRequestIsTyped(request, intentionallyWrong) || activeFieldOfficeSurvey is not null ||
            HasActiveExecutorOperation() || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FieldOfficeSurveyBlocked(request,
                FieldOfficeSurveyRequestIsTyped(request, intentionallyWrong)
                    ? "field_office_survey_player_busy"
                    : "field_office_survey_typed_projection_required"));
            return;
        }
        if (Game1.currentLocation is not IslandFieldOffice office ||
            !string.Equals(office.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            office.getSafariGuy() is null || office.safariGuyMutex.IsLocked())
        {
            pending.Completion.SetResult(FieldOfficeSurveyBlocked(request, "field_office_survey_location_professor_or_mutex_unavailable"));
            return;
        }

        var action = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        if (!AreAdjacent(action, stand) ||
            office.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != request.FieldOfficeSurveyActionRaw ||
            request.FieldOfficeSurveyActionRaw != "FieldOfficeSurvey" || !IsTileOnMap(office, stand) ||
            !IsTileWalkable(office, stand) || IsTileOccupiedByCharacter(office, stand) ||
            !FieldOfficeSurveyLiveProjectionMatches(request, office, intentionallyWrong))
        {
            pending.Completion.SetResult(FieldOfficeSurveyBlocked(request, "field_office_survey_endpoint_or_projection_drifted"));
            return;
        }
        var path = TryBuildTilePath(office, Game1.player.TilePoint, stand, request.MaxMovementTiles ?? 512,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FieldOfficeSurveyBlocked(request, "field_office_survey_path_unavailable:" + pathReason));
            return;
        }
        activeFieldOfficeSurvey = new ActiveFieldOfficeSurvey(pending, office, action, stand, path, intentionallyWrong);
    }

    private void TickFieldOfficeSurvey()
    {
        var active = activeFieldOfficeSurvey;
        if (active is null)
            return;
        try
        {
            if (++active.ElapsedTicks > 2400 || !ReferenceEquals(Game1.currentLocation, active.Office))
            {
                CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_world_location_or_timeout");
                return;
            }
            if (!active.ActionIssued && Game1.player.TilePoint != active.StandTile)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_path_exhausted");
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
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_movement_stuck");
                return;
            }

            StopAllMovement();
            if (active.Cooldown-- > 0)
                return;
            var request = active.Pending.Request;
            if (!active.ActionIssued)
            {
                if (!FieldOfficeSurveyLiveProjectionMatches(request, active.Office, active.IntentionallyWrong))
                {
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_preopen_projection_drifted");
                    return;
                }
                Game1.player.faceDirection(DirectionTo(active.StandTile, active.ActionTile));
                var handled = active.Office.checkAction(
                    new xTile.Dimensions.Location(active.ActionTile.X, active.ActionTile.Y),
                    new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                if (!handled)
                {
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_action_not_handled");
                    return;
                }
                active.ActionIssued = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.PromptAnswered)
            {
                if (!TryGetFieldOfficeSurveyQuestion(request.FieldOfficeSurveyPromptQuestionKey, out var prompt))
                {
                    if (++active.QuestionWaitTicks <= 240)
                        return;
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_prompt_missing");
                    return;
                }
                var yes = prompt.responses?.FirstOrDefault(value => value.responseKey == request.FieldOfficeSurveyPromptResponseKey);
                if (yes is null || !active.Office.answerDialogue(yes))
                {
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_prompt_response_failed");
                    return;
                }
                active.PromptAnswered = true;
                active.QuestionWaitTicks = 0;
                active.Cooldown = 8;
                return;
            }
            if (!active.NumericAnswerIssued)
            {
                if (!TryGetFieldOfficeSurveyQuestion(request.FieldOfficeSurveyAnswerQuestionKey, out var question))
                {
                    if (++active.QuestionWaitTicks <= 240)
                        return;
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_numeric_question_missing");
                    return;
                }
                var response = question.responses?.FirstOrDefault(value =>
                    value.responseKey == request.FieldOfficeSurveyAnswerResponseKey &&
                    value.responseText == request.FieldOfficeSurveyAnswer!.Value.ToString());
                if (response is null || !active.Office.answerDialogue(response))
                {
                    CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_numeric_response_failed");
                    return;
                }
                active.WalnutDebrisSpawnObservedCount = active.Office.debris.Count(debris =>
                    !active.DebrisBefore.Contains(debris) && DebrisQualifiedItemId(debris) == "(O)73");
                active.NumericAnswerIssued = true;
                active.Cooldown = 8;
                return;
            }

            if (Game1.activeClickableMenu is DialogueBox resultDialogue)
            {
                if (++active.ResultDialogueTicks % 12 == 0)
                    resultDialogue.receiveLeftClick(
                        resultDialogue.xPositionOnScreen + resultDialogue.width / 2,
                        resultDialogue.yPositionOnScreen + resultDialogue.height / 2);
                return;
            }
            if (Game1.activeClickableMenu is not null)
                return;
            if (!FieldOfficeSurveyPostconditionsMatch(active))
            {
                if (++active.SettlementWaitTicks <= 300)
                    return;
                CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_native_settlement_mismatch");
                return;
            }
            CompleteFieldOfficeSurvey(active);
        }
        catch (Exception ex)
        {
            CompleteFieldOfficeSurveyBlocked(active, "field_office_survey_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private bool TryGetFieldOfficeSurveyQuestion(string expectedKey, out DialogueBox dialogue)
    {
        dialogue = Game1.activeClickableMenu as DialogueBox ?? null!;
        return dialogue is not null && dialogue.isQuestion &&
            string.Equals(Game1.currentLocation.lastQuestionKey, expectedKey, StringComparison.Ordinal);
    }

    private void CompleteFieldOfficeSurvey(ActiveFieldOfficeSurvey active)
    {
        StopAllMovement();
        activeFieldOfficeSurvey = null;
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
            PrimitiveKind = active.IntentionallyWrong ? "debug_answer_field_office_survey_wrong" : "answer_field_office_survey",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = active.IntentionallyWrong
                ? new[] { "native_FieldOfficeSurvey_prompt_completed", "native_wrong_numeric_response_completed", "failed_today_lock_verified" }
                : new[] { "native_FieldOfficeSurvey_prompt_completed", "native_exact_correct_numeric_response_completed", "native_walnut_debris_spawn_and_shared_pickup_verified", "plant_nut_walnut_and_finale_projection_verified" },
            RequestedEffect = FieldOfficeSurveyRequestedEffect(request, active.IntentionallyWrong),
            ObservedEffect = FieldOfficeSurveyObservedEffect(active.Office),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 360,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Office.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            ChangedFacts = FieldOfficeSurveyChangedFacts(request, active.Office, active.IntentionallyWrong)
        });
    }

    private void CompleteFieldOfficeSurveyBlocked(ActiveFieldOfficeSurvey active, string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is DialogueBox dialogue)
            dialogue.exitThisMenuNoSound();
        activeFieldOfficeSurvey = null;
        active.Pending.Completion.SetResult(FieldOfficeSurveyBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult FieldOfficeSurveyBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(
            request,
            request.OptionId == "debug.answer_field_office_survey_wrong"
                ? "debug_answer_field_office_survey_wrong"
                : "answer_field_office_survey",
            FieldOfficeSurveyRequestedEffect(request, request.OptionId == "debug.answer_field_office_survey_wrong"),
            FieldOfficeSurveyObservedEffect(Game1.getLocationFromName("IslandFieldOffice") as IslandFieldOffice),
            reason);
}
