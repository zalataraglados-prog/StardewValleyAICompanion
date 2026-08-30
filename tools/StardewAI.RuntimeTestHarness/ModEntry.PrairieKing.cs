using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimePrairieKingEquivalentContract =
        "Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement";

    private void StartPrairieKing(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!PrairieKingRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_prairie_king",
                "prairie_king=timed_equivalent_complete_without_dying", PrairieKingObservedEffect(null),
                "prairie_king_typed_request_required"));
            return;
        }
        if (activePrairieKing is not null || HasActiveExecutorOperation() || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_prairie_king",
                "prairie_king=timed_equivalent_complete_without_dying", PrairieKingObservedEffect(null),
                "prairie_king_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var action = location?.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
        var exact = location?.NameOrUniqueName == "Saloon" && request.LocationId == "Saloon" &&
            Game1.player.stats.Get("completedPrairieKing") == request.PrairieKingCompletedBefore &&
            Game1.player.stats.Get("completedPrairieKingWithoutDying") == request.PrairieKingCompletedWithoutDyingBefore &&
            request.PrairieKingCompletedWithoutDyingBefore == 0 &&
            string.Equals(action, request.PrairieKingActionRaw, StringComparison.Ordinal) &&
            AreAdjacent(stand, interaction) && IsTileOnMap(location, stand) &&
            IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand);
        if (!exact)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_prairie_king",
                "prairie_king=timed_equivalent_complete_without_dying", PrairieKingObservedEffect(null),
                "prairie_king_endpoint_or_transparent_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location!, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_prairie_king",
                "route=prairie_king_arcade_stand", PrairieKingObservedEffect(null),
                "prairie_king_path_unavailable:" + pathReason));
            return;
        }

        activePrairieKing = new ActivePrairieKing(
            pending, location!, interaction, stand, path, maxMovementTiles);
    }

    private static bool PrairieKingRequestIsTyped(TrainingExecutionRequest request) =>
        request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue &&
        request.LocationId == "Saloon" && request.MinigameId == "PrairieKing" &&
        request.PrairieKingActionRaw == "Arcade_Prairie" &&
        request.PrairieKingActionToken == "Arcade_Prairie" &&
        request.PrairieKingCompletedBefore is >= 0 &&
        request.PrairieKingCompletedWithoutDyingBefore == 0 &&
        request.PrairieKingCompletionGoal == "complete_without_dying" &&
        request.PrairieKingEquivalentDurationTicks == 108000 &&
        request.PrairieKingEquivalentAcceleration == 60 &&
        request.PrairieKingEquivalentContract == RuntimePrairieKingEquivalentContract &&
        !string.IsNullOrWhiteSpace(request.PrairieKingProjectionFingerprint);

    private void TickPrairieKing()
    {
        var active = activePrairieKing;
        if (active is null)
            return;
        if (active.Stage == PrairieKingStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "prairie_king", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockPrairieKing(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenPrairieKing(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockPrairieKing(active, "prairie_king_timeout");
            return;
        }
        switch (active.Stage)
        {
            case PrairieKingStage.WaitNativeStart:
                TickPrairieKingStart(active);
                break;
            case PrairieKingStage.EquivalentSession:
                TickPrairieKingEquivalentSession(active);
                break;
            case PrairieKingStage.WaitNativeSettlement:
                TickPrairieKingSettlement(active);
                break;
            case PrairieKingStage.Verify:
                CompletePrairieKing(active);
                break;
        }
    }

    private void OpenPrairieKing(ActivePrairieKing active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        active.NativeEntryObserved = active.Location.checkAction(
            new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
            Game1.viewport,
            Game1.player);
        if (!active.NativeEntryObserved && Game1.currentMinigame is not AbigailGame &&
            Game1.activeClickableMenu is not DialogueBox)
        {
            BlockPrairieKing(active, "prairie_king_native_check_action_rejected");
            return;
        }
        active.Stage = PrairieKingStage.WaitNativeStart;
        active.StageTicks = 0;
    }

    private void TickPrairieKingStart(ActivePrairieKing active)
    {
        if (Game1.currentMinigame is AbigailGame game)
        {
            if (game.minigameId() != "PrairieKing" || AbigailGame.playingWithAbigail)
            {
                BlockPrairieKing(active, "prairie_king_native_instance_contract_mismatch");
                return;
            }
            active.Game = game;
            AbigailGame.onStartMenu = true;
            AbigailGame.startTimer = int.MaxValue;
            active.Stage = PrairieKingStage.EquivalentSession;
            active.StageTicks = 0;
            return;
        }

        if (Game1.activeClickableMenu is DialogueBox menu)
        {
            if (!active.HadSavedProgress || active.NativeNewGameObserved ||
                active.Location.lastQuestionKey != active.Pending.Request.PrairieKingDialogueKey)
            {
                if (active.StageTicks > 180)
                    BlockPrairieKing(active, "prairie_king_unexpected_dialogue_branch");
                return;
            }
            var responseIndex = Array.FindIndex(menu.responses, response =>
                string.Equals(response.responseKey,
                    active.Pending.Request.PrairieKingDialogueResponseKey,
                    StringComparison.Ordinal));
            if (responseIndex < 0 || menu.transitioning || menu.safetyTimer > 0 ||
                menu.responseCC is null || responseIndex >= menu.responseCC.Count)
            {
                if (active.StageTicks > 180)
                    BlockPrairieKing(active, "prairie_king_new_game_response_not_clickable_timeout");
                return;
            }
            var bounds = menu.responseCC[responseIndex].bounds;
            menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
            menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
            active.NativeNewGameObserved = true;
            active.StageTicks = 0;
            return;
        }

        if (Game1.currentMinigame is not null || active.StageTicks > 360)
            BlockPrairieKing(active, "prairie_king_native_start_timeout_or_wrong_minigame");
    }

    private void TickPrairieKingEquivalentSession(ActivePrairieKing active)
    {
        if (!ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            BlockPrairieKing(active, "prairie_king_minigame_closed_before_equivalent_timer_elapsed");
            return;
        }
        AbigailGame.onStartMenu = true;
        AbigailGame.startTimer = int.MaxValue;
        active.EquivalentElapsedTicks = Math.Min(
            active.Pending.Request.PrairieKingEquivalentDurationTicks!.Value,
            active.EquivalentElapsedTicks + active.Pending.Request.PrairieKingEquivalentAcceleration!.Value);
        if (active.EquivalentElapsedTicks < active.Pending.Request.PrairieKingEquivalentDurationTicks)
            return;
        if (active.Game!.died)
        {
            BlockPrairieKing(active, "prairie_king_equivalent_session_died_flag_drifted");
            return;
        }

        AbigailGame.onStartMenu = false;
        AbigailGame.startTimer = 0;
        active.Game!.usePowerup(-3);
        active.NativeCompletionTriggerInvoked = true;
        active.Stage = PrairieKingStage.WaitNativeSettlement;
        active.StageTicks = 0;
    }

    private void TickPrairieKingSettlement(ActivePrairieKing active)
    {
        if (!ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            BlockPrairieKing(active, "prairie_king_minigame_closed_before_native_settlement");
            return;
        }
        var completed = Game1.player.stats.Get("completedPrairieKing");
        var withoutDying = Game1.player.stats.Get("completedPrairieKingWithoutDying");
        if (completed == active.CompletedBefore + 1 &&
            withoutDying == active.CompletedWithoutDyingBefore + 1 &&
            Game1.player.hasOrWillReceiveMail("Beat_PK"))
        {
            active.Stage = PrairieKingStage.Verify;
            active.StageTicks = 0;
            return;
        }
        if (active.StageTicks > 600)
            BlockPrairieKing(active, "prairie_king_native_phase1_settlement_timeout_or_receipt_mismatch");
    }

    private void CompletePrairieKing(ActivePrairieKing active)
    {
        var request = active.Pending.Request;
        var completed = Game1.player.stats.Get("completedPrairieKing");
        var withoutDying = Game1.player.stats.Get("completedPrairieKingWithoutDying");
        CleanupPrairieKing(active);
        activePrairieKing = null;
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
            PrimitiveKind = "play_prairie_king",
            PrimitiveVerificationStatus = "simulated_equivalent",
            PrimitiveVerificationReasons = new[]
            {
                "ai_actor_timed_equivalent_elapsed",
                "native_Arcade_Prairie_entry_observed",
                "native_start_menu_suspended_during_equivalent_session",
                "native_AbigailGame_usePowerup_minus3_completion_trigger_invoked",
                "native_phase1_completion_no_death_stats_mail_and_achievement_checks_observed",
                "not_native_perfect_proxy_play"
            },
            RequestedEffect = "prairie_king=timed_equivalent_complete_without_dying",
            ObservedEffect = PrairieKingObservedEffect(active.Game) +
                ";equivalent_duration_ticks=" + request.PrairieKingEquivalentDurationTicks +
                ";wall_ticks=" + active.ElapsedTicks +
                ";acceleration=" + request.PrairieKingEquivalentAcceleration,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.stats.completedPrairieKing",
                    Before = active.CompletedBefore.ToString(CultureInfo.InvariantCulture),
                    After = completed.ToString(CultureInfo.InvariantCulture)
                },
                new SimulatedFactChange
                {
                    Path = "player.stats.completedPrairieKingWithoutDying",
                    Before = active.CompletedWithoutDyingBefore.ToString(CultureInfo.InvariantCulture),
                    After = withoutDying.ToString(CultureInfo.InvariantCulture)
                },
                new SimulatedFactChange
                {
                    Path = "minigame.prairie_king.execution_strategy",
                    Before = "pending",
                    After = "timed_equivalent"
                }
            }
        });
    }

    private void BlockPrairieKing(ActivePrairieKing active, params string[] reasons)
    {
        CleanupPrairieKing(active);
        activePrairieKing = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "play_prairie_king",
            "prairie_king=timed_equivalent_complete_without_dying",
            PrairieKingObservedEffect(active.Game),
            reasons));
    }

    private static void CleanupPrairieKing(ActivePrairieKing active)
    {
        if (!ReferenceEquals(Game1.currentMinigame, active.Game) || active.Game is null)
            return;
        if (active.Game.forceQuit())
            Game1.currentMinigame = null;
    }

    private static string PrairieKingObservedEffect(AbigailGame? game) =>
        "minigame=" + (game?.minigameId() ?? Game1.currentMinigame?.minigameId() ?? "none") +
        ";completed=" + Game1.player.stats.Get("completedPrairieKing") +
        ";completed_without_dying=" + Game1.player.stats.Get("completedPrairieKingWithoutDying") +
        ";beat_pk_mail=" + Game1.player.hasOrWillReceiveMail("Beat_PK") +
        ";end_cutscene_phase=" + AbigailGame.endCutscenePhase;
}
