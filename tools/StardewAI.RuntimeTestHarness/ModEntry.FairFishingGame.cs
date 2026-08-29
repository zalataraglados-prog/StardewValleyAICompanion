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
    private const string RuntimeFairFishingNativeContract =
        "Event.checkAction(festival_fall16_buildings_503_504)->DialogueBox(fishingGame:Play).receiveLeftClick->Event.answerDialogue(fishingGame,0)->Money-50->globalFadeToBlack(FishingGame.startMe)->native_100000ms_FishingGame_input_session->perfection_score_reward->festivalScore";

    private static readonly FieldInfo? RuntimeFairFishingTimerToStartField = typeof(FishingGame)
        .GetField("timerToStart", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? RuntimeFairFishingGameEndTimerField = typeof(FishingGame)
        .GetField("gameEndTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? RuntimeFairFishingShowResultsTimerField = typeof(FishingGame)
        .GetField("showResultsTimer", BindingFlags.Instance | BindingFlags.NonPublic);

    private enum FairFishingStage
    {
        Move,
        WaitDialogue,
        WaitMinigame,
        RunMinigame,
        WaitReturn
    }

    private sealed class ActiveFairFishingGame : INativeObjectInteractionMovement
    {
        public ActiveFairFishingGame(
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
        public int MaxTicks => 12000;
        public string StartedAt { get; }
        public FairFishingStage Stage { get; set; }
        public FishingGame? Game { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int RawScore { get; set; } = -1;
        public int RawFishCaught { get; set; } = -1;
        public int RawPerfections { get; set; } = -1;
        public int BobberControlTicks { get; set; }
        public int BobberPressedTicks { get; set; }
        public int CastCount { get; set; }
        public int HookCount { get; set; }
        public BobberBar? LastBobberBar { get; set; }
        public bool LastBobberWasPerfect { get; set; }
        public string FirstBobberPerfectFlagDropDiagnostic { get; set; } = "none";
        public bool CastInputHeld { get; set; }
        public bool HookIssuedForNibble { get; set; }
        public bool SawBobberBar { get; set; }
        public bool SawNativeReturn { get; set; }
    }

    private void StartFairFishingGame(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!FairFishingRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_fishing_game",
                "fair_fishing_game=requested", FairFishingObservedEffect(null),
                "fair_fishing_game_typed_request_required"));
            return;
        }
        if (activeFairFishingGame is not null || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_fishing_game",
                "fair_fishing_game=requested", FairFishingObservedEffect(null),
                "fair_fishing_game_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var festival = location?.currentEvent;
        if (location is null || festival is null || !festival.isFestival || festival.id != "festival_fall16" ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            Game1.player.Money != request.FairFishingMoneyBefore ||
            Game1.player.festivalScore != request.FairFishingFestivalScoreBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_fishing_game",
                "festival=fall16", FairFishingObservedEffect(null),
                "fair_fishing_game_festival_money_or_score_drifted"));
            return;
        }
        var interaction = new Point(request.FairFishingInteractionTileX!.Value, request.FairFishingInteractionTileY!.Value);
        var stand = new Point(request.FairFishingStandTileX!.Value, request.FairFishingStandTileY!.Value);
        var tileIndex = location.getTileIndexAt(interaction.X, interaction.Y, "Buildings", "untitled tile sheet");
        if (tileIndex is not (503 or 504) || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_fishing_game",
                "interaction=fair_fishing_stand", FairFishingObservedEffect(null),
                "fair_fishing_game_endpoint_drifted"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_fishing_game",
                "route=stand", FairFishingObservedEffect(null),
                "fair_fishing_game_path_unavailable:" + pathReason));
            return;
        }
        activeFairFishingGame = new ActiveFairFishingGame(
            pending, location, festival, interaction, stand, path, maxMovementTiles);
    }

    private static bool FairFishingRequestIsTyped(TrainingExecutionRequest request) =>
        request.FairFishingInteractionTileX.HasValue && request.FairFishingInteractionTileY.HasValue &&
        request.FairFishingStandTileX.HasValue && request.FairFishingStandTileY.HasValue &&
        request.FairFishingMoneyBefore is >= 50 && request.FairFishingEntryFeeMoney == 50 &&
        request.FairFishingFestivalScoreBefore.HasValue && request.FairFishingStardropPriceStarTokens == 2000 &&
        request.FairFishingProjectedUnclaimedGrangeTokens is >= 0 && request.FairFishingRemainingStarTokenDemand is > 0 &&
        request.FairFishingGameDurationMs == 100000 && request.FairFishingResultsDurationMs == 11100 &&
        request.FairFishingDialogueKey == "fishingGame" && request.FairFishingPlayResponseKey == "Play" &&
        request.FairFishingExecutionStrategy == "native_predictive_legal_input" &&
        !string.IsNullOrWhiteSpace(request.FairFishingProjectionFingerprint) &&
        request.NativeContract == RuntimeFairFishingNativeContract;

    private void TickFairFishingGame()
    {
        var active = activeFairFishingGame;
        if (active is null)
            return;
        if (active.Stage == FairFishingStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "fair_fishing_game", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockFairFishingGame(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenFairFishingDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockFairFishingGame(active, "fair_fishing_game_timeout");
            return;
        }
        if (active.Stage is not FairFishingStage.WaitReturn &&
            !ReferenceEquals(Game1.currentLocation?.currentEvent, active.Festival))
        {
            BlockFairFishingGame(active, "fair_fishing_game_festival_event_changed");
            return;
        }

        switch (active.Stage)
        {
            case FairFishingStage.WaitDialogue:
                TickFairFishingDialogue(active);
                break;
            case FairFishingStage.WaitMinigame:
                TickFairFishingMinigameStart(active);
                break;
            case FairFishingStage.RunMinigame:
                TickFairFishingNativeSession(active);
                break;
            case FairFishingStage.WaitReturn:
                TickFairFishingReturn(active);
                break;
        }
    }

    private void OpenFairFishingDialogue(ActiveFairFishingGame active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Festival.checkAction(
                new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport,
                Game1.player))
        {
            BlockFairFishingGame(active, "fair_fishing_game_native_check_action_rejected");
            return;
        }
        active.Stage = FairFishingStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickFairFishingDialogue(ActiveFairFishingGame active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockFairFishingGame(active, "fair_fishing_game_dialogue_open_timeout");
            return;
        }
        if (Game1.currentLocation.lastQuestionKey != "fishingGame")
        {
            BlockFairFishingGame(active, "fair_fishing_game_dialogue_key_mismatch");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses, row => row.responseKey == "Play");
        if (responseIndex < 0)
        {
            BlockFairFishingGame(active, "fair_fishing_game_native_play_response_missing");
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null ||
            responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockFairFishingGame(active, "fair_fishing_game_dialogue_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        var clickX = bounds.Center.X;
        var clickY = bounds.Center.Y;
        menu.performHoverAction(clickX, clickY);
        if (menu.selectedResponse != responseIndex)
        {
            BlockFairFishingGame(active, "fair_fishing_game_native_play_hover_rejected");
            return;
        }
        menu.receiveLeftClick(clickX, clickY);
        if (Game1.player.Money != active.Pending.Request.FairFishingMoneyBefore - 50)
        {
            BlockFairFishingGame(active, "fair_fishing_game_entry_fee_receipt_mismatch");
            return;
        }
        active.Stage = FairFishingStage.WaitMinigame;
        active.StageTicks = 0;
    }

    private void TickFairFishingMinigameStart(ActiveFairFishingGame active)
    {
        if (Game1.currentMinigame is FishingGame game)
        {
            if (!ReferenceEquals(game.originalLocation, active.Location) || game.minigameId() != "FishingGame")
            {
                BlockFairFishingGame(active, "fair_fishing_game_native_instance_context_mismatch");
                return;
            }
            active.Game = game;
            active.Stage = FairFishingStage.RunMinigame;
            active.StageTicks = 0;
            return;
        }
        if (Game1.currentMinigame is not null || active.StageTicks > 300)
            BlockFairFishingGame(active, "fair_fishing_game_native_start_timeout_or_wrong_minigame");
    }

    private void TickFairFishingNativeSession(ActiveFairFishingGame active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            ReleaseSmapiLeftButtonOverride();
            active.Stage = FairFishingStage.WaitReturn;
            active.StageTicks = 0;
            return;
        }
        var showResultsTimer = ReadRuntimeFairFishingInt(game, RuntimeFairFishingShowResultsTimerField);
        if (active.RawScore < 0 &&
            (game.gameDone && showResultsTimer < 0 || showResultsTimer is > 7000))
        {
            active.RawScore = game.score;
            active.RawFishCaught = game.fishCaught;
            active.RawPerfections = game.perfections;
        }
        if (showResultsTimer >= 0)
        {
            TryApplySmapiLeftButtonOverride(pressed: false, out _);
            return;
        }

        if (Game1.activeClickableMenu is BobberBar bar)
        {
            active.SawBobberBar = true;
            return;
        }

        var rod = Game1.player.CurrentTool as FishingRod;
        if (rod is null || rod.QualifiedItemId != "(T)BambooPole")
        {
            BlockFairFishingGame(active, "fair_fishing_game_native_bamboo_pole_missing");
            return;
        }
        if (rod.fishCaught)
        {
            if (!TryApplySmapiLeftButtonOverride(pressed: true, out var reason))
                BlockFairFishingGame(active, "fair_fishing_game_fish_hold_input_failed:" + reason);
            return;
        }
        if (rod.isNibbling)
        {
            if (!active.HookIssuedForNibble)
            {
                game.receiveLeftClick(0, 0);
                active.HookIssuedForNibble = true;
                active.HookCount++;
            }
            return;
        }
        active.HookIssuedForNibble = false;
        if (rod.isTimingCast)
        {
            var projectedPower = Math.Clamp(rod.castingPower + Math.Max(0f, rod.castingTimerSpeed) * 17f, 0f, 1f);
            if (projectedPower >= 0.99f)
            {
                active.CastInputHeld = false;
                game.releaseLeftClick(0, 0);
            }
            if (!TryApplySmapiLeftButtonOverride(active.CastInputHeld, out var reason))
                BlockFairFishingGame(active, "fair_fishing_game_cast_hold_input_failed:" + reason);
            return;
        }
        if (rod.isCasting || rod.castedButBobberStillInAir || rod.isFishing || rod.isReeling ||
            rod.pullingOutOfWater || game.gameDone)
        {
            TryApplySmapiLeftButtonOverride(pressed: false, out _);
            return;
        }
        var timerToStart = ReadRuntimeFairFishingInt(game, RuntimeFairFishingTimerToStartField);
        var gameEndTimer = ReadRuntimeFairFishingInt(game, RuntimeFairFishingGameEndTimerField);
        if (timerToStart <= 0 && gameEndTimer > 1000 && Game1.activeClickableMenu is null)
        {
            active.CastInputHeld = true;
            active.CastCount++;
            if (!TryApplySmapiLeftButtonOverride(pressed: true, out var reason))
            {
                BlockFairFishingGame(active, "fair_fishing_game_cast_start_input_failed:" + reason);
                return;
            }
            game.receiveLeftClick(0, 0);
        }
    }

    private void TickFairFishingReturn(ActiveFairFishingGame active)
    {
        if (Game1.currentMinigame is not null)
        {
            if (active.StageTicks > 300)
                BlockFairFishingGame(active, "fair_fishing_game_unload_timeout");
            return;
        }
        var expectedReturn = Game1.year % 2 == 0 ? new Point(36, 68) : new Point(24, 71);
        active.SawNativeReturn = ReferenceEquals(Game1.currentLocation, active.Location) &&
            ReferenceEquals(Game1.player.currentLocation, active.Location) && Game1.player.TilePoint == expectedReturn;
        if (!active.SawNativeReturn || Game1.player.TemporaryItem is not null || Game1.activeClickableMenu is not null ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            if (active.StageTicks > 240)
                BlockFairFishingGame(active, "fair_fishing_game_native_return_state_mismatch");
            return;
        }
        CompleteFairFishingGame(active);
    }

    private static void RecordFairFishingPerfectLoss(ActiveFairFishingGame active, BobberBar bar)
    {
        if (!ReferenceEquals(active.LastBobberBar, bar))
        {
            active.LastBobberBar = bar;
            active.LastBobberWasPerfect = bar.perfect;
            return;
        }
        if (active.LastBobberWasPerfect && !bar.perfect && active.FirstBobberPerfectFlagDropDiagnostic == "none")
        {
            active.FirstBobberPerfectFlagDropDiagnostic =
                "fish=" + bar.whichFish +
                ",difficulty=" + bar.difficulty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",motion=" + bar.motionType +
                ",fish_position=" + bar.bobberPosition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",fish_speed=" + (bar.bobberSpeed + bar.floaterSinkerAcceleration).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",fish_acceleration=" + bar.bobberAcceleration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",fish_target=" + bar.bobberTargetPosition.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",bar_position=" + bar.bobberBarPos.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",bar_speed=" + bar.bobberBarSpeed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                ",bar_height=" + bar.bobberBarHeight;
        }
        active.LastBobberWasPerfect = bar.perfect;
    }

    private bool ApplyFairFishingBobberInput(ActiveFairFishingGame active, BobberBar bar, out string reason)
    {
        active.SawBobberBar = true;
        RecordFairFishingPerfectLoss(active, bar);
        var shouldPress = PerfectBobberBarShouldPress(bar);
        active.BobberControlTicks++;
        if (shouldPress)
            active.BobberPressedTicks++;
        return ApplyBobberBarInput(shouldPress, out reason);
    }

    private void CompleteFairFishingGame(ActiveFairFishingGame active)
    {
        var request = active.Pending.Request;
        var game = active.Game!;
        var scoreBeforeDouble = active.RawScore + active.RawPerfections * 10;
        var doubled = active.RawFishCaught >= 3 && active.RawPerfections >= 3;
        var expectedScore = doubled ? scoreBeforeDouble * 2 : scoreBeforeDouble;
        var expectedPerfectionBonus = active.RawPerfections * 10 + (doubled ? scoreBeforeDouble : 0);
        var expectedTokens = expectedScore >= 10 ? (expectedScore + 5) / 10 * 6 * 2 : 0;
        var verified = active.RawScore >= 0 && active.RawFishCaught > 0 &&
            active.RawPerfections >= 0 && active.RawPerfections <= active.RawFishCaught &&
            active.SawBobberBar && active.BobberControlTicks > 0 &&
            game.score == expectedScore && game.perfectionBonus == expectedPerfectionBonus &&
            game.starTokensWon == expectedTokens && expectedTokens > 0 &&
            Game1.player.Money == request.FairFishingMoneyBefore - 50 &&
            Game1.player.festivalScore == request.FairFishingFestivalScoreBefore + expectedTokens &&
            active.SawNativeReturn && ReferenceEquals(active.Location.currentEvent, active.Festival);
        activeFairFishingGame = null;
        ReleaseSmapiLeftButtonOverride();
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
            PrimitiveKind = "play_fair_fishing_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_festival_checkAction_and_DialogueBox_Play_click_verified",
                    "exact_50g_entry_fee_verified",
                    "native_100_second_FishingGame_completed",
                    "shared_predictive_legal_bobber_input_controller_verified",
                    "native_perfection_outcome=" + active.RawPerfections + "/" + active.RawFishCaught,
                    "exact_native_score_perfection_and_star_token_formula_verified",
                    "native_festival_return_and_temporary_loadout_cleanup_verified"
                }
                : new[] { "fair_fishing_game_post_state_mismatch" },
            RequestedEffect = "money=-50;festival_score=+native_fishing_game_reward;duration_ms=100000",
            ObservedEffect = FairFishingObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fair_fishing_game_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.money", Before = request.FairFishingMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                    new SimulatedFactChange { Path = "player.festival_score", Before = request.FairFishingFestivalScoreBefore.ToString()!, After = Game1.player.festivalScore.ToString() },
                    new SimulatedFactChange { Path = "player.fair_fishing_game.last_session_score", Before = "none", After = game.score.ToString() },
                    new SimulatedFactChange { Path = "player.fair_fishing_game.last_session_perfections", Before = "none", After = active.RawPerfections + "/" + active.RawFishCaught }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockFairFishingGame(ActiveFairFishingGame active, string reason)
    {
        activeFairFishingGame = null;
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        if (active.Game is { } game && ReferenceEquals(Game1.currentMinigame, game))
            game.receiveKeyPress(Keys.Escape);
        else if (Game1.activeClickableMenu is DialogueBox)
            Game1.exitActiveMenu();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_fair_fishing_game", "money=-50;native_session=100000ms",
            FairFishingObservedEffect(active), reason));
    }

    private static int ReadRuntimeFairFishingInt(FishingGame game, FieldInfo? field) =>
        field?.GetValue(game) is int value ? value : int.MinValue;

    private static string FairFishingObservedEffect(ActiveFairFishingGame? active)
    {
        var game = active?.Game ?? Game1.currentMinigame as FishingGame;
        return "festival=" + (active?.Festival.id ?? Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
            ";money=" + Game1.player.Money +
            ";festival_score=" + Game1.player.festivalScore +
            ";score=" + (game?.score.ToString() ?? "unavailable") +
            ";fish_caught=" + (game?.fishCaught.ToString() ?? "unavailable") +
            ";perfections=" + (game?.perfections.ToString() ?? "unavailable") +
            ";tokens_won=" + (game?.starTokensWon.ToString() ?? "unavailable") +
            ";casts=" + (active?.CastCount.ToString() ?? "unavailable") +
            ";hooks=" + (active?.HookCount.ToString() ?? "unavailable") +
            ";bobber_control_ticks=" + (active?.BobberControlTicks.ToString() ?? "unavailable") +
            ";first_bobber_perfect_flag_drop=" + (active?.FirstBobberPerfectFlagDropDiagnostic ?? "unavailable") +
            ";temporary_item=" + (Game1.player.TemporaryItem?.QualifiedItemId ?? "none");
    }
}
