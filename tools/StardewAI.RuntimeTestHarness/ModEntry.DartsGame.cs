using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeDartsNativeContract =
        "IslandSouthEastCave_DartsGame_checkAction_then_yes_then_native_Darts_mouse_aim_charge_release_then_native_limited_nut_drop";

    private static readonly (int Sector, float Radius)[] DartsPerfectTargets =
    {
        (0, 50f), (0, 50f), (0, 50f), (0, 50f), (9, 50f), (19, 83.5f)
    };

    private void StartDartsGame(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!DartsGameRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_darts",
                "darts_limited_nut_drop_delta=1;native_score=0;throws<=6", DartsGameObservedEffect(null),
                "darts_game_typed_request_required"));
            return;
        }
        if (activeDartsGame is not null || HasActiveExecutorOperation() || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_darts",
                "darts_limited_nut_drop_delta=1;native_score=0;throws<=6", DartsGameObservedEffect(null),
                "darts_game_player_busy"));
            return;
        }

        var location = Game1.currentLocation as IslandSouthEastCave;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var action = location?.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
        var dropped = Game1.player.team.GetDroppedLimitedNutCount("Darts");
        var expectedDarts = dropped switch { 1 => 15, 2 => 10, _ => 20 };
        var exact = location is not null && location.NameOrUniqueName == "IslandSouthEastCave" &&
            request.LocationId == "IslandSouthEastCave" && IslandSouthEastCave.isPirateNight() &&
            dropped == request.DartsLimitedNutDroppedBefore && dropped < 3 &&
            expectedDarts == request.DartsStartingDartCount &&
            string.Equals(action, request.DartsActionRaw, StringComparison.Ordinal) &&
            AreAdjacent(stand, interaction) && IsTileOnMap(location, stand) &&
            IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand);
        if (!exact)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_darts",
                "darts_limited_nut_drop_delta=1;native_score=0;throws<=6", DartsGameObservedEffect(null),
                "darts_game_endpoint_or_transparent_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location!, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_darts",
                "route=darts_game_stand", DartsGameObservedEffect(null),
                "darts_game_path_unavailable:" + pathReason));
            return;
        }
        activeDartsGame = new ActiveDartsGame(pending, location!, interaction, stand, path, maxMovementTiles);
    }

    private static bool DartsGameRequestIsTyped(TrainingExecutionRequest request) =>
        request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue &&
        request.LocationId == "IslandSouthEastCave" && request.DartsActionRaw == "DartsGame" &&
        request.DartsActionToken == "DartsGame" && request.DartsYesResponseKey == "Yes" &&
        request.DartsLimitedNutKey == "Darts" && request.DartsLimitedNutLimit == 3 &&
        request.DartsLimitedNutDroppedBefore is >= 0 and < 3 &&
        request.DartsLimitedNutDroppedAfter == request.DartsLimitedNutDroppedBefore + 1 &&
        request.DartsStartingDartCount == (request.DartsLimitedNutDroppedBefore switch { 1 => 15, 2 => 10, _ => 20 }) &&
        request.DartsStartingPoints == 301 && request.DartsPerfectVictoryMaxThrows == 6 &&
        request.DartsPerfectScorePlan == "T20,T20,T20,T20,T17,D5" &&
        Math.Abs(request.DartsChargeReleaseThreshold.GetValueOrDefault() - 0.02d) < 0.0001d &&
        !string.IsNullOrWhiteSpace(request.DartsProjectionFingerprint) &&
        request.NativeContract == RuntimeDartsNativeContract;

    private void TickDartsGame()
    {
        var active = activeDartsGame;
        if (active is null)
            return;
        if (active.Stage == DartsGameStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "darts_game", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockDartsGame(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenDartsGameDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockDartsGame(active, "darts_game_timeout");
            return;
        }
        switch (active.Stage)
        {
            case DartsGameStage.WaitDialogue:
                TickDartsGameDialogue(active);
                break;
            case DartsGameStage.WaitMinigame:
                TickDartsGameStart(active);
                break;
            case DartsGameStage.Play:
                TickDartsGamePlay(active);
                break;
            case DartsGameStage.WaitResultDialogue:
                TickDartsGameResultDialogue(active);
                break;
            case DartsGameStage.Verify:
                CompleteDartsGame(active);
                break;
        }
    }

    private void OpenDartsGameDialogue(ActiveDartsGame active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        var checkActionReturned = active.Location.checkAction(
            new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
            Game1.viewport, Game1.player);
        active.NativeCheckActionHandled = checkActionReturned || Game1.activeClickableMenu is DialogueBox;
        if (!active.NativeCheckActionHandled)
        {
            BlockDartsGame(active, "darts_game_native_check_action_rejected");
            return;
        }
        active.Stage = DartsGameStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickDartsGameDialogue(ActiveDartsGame active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockDartsGame(active, "darts_game_dialogue_open_timeout");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses,
            response => string.Equals(response.responseKey, active.Pending.Request.DartsYesResponseKey, StringComparison.OrdinalIgnoreCase));
        if (responseIndex < 0 || menu.transitioning || menu.safetyTimer > 0 ||
            menu.responseCC is null || responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockDartsGame(active, "darts_game_yes_response_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        active.NativeYesObserved = true;
        active.Stage = DartsGameStage.WaitMinigame;
        active.StageTicks = 0;
    }

    private void TickDartsGameStart(ActiveDartsGame active)
    {
        if (Game1.currentMinigame is Darts game)
        {
            if (game.minigameId() != "Darts" || game.startingDartCount != active.Pending.Request.DartsStartingDartCount ||
                game.dartCount != game.startingDartCount || game.points != 301)
            {
                BlockDartsGame(active, "darts_game_native_instance_contract_mismatch");
                return;
            }
            active.Game = game;
            active.Stage = DartsGameStage.Play;
            active.StageTicks = 0;
            return;
        }
        if (Game1.currentMinigame is not null || active.StageTicks > 360)
            BlockDartsGame(active, "darts_game_native_start_timeout_or_wrong_minigame");
    }

    private void TickDartsGamePlay(ActiveDartsGame active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            ReleaseDartsGameInput(active);
            active.FinalPoints = game.points;
            active.CompletedThrows = game.throwsCount;
            active.PerfectVictory = game.IsPerfectVictory();
            active.Stage = DartsGameStage.WaitResultDialogue;
            active.StageTicks = 0;
            return;
        }
        active.CompletedThrows = game.throwsCount;
        if (game.currentGameState == Darts.GameState.Firing && active.ShotTrace.Count < game.throwsCount)
        {
            var mouse = Game1.getMousePosition();
            active.ShotTrace.Add(
                "throw=" + game.throwsCount +
                ",cursor=" + game.cursorPosition.X.ToString("0.00", CultureInfo.InvariantCulture) + ":" + game.cursorPosition.Y.ToString("0.00", CultureInfo.InvariantCulture) +
                ",aim=" + game.aimPosition.X.ToString("0.00", CultureInfo.InvariantCulture) + ":" + game.aimPosition.Y.ToString("0.00", CultureInfo.InvariantCulture) +
                ",charge=" + game.chargeTime.ToString("0.000", CultureInfo.InvariantCulture) +
                ",scale=" + game.pixelScale.ToString("0.00", CultureInfo.InvariantCulture) +
                ",upper=" + game.upperLeft.X.ToString("0.00", CultureInfo.InvariantCulture) + ":" + game.upperLeft.Y.ToString("0.00", CultureInfo.InvariantCulture) +
                ",mouse=" + mouse.X + ":" + mouse.Y);
        }
        if (game.currentGameState == Darts.GameState.ShowScore && active.HitScores.Count < game.throwsCount)
            active.HitScores.Add(game.lastHitAmount);
        if (game.throwsCount > 6 || game.points < 0)
            BlockDartsGame(active, "darts_game_native_score_plan_drifted");
    }

    private bool ApplyDartsGameInput(ActiveDartsGame active, out string reason)
    {
        reason = string.Empty;
        if (active.Stage != DartsGameStage.Play || active.Game is null ||
            !ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            ReleaseDartsGameInput(active);
            return true;
        }
        var game = active.Game;
        var pressed = false;
        if (game.currentGameState == Darts.GameState.Aiming)
        {
            if (game.throwsCount >= DartsPerfectTargets.Length)
            {
                reason = "darts_game_perfect_target_sequence_exhausted";
                return false;
            }
            var target = DartsPerfectAim(game, DartsPerfectTargets[game.throwsCount]);
            if (active.AimTargetThrowIndex != game.throwsCount)
            {
                active.AimTargetThrowIndex = game.throwsCount;
                active.AimSettlingTicks = 0;
            }
            var observedWobble = active.AimSettlingTicks == 0
                ? Vector2.Zero
                : game.aimPosition - game.cursorPosition;
            var cursor = target - observedWobble;
            Game1.setMousePosition(Utility.Vector2ToPoint(game.TransformDraw(cursor)));
            pressed = active.AimSettlingTicks >= 2 && Vector2.DistanceSquared(game.aimPosition, target) <= 4f;
            active.AimSettlingTicks++;
        }
        else if (game.currentGameState == Darts.GameState.Charging)
        {
            pressed = !(game.chargeDirection < 0f &&
                game.chargeTime <= active.Pending.Request.DartsChargeReleaseThreshold.GetValueOrDefault(0.02d));
        }
        active.InputPressed = pressed;
        return TryApplySmapiLeftButtonOverride(pressed, out reason);
    }

    private static Vector2 DartsPerfectAim(Darts game, (int Sector, float Radius) target)
    {
        var angle = MathHelper.ToRadians(90f + target.Sector * 18f);
        var boardVector = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * target.Radius;
        return game.dartBoardCenter - boardVector;
    }

    private void TickDartsGameResultDialogue(ActiveDartsGame active)
    {
        if (Game1.currentMinigame is not null)
            return;
        if (Game1.activeClickableMenu is DialogueBox menu)
        {
            active.StableCompletionTicks = 0;
            if (!menu.transitioning && menu.safetyTimer <= 0 && active.StageTicks % 12 == 0)
            {
                menu.receiveLeftClick(Game1.viewport.Width / 2, Game1.viewport.Height / 2);
                active.ResultDialogueClicks++;
            }
            if (active.StageTicks > 1200)
                BlockDartsGame(active, "darts_game_result_dialogue_close_timeout");
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            active.StableCompletionTicks = 0;
            if (active.StageTicks > 1200)
                BlockDartsGame(active, "darts_game_unexpected_result_menu");
            return;
        }
        if (Game1.player.team.GetDroppedLimitedNutCount("Darts") == active.Pending.Request.DartsLimitedNutDroppedAfter &&
            Game1.player.CanMove && !Game1.player.UsingTool)
        {
            active.StableCompletionTicks++;
            if (active.StableCompletionTicks >= 240)
                active.Stage = DartsGameStage.Verify;
        }
        else if (active.StageTicks > 1200)
            BlockDartsGame(active, "darts_game_limited_nut_drop_timeout");
    }

    private void CompleteDartsGame(ActiveDartsGame active)
    {
        var request = active.Pending.Request;
        var verified = active.NativeCheckActionHandled && active.NativeYesObserved &&
            active.FinalPoints == 0 && active.CompletedThrows <= request.DartsPerfectVictoryMaxThrows &&
            active.PerfectVictory && Game1.player.team.GetDroppedLimitedNutCount("Darts") == request.DartsLimitedNutDroppedAfter &&
            Game1.currentMinigame is null && Game1.activeClickableMenu is null && !Game1.dialogueUp;
        ReleaseDartsGameInput(active);
        activeDartsGame = null;
        var reasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "native_IslandSouthEastCave_DartsGame_action_and_yes_constructed_projected_allowance",
                "native_mouse_aim_and_charge_release_completed_301_in_at_most_six_throws",
                "native_result_dialogue_and_FarmerTeam_limited_nut_drop_advanced_exactly_one",
                "score_reward_and_minigame_cleanup_receipt_verified"
            }
            : new[] { "darts_game_native_receipt_mismatch" };
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "play_darts",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "darts_limited_nut_drop_delta=1;native_score=0;throws<=6;session_closed=true",
            ObservedEffect = DartsGameObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : reasons
        });
    }

    private void BlockDartsGame(ActiveDartsGame active, string reason)
    {
        ReleaseDartsGameInput(active);
        if (ReferenceEquals(Game1.currentMinigame, active.Game))
            active.Game?.forceQuit();
        activeDartsGame = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_darts", "darts_limited_nut_drop_delta=1;native_score=0;throws<=6",
            DartsGameObservedEffect(active), reason));
    }

    private void ReleaseDartsGameInput(ActiveDartsGame active)
    {
        active.InputPressed = false;
        TryApplySmapiLeftButtonOverride(pressed: false, out _);
    }

    private static string DartsGameObservedEffect(ActiveDartsGame? active) =>
        "limited_nut_dropped=" + Game1.player.team.GetDroppedLimitedNutCount("Darts").ToString(CultureInfo.InvariantCulture) +
        ";points=" + (active?.FinalPoints ?? active?.Game?.points ?? -1).ToString(CultureInfo.InvariantCulture) +
        ";throws=" + (active?.CompletedThrows ?? active?.Game?.throwsCount ?? 0).ToString(CultureInfo.InvariantCulture) +
        ";perfect=" + (active?.PerfectVictory ?? active?.Game?.IsPerfectVictory() ?? false).ToString().ToLowerInvariant() +
        ";hits=" + string.Join(",", active?.HitScores ?? new List<int>()) +
        ";shot_trace=" + string.Join("|", active?.ShotTrace ?? new List<string>()) +
        ";result_dialogue_clicks=" + (active?.ResultDialogueClicks ?? 0).ToString(CultureInfo.InvariantCulture) +
        ";stable_completion_ticks=" + (active?.StableCompletionTicks ?? 0).ToString(CultureInfo.InvariantCulture) +
        ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
        ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
}
