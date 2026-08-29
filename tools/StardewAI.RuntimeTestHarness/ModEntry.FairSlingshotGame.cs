using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeFairSlingshotNativeContract =
        "Event.checkAction(festival_fall16_buildings_501_502)->DialogueBox(slingshotGame:Play).receiveLeftClick->Event.answerDialogue(slingshotGame,0)->Money-50->globalFadeToBlack(TargetGame.startMe)->native_50000ms_TargetGame_input_session->accuracy_multiplier_score_reward->festivalScore";

    private static readonly FieldInfo? RuntimeFairSlingshotLocationField = RuntimePrivateField<TargetGame>("location");
    private static readonly FieldInfo? RuntimeFairSlingshotTimerToStartField = RuntimePrivateField<TargetGame>("timerToStart");
    private static readonly FieldInfo? RuntimeFairSlingshotGameEndTimerField = RuntimePrivateField<TargetGame>("gameEndTimer");
    private static readonly FieldInfo? RuntimeFairSlingshotShowResultsTimerField = RuntimePrivateField<TargetGame>("showResultsTimer");
    private static readonly FieldInfo? RuntimeFairSlingshotGameDoneField = RuntimePrivateField<TargetGame>("gameDone");
    private static readonly FieldInfo? RuntimeFairSlingshotModifierBonusField = RuntimePrivateField<TargetGame>("modifierBonus");
    private static readonly FieldInfo? RuntimeFairTargetTypeField = RuntimePrivateField<TargetGame.Target>("targetType");
    private static readonly FieldInfo? RuntimeFairTargetSpeedField = RuntimePrivateField<TargetGame.Target>("speed");
    private static readonly FieldInfo? RuntimeFairTargetSpawnedField = RuntimePrivateField<TargetGame.Target>("spawned");
    private static readonly FieldInfo? RuntimeFairTargetAtPauseField = RuntimePrivateField<TargetGame.Target>("atPausePosition");
    private static readonly FieldInfo? RuntimeFairTargetPausePositionField = RuntimePrivateField<TargetGame.Target>("xPausePosition");
    private static readonly FieldInfo? RuntimeFairTargetPauseTimeField = RuntimePrivateField<TargetGame.Target>("xPauseTime");

    private enum FairSlingshotStage
    {
        Move,
        WaitDialogue,
        WaitMinigame,
        RunMinigame,
        WaitReturn
    }

    private sealed class ActiveFairSlingshotGame : INativeObjectInteractionMovement
    {
        public ActiveFairSlingshotGame(
            PendingExecution pending,
            GameLocation location,
            StardewValley.Event festival,
            Point interaction,
            Point stand,
            List<Point> path,
            int maxMovementTiles)
        {
            Pending = pending;
            Location = location;
            Festival = festival;
            Interaction = interaction;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
            StartedAt = DateTimeOffset.UtcNow.ToString("O");
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public StardewValley.Event Festival { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int MaxTicks => 6000;
        public string StartedAt { get; }
        public FairSlingshotStage Stage { get; set; }
        public TargetGame? Game { get; set; }
        public GameLocation? TargetLocation { get; set; }
        public Slingshot? Slingshot { get; set; }
        public TargetGame.Target? AimTarget { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int RawScore { get; set; } = -1;
        public int RawShotsFired { get; set; } = -1;
        public int RawSuccessfulShots { get; set; } = -1;
        public bool ChargeHeld { get; set; }
        public int ChargeTicks { get; set; }
        public int CooldownTicks { get; set; }
        public int NativeShotsLaunched { get; set; }
        public int AimControlTicks { get; set; }
        public int TargetsAimed { get; set; }
        public bool SawNativeReturn { get; set; }
    }

    private void StartFairSlingshotGame(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!FairSlingshotRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_slingshot_game",
                "fair_slingshot_game=requested", FairSlingshotObservedEffect(null),
                "fair_slingshot_game_typed_request_required"));
            return;
        }
        if (activeFairSlingshotGame is not null || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_slingshot_game",
                "fair_slingshot_game=requested", FairSlingshotObservedEffect(null),
                "fair_slingshot_game_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var festival = location?.currentEvent;
        if (location is null || festival is null || !festival.isFestival || festival.id != "festival_fall16" ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            Game1.player.Money != request.FairSlingshotMoneyBefore ||
            Game1.player.festivalScore != request.FairSlingshotFestivalScoreBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_slingshot_game",
                "festival=fall16", FairSlingshotObservedEffect(null),
                "fair_slingshot_game_festival_money_or_score_drifted"));
            return;
        }
        var interaction = new Point(request.FairSlingshotInteractionTileX!.Value, request.FairSlingshotInteractionTileY!.Value);
        var stand = new Point(request.FairSlingshotStandTileX!.Value, request.FairSlingshotStandTileY!.Value);
        var tileIndex = location.getTileIndexAt(interaction.X, interaction.Y, "Buildings", "untitled tile sheet");
        if (tileIndex is not (501 or 502) || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_slingshot_game",
                "interaction=fair_slingshot_stand", FairSlingshotObservedEffect(null),
                "fair_slingshot_game_endpoint_drifted"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_slingshot_game",
                "route=stand", FairSlingshotObservedEffect(null),
                "fair_slingshot_game_path_unavailable:" + pathReason));
            return;
        }
        activeFairSlingshotGame = new ActiveFairSlingshotGame(
            pending, location, festival, interaction, stand, path, maxMovementTiles);
    }

    private static bool FairSlingshotRequestIsTyped(TrainingExecutionRequest request) =>
        request.FairSlingshotInteractionTileX.HasValue && request.FairSlingshotInteractionTileY.HasValue &&
        request.FairSlingshotStandTileX.HasValue && request.FairSlingshotStandTileY.HasValue &&
        request.FairSlingshotMoneyBefore is >= 50 && request.FairSlingshotEntryFeeMoney == 50 &&
        request.FairSlingshotFestivalScoreBefore.HasValue && request.FairSlingshotStardropPriceStarTokens == 2000 &&
        request.FairSlingshotProjectedUnclaimedGrangeTokens is >= 0 && request.FairSlingshotRemainingStarTokenDemand is > 0 &&
        request.FairSlingshotPrestartDurationMs == 1000 && request.FairSlingshotGameDurationMs == 50000 &&
        request.FairSlingshotPostGameDelayMs == 1000 && request.FairSlingshotResultsDurationMs == 16100 &&
        request.FairSlingshotTargetCount == 79 && request.FairSlingshotDialogueKey == "slingshotGame" &&
        request.FairSlingshotPlayResponseKey == "Play" &&
        request.FairSlingshotExecutionStrategy == "native_predictive_intercept_legal_input" &&
        !string.IsNullOrWhiteSpace(request.FairSlingshotProjectionFingerprint) &&
        request.NativeContract == RuntimeFairSlingshotNativeContract;

    private void TickFairSlingshotGame()
    {
        var active = activeFairSlingshotGame;
        if (active is null)
            return;
        if (active.Stage == FairSlingshotStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "fair_slingshot_game", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockFairSlingshotGame(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenFairSlingshotDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_timeout");
            return;
        }
        if (active.Stage is not FairSlingshotStage.WaitReturn &&
            !ReferenceEquals(Game1.currentLocation?.currentEvent, active.Festival))
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_festival_event_changed");
            return;
        }

        switch (active.Stage)
        {
            case FairSlingshotStage.WaitDialogue:
                TickFairSlingshotDialogue(active);
                break;
            case FairSlingshotStage.WaitMinigame:
                TickFairSlingshotMinigameStart(active);
                break;
            case FairSlingshotStage.RunMinigame:
                TickFairSlingshotNativeSession(active);
                break;
            case FairSlingshotStage.WaitReturn:
                TickFairSlingshotReturn(active);
                break;
        }
    }

    private void OpenFairSlingshotDialogue(ActiveFairSlingshotGame active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Festival.checkAction(
                new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport,
                Game1.player))
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_check_action_rejected");
            return;
        }
        active.Stage = FairSlingshotStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickFairSlingshotDialogue(ActiveFairSlingshotGame active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockFairSlingshotGame(active, "fair_slingshot_game_dialogue_open_timeout");
            return;
        }
        if (Game1.currentLocation.lastQuestionKey != "slingshotGame")
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_dialogue_key_mismatch");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses, row => row.responseKey == "Play");
        if (responseIndex < 0)
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_play_response_missing");
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null ||
            responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockFairSlingshotGame(active, "fair_slingshot_game_dialogue_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        var clickX = bounds.Center.X;
        var clickY = bounds.Center.Y;
        menu.performHoverAction(clickX, clickY);
        if (menu.selectedResponse != responseIndex)
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_play_hover_rejected");
            return;
        }
        menu.receiveLeftClick(clickX, clickY);
        if (Game1.player.Money != active.Pending.Request.FairSlingshotMoneyBefore - 50)
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_entry_fee_receipt_mismatch");
            return;
        }
        active.Stage = FairSlingshotStage.WaitMinigame;
        active.StageTicks = 0;
    }

    private void TickFairSlingshotMinigameStart(ActiveFairSlingshotGame active)
    {
        if (Game1.currentMinigame is TargetGame game)
        {
            var targetLocation = RuntimeFairSlingshotLocationField?.GetValue(game) as GameLocation;
            if (targetLocation is null || game.minigameId() != "TargetGame" ||
                Game1.player.TemporaryItem is not Slingshot slingshot || slingshot.QualifiedItemId != "(W)32" ||
                slingshot.attachments.ElementAtOrDefault(0)?.QualifiedItemId != "(O)390" ||
                slingshot.attachments.ElementAtOrDefault(0)?.Stack != 999 || game.targets.Count != 79)
            {
                BlockFairSlingshotGame(active, "fair_slingshot_game_native_instance_or_loadout_mismatch");
                return;
            }
            active.Game = game;
            active.TargetLocation = targetLocation;
            active.Slingshot = slingshot;
            SlingshotAimPatch.ActiveSlingshot = slingshot;
            active.Stage = FairSlingshotStage.RunMinigame;
            active.StageTicks = 0;
            return;
        }
        if (Game1.currentMinigame is not null || active.StageTicks > 300)
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_start_timeout_or_wrong_minigame");
    }

    private void TickFairSlingshotNativeSession(ActiveFairSlingshotGame active)
    {
        var game = active.Game!;
        var slingshot = active.Slingshot!;
        var targetLocation = active.TargetLocation!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            SlingshotAimPatch.Clear(slingshot);
            active.Stage = FairSlingshotStage.WaitReturn;
            active.StageTicks = 0;
            return;
        }

        var showResultsTimer = ReadRuntimeFairSlingshotInt(game, RuntimeFairSlingshotShowResultsTimerField);
        var gameDone = ReadRuntimeFairSlingshotBool(game, RuntimeFairSlingshotGameDoneField);
        if (active.RawScore < 0 && (gameDone && showResultsTimer < 0 || showResultsTimer > 11000))
        {
            active.RawScore = TargetGame.score;
            active.RawShotsFired = TargetGame.shotsFired;
            active.RawSuccessfulShots = TargetGame.successShots;
        }
        if (showResultsTimer >= 0)
        {
            if (active.ChargeHeld)
            {
                slingshot.finish();
                active.ChargeHeld = false;
            }
            return;
        }
        if (!ReferenceEquals(Game1.player.TemporaryItem, slingshot) ||
            slingshot.attachments.ElementAtOrDefault(0)?.QualifiedItemId != "(O)390")
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_loadout_drifted");
            return;
        }

        var timerToStart = ReadRuntimeFairSlingshotInt(game, RuntimeFairSlingshotTimerToStartField);
        var gameEndTimer = ReadRuntimeFairSlingshotInt(game, RuntimeFairSlingshotGameEndTimerField);
        if (timerToStart > 0)
            return;
        if (gameEndTimer <= 1200)
        {
            if (active.ChargeHeld)
                ReleaseFairSlingshotShot(active);
            return;
        }

        if (active.CooldownTicks > 0)
        {
            active.CooldownTicks--;
            return;
        }
        if (!active.ChargeHeld && targetLocation.projectiles.Count > 0)
            return;

        if (active.ChargeHeld)
        {
            if (active.AimTarget is null || !game.targets.Contains(active.AimTarget) ||
                !TryPredictFairTargetIntercept(active, active.AimTarget, 0, out var aim))
            {
                slingshot.finish();
                active.ChargeHeld = false;
                active.AimTarget = null;
                active.CooldownTicks = 2;
                return;
            }
            SlingshotAimPatch.AimWorldPixel = aim;
            active.AimControlTicks++;
            active.ChargeTicks++;
            if (active.ChargeTicks >= 20)
                ReleaseFairSlingshotShot(active);
            return;
        }

        var target = SelectFairSlingshotTarget(active);
        if (target is null || !TryPredictFairTargetIntercept(active, target, 20, out var initialAim))
            return;
        active.AimTarget = target;
        SlingshotAimPatch.ActiveSlingshot = slingshot;
        SlingshotAimPatch.AimWorldPixel = initialAim;
        active.AimControlTicks++;
        active.TargetsAimed++;
        game.receiveLeftClick(0, 0);
        if (!Game1.player.UsingTool || !Game1.player.usingSlingshot)
        {
            BlockFairSlingshotGame(active, "fair_slingshot_game_native_charge_start_not_observed");
            return;
        }
        active.ChargeHeld = true;
        active.ChargeTicks = 0;
    }

    private static TargetGame.Target? SelectFairSlingshotTarget(ActiveFairSlingshotGame active)
    {
        return active.Game!.targets
            .Where(target => ReadRuntimeFairSlingshotBool(target, RuntimeFairTargetSpawnedField))
            .Where(target => target.Position.Right > 0 && target.Position.Left < 1024)
            .Select(target => new
            {
                Target = target,
                Type = ReadRuntimeFairSlingshotInt(target, RuntimeFairTargetTypeField),
                Intercept = TryPredictFairTargetIntercept(active, target, 20, out var aim) ? aim : (Point?)null
            })
            .Where(row => row.Intercept.HasValue)
            .OrderByDescending(row => row.Type)
            .ThenBy(row => Vector2.DistanceSquared(
                active.Slingshot!.GetShootOrigin(Game1.player), row.Intercept!.Value.ToVector2()))
            .Select(row => row.Target)
            .FirstOrDefault();
    }

    private static bool TryPredictFairTargetIntercept(
        ActiveFairSlingshotGame active,
        TargetGame.Target target,
        int chargeTicks,
        out Point aim)
    {
        var origin = active.Slingshot!.GetShootOrigin(Game1.player);
        var futureTicks = chargeTicks;
        var predicted = target.Position.Center;
        for (var iteration = 0; iteration < 5; iteration++)
        {
            if (!TryProjectFairTarget(target, futureTicks, out predicted))
            {
                aim = Point.Zero;
                return false;
            }
            var distance = Vector2.Distance(origin, predicted.ToVector2());
            var travelTicks = (int)Math.Ceiling(distance / 19f) + 1;
            var next = chargeTicks + travelTicks;
            if (next == futureTicks)
                break;
            futureTicks = next;
        }
        if (!TryProjectFairTarget(target, futureTicks, out predicted))
        {
            aim = Point.Zero;
            return false;
        }
        aim = predicted;
        return true;
    }

    private static bool TryProjectFairTarget(TargetGame.Target target, int ticks, out Point center)
    {
        var x = target.Position.X;
        var y = target.Position.Y;
        var width = target.Position.Width;
        var height = target.Position.Height;
        var speed = ReadRuntimeFairSlingshotInt(target, RuntimeFairTargetSpeedField);
        var atPause = ReadRuntimeFairSlingshotBool(target, RuntimeFairTargetAtPauseField);
        var pausePosition = ReadRuntimeFairSlingshotInt(target, RuntimeFairTargetPausePositionField);
        var pauseRemainingMs = ReadRuntimeFairSlingshotInt(target, RuntimeFairTargetPauseTimeField);
        for (var tick = 0; tick < ticks; tick++)
        {
            if (atPause)
            {
                pauseRemainingMs -= 17;
                if (pauseRemainingMs <= 0)
                {
                    speed = -speed;
                    atPause = false;
                    pausePosition = -1;
                }
            }
            else
            {
                x += speed;
                if (pausePosition != -1 && Math.Abs(pausePosition - x) <= Math.Abs(speed))
                    atPause = true;
            }
            if (x < 0 || x + width > 1024)
            {
                center = Point.Zero;
                return false;
            }
        }
        center = new Point(x + width / 2, y + height / 2);
        return true;
    }

    private static void ReleaseFairSlingshotShot(ActiveFairSlingshotGame active)
    {
        var game = active.Game!;
        var location = active.TargetLocation!;
        var shotsBefore = TargetGame.shotsFired;
        var projectilesBefore = location.projectiles.Count;
        game.releaseLeftClick(0, 0);
        active.ChargeHeld = false;
        active.ChargeTicks = 0;
        active.AimTarget = null;
        active.CooldownTicks = 2;
        if (TargetGame.shotsFired != shotsBefore + 1 || location.projectiles.Count <= projectilesBefore)
            return;
        active.NativeShotsLaunched++;
    }

    private void TickFairSlingshotReturn(ActiveFairSlingshotGame active)
    {
        if (Game1.currentMinigame is not null)
        {
            if (active.StageTicks > 300)
                BlockFairSlingshotGame(active, "fair_slingshot_game_unload_timeout");
            return;
        }
        var expectedReturn = Game1.year % 2 == 0 ? new Point(24, 70) : new Point(24, 63);
        active.SawNativeReturn = ReferenceEquals(Game1.currentLocation, active.Location) &&
            ReferenceEquals(Game1.player.currentLocation, active.Location) && Game1.player.TilePoint == expectedReturn;
        if (!active.SawNativeReturn || Game1.player.TemporaryItem is not null || Game1.activeClickableMenu is not null ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            if (active.StageTicks > 240)
                BlockFairSlingshotGame(active, "fair_slingshot_game_native_return_state_mismatch");
            return;
        }
        CompleteFairSlingshotGame(active);
    }

    private void CompleteFairSlingshotGame(ActiveFairSlingshotGame active)
    {
        var request = active.Pending.Request;
        var rawShots = active.RawShotsFired;
        var rawSuccess = active.RawSuccessfulShots;
        var expectedAccuracy = rawShots > 1
            ? (int)Math.Max(0d, Math.Round((float)rawSuccess / (rawShots - 1), 2) * 100d)
            : 0;
        var expectedMultiplier = expectedAccuracy >= 100 ? 4f :
            expectedAccuracy >= 95 ? 3f :
            expectedAccuracy >= 90 ? 2.5f :
            expectedAccuracy >= 85 ? 2f :
            expectedAccuracy >= 75 ? 1.5f : 1f;
        var expectedScore = (int)(active.RawScore * expectedMultiplier);
        var expectedTokens = 0;
        if (expectedScore >= 40)
        {
            expectedTokens = (int)(((expectedScore * 2 - 30) / 10) * 2.5f) * 2;
            if (expectedTokens > 280)
                expectedTokens = 500;
        }
        var reportedModifier = ReadRuntimeFairSlingshotFloat(active.Game!, RuntimeFairSlingshotModifierBonusField);
        var verified = active.RawScore > 0 && rawShots > 1 && rawSuccess > 0 &&
            active.NativeShotsLaunched == rawShots && active.AimControlTicks > 0 && active.TargetsAimed > 0 &&
            TargetGame.accuracy == expectedAccuracy && TargetGame.score == expectedScore &&
            (expectedMultiplier == 1f ? reportedModifier == 0f : Math.Abs(reportedModifier - expectedMultiplier) < 0.001f) &&
            TargetGame.starTokensWon == expectedTokens && expectedTokens > 0 &&
            Game1.player.Money == request.FairSlingshotMoneyBefore - 50 &&
            Game1.player.festivalScore == request.FairSlingshotFestivalScoreBefore + expectedTokens &&
            active.SawNativeReturn && ReferenceEquals(active.Location.currentEvent, active.Festival);
        activeFairSlingshotGame = null;
        SlingshotAimPatch.Clear(active.Slingshot!);
        StopAllMovement();
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "play_fair_slingshot_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_festival_checkAction_and_DialogueBox_Play_click_verified",
                    "exact_50g_entry_fee_verified",
                    "native_50_second_TargetGame_completed",
                    "shared_slingshot_aim_patch_and_predictive_intercept_input_verified",
                    "exact_native_target_score_accuracy_multiplier_and_star_token_formula_verified",
                    "native_festival_return_and_temporary_loadout_cleanup_verified"
                }
                : new[] { "fair_slingshot_game_post_state_mismatch" },
            RequestedEffect = "money=-50;festival_score=+native_slingshot_game_reward;duration_ms=50000",
            ObservedEffect = FairSlingshotObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fair_slingshot_game_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.money", Before = request.FairSlingshotMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                    new SimulatedFactChange { Path = "player.festival_score", Before = request.FairSlingshotFestivalScoreBefore.ToString()!, After = Game1.player.festivalScore.ToString() },
                    new SimulatedFactChange { Path = "player.fair_slingshot_game.last_session_score", Before = "none", After = TargetGame.score.ToString() },
                    new SimulatedFactChange { Path = "player.fair_slingshot_game.last_session_accuracy", Before = "none", After = TargetGame.accuracy.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockFairSlingshotGame(ActiveFairSlingshotGame active, string reason)
    {
        activeFairSlingshotGame = null;
        if (active.Slingshot is not null)
        {
            active.Slingshot.finish();
            SlingshotAimPatch.Clear(active.Slingshot);
        }
        StopAllMovement();
        if (active.Game is { } game && ReferenceEquals(Game1.currentMinigame, game))
            game.receiveKeyPress(Keys.Escape);
        else if (Game1.activeClickableMenu is DialogueBox)
            Game1.exitActiveMenu();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_fair_slingshot_game", "money=-50;native_session=50000ms",
            FairSlingshotObservedEffect(active), reason));
    }

    private static FieldInfo? RuntimePrivateField<T>(string name) =>
        typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static int ReadRuntimeFairSlingshotInt(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is int value ? value : int.MinValue;

    private static bool ReadRuntimeFairSlingshotBool(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is bool value && value;

    private static float ReadRuntimeFairSlingshotFloat(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is float value ? value : float.NaN;

    private static string FairSlingshotObservedEffect(ActiveFairSlingshotGame? active)
    {
        var game = active?.Game ?? Game1.currentMinigame as TargetGame;
        return "festival=" + (active?.Festival.id ?? Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
            ";money=" + Game1.player.Money +
            ";festival_score=" + Game1.player.festivalScore +
            ";raw_score=" + (active?.RawScore.ToString() ?? "unavailable") +
            ";final_score=" + (game is null ? "unavailable" : TargetGame.score.ToString()) +
            ";shots_fired=" + (game is null ? "unavailable" : TargetGame.shotsFired.ToString()) +
            ";successful_shots=" + (game is null ? "unavailable" : TargetGame.successShots.ToString()) +
            ";accuracy=" + (game is null ? "unavailable" : TargetGame.accuracy.ToString()) +
            ";tokens_won=" + (game is null ? "unavailable" : TargetGame.starTokensWon.ToString()) +
            ";native_shots_launched=" + (active?.NativeShotsLaunched.ToString() ?? "unavailable") +
            ";aim_control_ticks=" + (active?.AimControlTicks.ToString() ?? "unavailable") +
            ";temporary_item=" + (Game1.player.TemporaryItem?.QualifiedItemId ?? "none");
    }
}
