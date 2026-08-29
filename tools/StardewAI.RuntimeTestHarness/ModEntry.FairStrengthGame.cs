using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeFairStrengthNativeContract =
        "Event.checkAction(festival_fall16_buildings_540,player_tile_x_29)->StrengthGame.receiveLeftClick->FarmerSprite.animateOnce(168,80ms,8)->StrengthGame.afterSwingAnimation->power>=99->festivalScore+1->native_result_dialogue_and_exit";
    private const int FairStrengthSettledPowerUpdatesToImpact = 9;
    private static readonly FieldInfo? RuntimeFairStrengthPowerField = RuntimePrivateField<StrengthGame>("power");
    private static readonly FieldInfo? RuntimeFairStrengthChangeSpeedField = RuntimePrivateField<StrengthGame>("changeSpeed");
    private static readonly FieldInfo? RuntimeFairStrengthEndTimerField = RuntimePrivateField<StrengthGame>("endTimer");
    private static readonly FieldInfo? RuntimeFairStrengthClickedField = RuntimePrivateField<StrengthGame>("clicked");
    private static readonly FieldInfo? RuntimeFairStrengthShowedResultField = RuntimePrivateField<StrengthGame>("showedResult");

    private enum FairStrengthStage
    {
        Move,
        SettleMovement,
        WaitMenu,
        WaitTiming,
        WaitSwing,
        WaitResult,
        CloseResult,
        WaitCleanup
    }

    private sealed class ActiveFairStrengthGame : INativeObjectInteractionMovement
    {
        public ActiveFairStrengthGame(
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
        public int MaxTicks => 900;
        public string StartedAt { get; }
        public FairStrengthStage Stage { get; set; }
        public StrengthGame? Game { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public int StableMovementTicks { get; set; }
        public int NativeClicks { get; set; }
        public float ClickPower { get; set; } = float.NaN;
        public float ClickChangeSpeed { get; set; } = float.NaN;
        public float PredictedImpactPower { get; set; } = float.NaN;
        public int PredictedUpdatesToImpact { get; set; }
        public float ClickAnimationIntervalModifier { get; set; } = float.NaN;
        public float FinalPower { get; set; } = float.NaN;
        public float FinalEndTimer { get; set; } = float.NaN;
        public bool SawNativeResult { get; set; }
        public bool SawNativeCleanup { get; set; }
    }

    private void StartFairStrengthGame(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!FairStrengthRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_strength_game",
                "fair_strength_game=requested", FairStrengthObservedEffect(null),
                "fair_strength_game_typed_request_required"));
            return;
        }
        if (activeFairStrengthGame is not null || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_strength_game",
                "fair_strength_game=requested", FairStrengthObservedEffect(null),
                "fair_strength_game_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var festival = location?.currentEvent;
        if (location is null || festival is null || !festival.isFestival || festival.id != "festival_fall16" ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            Game1.player.festivalScore != request.FairStrengthFestivalScoreBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_strength_game",
                "festival=fall16", FairStrengthObservedEffect(null),
                "fair_strength_game_festival_or_score_drifted"));
            return;
        }

        var interaction = new Point(request.FairStrengthInteractionTileX!.Value, request.FairStrengthInteractionTileY!.Value);
        var stand = new Point(request.FairStrengthStandTileX!.Value, request.FairStrengthStandTileY!.Value);
        var tileIndex = location.getTileIndexAt(interaction.X, interaction.Y, "Buildings", "untitled tile sheet");
        if (tileIndex != 540 || stand.X != 29 || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_strength_game",
                "interaction=fair_strength_stand", FairStrengthObservedEffect(null),
                "fair_strength_game_endpoint_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_fair_strength_game",
                "route=stand", FairStrengthObservedEffect(null),
                "fair_strength_game_path_unavailable:" + pathReason));
            return;
        }
        activeFairStrengthGame = new ActiveFairStrengthGame(
            pending, location, festival, interaction, stand, path, maxMovementTiles);
    }

    private static bool FairStrengthRequestIsTyped(TrainingExecutionRequest request) =>
        request.FairStrengthInteractionTileX.HasValue && request.FairStrengthInteractionTileY.HasValue &&
        request.FairStrengthStandTileX == 29 && request.FairStrengthStandTileY.HasValue &&
        request.FairStrengthFestivalScoreBefore.HasValue && request.FairStrengthStardropPriceStarTokens == 2000 &&
        request.FairStrengthProjectedUnclaimedGrangeTokens is >= 0 && request.FairStrengthRemainingStarTokenDemand == 1 &&
        request.FairStrengthEntryFeeMoney == 0 && request.FairStrengthExpectedRewardStarTokens == 1 &&
        request.FairStrengthPerfectPowerMinimum == 99d && request.FairStrengthPowerMaximum == 100d &&
        request.FairStrengthRequiredPlayerTileX == 29 && request.FairStrengthSwingStartFrame == 168 &&
        request.FairStrengthSwingIntervalMs == 80d && request.FairStrengthSwingFrameCount == 8 &&
        request.FairStrengthPerfectResultDelayMs == 2000d &&
        request.FairStrengthExecutionStrategy == "native_predictive_single_click_max_power" &&
        !string.IsNullOrWhiteSpace(request.FairStrengthProjectionFingerprint) &&
        request.NativeContract == RuntimeFairStrengthNativeContract;

    private void TickFairStrengthGame()
    {
        var active = activeFairStrengthGame;
        if (active is null)
            return;
        if (active.Stage == FairStrengthStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "fair_strength_game", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockFairStrengthGame(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
            {
                active.Stage = FairStrengthStage.SettleMovement;
                active.StageTicks = 0;
            }
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockFairStrengthGame(active, "fair_strength_game_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation?.currentEvent, active.Festival))
        {
            BlockFairStrengthGame(active, "fair_strength_game_festival_event_changed");
            return;
        }

        switch (active.Stage)
        {
            case FairStrengthStage.SettleMovement:
                TickFairStrengthMovementSettle(active);
                break;
            case FairStrengthStage.WaitMenu:
                TickFairStrengthMenuOpen(active);
                break;
            case FairStrengthStage.WaitTiming:
                if (!ReferenceEquals(Game1.activeClickableMenu, active.Game))
                    BlockFairStrengthGame(active, "fair_strength_game_menu_changed_before_click");
                break;
            case FairStrengthStage.WaitSwing:
                TickFairStrengthSwing(active);
                break;
            case FairStrengthStage.WaitResult:
                TickFairStrengthResult(active);
                break;
            case FairStrengthStage.CloseResult:
                CloseFairStrengthResult(active);
                break;
            case FairStrengthStage.WaitCleanup:
                TickFairStrengthCleanup(active);
                break;
        }
    }

    private void TickFairStrengthMovementSettle(ActiveFairStrengthGame active)
    {
        StopAllMovement("fair_strength_game_settle");
        if (Game1.player.movementDirections.Count == 0 &&
            Math.Abs(Game1.player.FarmerSprite.intervalModifier - 1f) < 0.001f)
        {
            active.StableMovementTicks++;
            if (active.StableMovementTicks >= 2)
                OpenFairStrengthGame(active);
            return;
        }
        active.StableMovementTicks = 0;
        if (active.StageTicks > 60)
            BlockFairStrengthGame(active, "fair_strength_game_movement_input_did_not_settle");
    }

    private void OpenFairStrengthGame(ActiveFairStrengthGame active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Festival.checkAction(
                new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport,
                Game1.player))
        {
            BlockFairStrengthGame(active, "fair_strength_game_native_check_action_rejected");
            return;
        }
        active.Stage = FairStrengthStage.WaitMenu;
        active.StageTicks = 0;
    }

    private void TickFairStrengthMenuOpen(ActiveFairStrengthGame active)
    {
        if (Game1.activeClickableMenu is StrengthGame game)
        {
            var speed = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthChangeSpeedField);
            var power = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthPowerField);
            if (speed is not (3f or 4f) || power < 0f || power > 100f ||
                ReadRuntimeFairStrengthBool(game, RuntimeFairStrengthClickedField))
            {
                BlockFairStrengthGame(active, "fair_strength_game_native_initial_state_mismatch");
                return;
            }
            active.Game = game;
            active.Stage = FairStrengthStage.WaitTiming;
            active.StageTicks = 0;
            return;
        }
        if (active.StageTicks > 120)
            BlockFairStrengthGame(active, "fair_strength_game_native_menu_open_timeout");
    }

    private void ApplyFairStrengthGameInput()
    {
        var active = activeFairStrengthGame;
        if (active is null || active.Stage != FairStrengthStage.WaitTiming ||
            active.Game is not { } game || !ReferenceEquals(Game1.activeClickableMenu, game))
            return;

        var power = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthPowerField);
        var speed = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthChangeSpeedField);
        if (float.IsNaN(power) || speed is not (3f or 4f or -3f or -4f))
        {
            BlockFairStrengthGame(active, "fair_strength_game_live_power_state_unavailable");
            return;
        }
        var updatesToImpact = FairStrengthSettledPowerUpdatesToImpact;
        var predicted = ProjectFairStrengthPower(power, speed, updatesToImpact);
        if (predicted < 99f)
            return;

        active.ClickPower = power;
        active.ClickChangeSpeed = speed;
        active.PredictedImpactPower = predicted;
        active.PredictedUpdatesToImpact = updatesToImpact;
        active.ClickAnimationIntervalModifier = Game1.player.FarmerSprite.intervalModifier;
        game.receiveLeftClick(0, 0);
        active.NativeClicks++;
        if (!ReadRuntimeFairStrengthBool(game, RuntimeFairStrengthClickedField) ||
            Game1.player.toolOverrideFunction is null || !Game1.player.FarmerSprite.isOnToolAnimation())
        {
            BlockFairStrengthGame(active, "fair_strength_game_native_click_or_swing_not_observed");
            return;
        }
        active.Stage = FairStrengthStage.WaitSwing;
        active.StageTicks = 0;
    }

    private static float ProjectFairStrengthPower(float power, float speed, int updates)
    {
        for (var index = 0; index < updates; index++)
        {
            power += speed;
            if (power > 100f)
            {
                power = 100f;
                speed = -speed;
            }
            else if (power < 0f)
            {
                power = 0f;
                speed = -speed;
            }
        }
        return power;
    }

    private void TickFairStrengthSwing(ActiveFairStrengthGame active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.activeClickableMenu, game))
        {
            BlockFairStrengthGame(active, "fair_strength_game_menu_changed_during_swing");
            return;
        }
        var speed = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthChangeSpeedField);
        if (speed != 0f)
        {
            if (active.StageTicks > 90)
                BlockFairStrengthGame(active, "fair_strength_game_native_swing_callback_timeout");
            return;
        }
        active.FinalPower = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthPowerField);
        active.FinalEndTimer = ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthEndTimerField);
        if (active.NativeClicks != 1 || active.FinalPower < 99f || active.FinalPower > 100f ||
            active.FinalEndTimer <= 0f || active.FinalEndTimer > 2000f)
        {
            BlockFairStrengthGame(active, "fair_strength_game_predicted_maximum_power_missed");
            return;
        }
        active.Stage = FairStrengthStage.WaitResult;
        active.StageTicks = 0;
    }

    private void TickFairStrengthResult(ActiveFairStrengthGame active)
    {
        var game = active.Game!;
        if (!ReadRuntimeFairStrengthBool(game, RuntimeFairStrengthShowedResultField))
        {
            if (!ReferenceEquals(Game1.activeClickableMenu, game))
            {
                BlockFairStrengthGame(active, "fair_strength_game_menu_changed_before_result");
                return;
            }
            if (active.StageTicks > 180)
                BlockFairStrengthGame(active, "fair_strength_game_native_result_timeout");
            return;
        }
        if (Game1.activeClickableMenu is not DialogueBox || !Game1.dialogueUp ||
            Game1.player.festivalScore != active.Pending.Request.FairStrengthFestivalScoreBefore + 1)
        {
            BlockFairStrengthGame(active, "fair_strength_game_native_reward_receipt_mismatch");
            return;
        }
        active.SawNativeResult = true;
        active.Stage = FairStrengthStage.CloseResult;
        active.StageTicks = 0;
    }

    private void CloseFairStrengthResult(ActiveFairStrengthGame active)
    {
        var game = active.Game!;
        game.receiveLeftClick(0, 0);
        active.Stage = FairStrengthStage.WaitCleanup;
        active.StageTicks = 0;
    }

    private void TickFairStrengthCleanup(ActiveFairStrengthGame active)
    {
        if (ReferenceEquals(Game1.activeClickableMenu, active.Game) || Game1.dialogueUp ||
            Game1.player.toolOverrideFunction is not null || Game1.player.FarmerSprite.isOnToolAnimation())
        {
            if (active.StageTicks > 120)
                BlockFairStrengthGame(active, "fair_strength_game_native_cleanup_timeout");
            return;
        }
        active.SawNativeCleanup = true;
        CompleteFairStrengthGame(active);
    }

    private void CompleteFairStrengthGame(ActiveFairStrengthGame active)
    {
        var request = active.Pending.Request;
        var verified = active.NativeClicks == 1 && active.FinalPower is >= 99f and <= 100f &&
            active.SawNativeResult && active.SawNativeCleanup &&
            Game1.player.festivalScore == request.FairStrengthFestivalScoreBefore + 1 &&
            ReferenceEquals(active.Location.currentEvent, active.Festival);
        activeFairStrengthGame = null;
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
            PrimitiveKind = "play_fair_strength_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_festival_checkAction_direct_StrengthGame_entry_verified",
                    "shared_BFS_exact_x29_stand_verified",
                    "single_native_click_and_original_168_80ms_8_frame_swing_verified",
                    "predictive_maximum_power_at_native_impact_verified",
                    "exact_one_star_token_reward_and_native_cleanup_verified"
                }
                : new[] { "fair_strength_game_post_state_mismatch" },
            RequestedEffect = "entry_fee_money=0;festival_score=+1;final_power>=99",
            ObservedEffect = FairStrengthObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fair_strength_game_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.festival_score",
                        Before = request.FairStrengthFestivalScoreBefore.ToString()!,
                        After = Game1.player.festivalScore.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockFairStrengthGame(ActiveFairStrengthGame active, string reason)
    {
        activeFairStrengthGame = null;
        StopAllMovement();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_fair_strength_game", "entry_fee_money=0;festival_score=+1;final_power>=99",
            FairStrengthObservedEffect(active), reason));
    }

    private static float ReadRuntimeFairStrengthFloat(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is float value ? value : float.NaN;

    private static bool ReadRuntimeFairStrengthBool(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is bool value && value;

    private static string FairStrengthObservedEffect(ActiveFairStrengthGame? active)
    {
        var game = active?.Game ?? Game1.activeClickableMenu as StrengthGame;
        return "festival=" + (active?.Festival.id ?? Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";festival_score=" + Game1.player.festivalScore +
            ";power=" + (game is null ? "unavailable" : ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthPowerField).ToString("0.###")) +
            ";change_speed=" + (game is null ? "unavailable" : ReadRuntimeFairStrengthFloat(game, RuntimeFairStrengthChangeSpeedField).ToString("0.###")) +
            ";click_power=" + (active?.ClickPower.ToString("0.###") ?? "unavailable") +
            ";click_speed=" + (active?.ClickChangeSpeed.ToString("0.###") ?? "unavailable") +
            ";predicted_impact_power=" + (active?.PredictedImpactPower.ToString("0.###") ?? "unavailable") +
            ";predicted_updates_to_impact=" + (active?.PredictedUpdatesToImpact.ToString() ?? "unavailable") +
            ";click_interval_modifier=" + (active?.ClickAnimationIntervalModifier.ToString("0.###") ?? "unavailable") +
            ";final_power=" + (active?.FinalPower.ToString("0.###") ?? "unavailable") +
            ";native_clicks=" + (active?.NativeClicks.ToString() ?? "unavailable") +
            ";dialogue_up=" + Game1.dialogueUp.ToString().ToLowerInvariant() +
            ";tool_override=" + (Game1.player.toolOverrideFunction is not null).ToString().ToLowerInvariant();
    }
}
