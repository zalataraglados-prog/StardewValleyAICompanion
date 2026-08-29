using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeFairWheelNativeContract =
        "Event.checkAction(festival_fall16_buildings_308_309)->DialogueBox(wheelBet:Green).receiveLeftClick->Event.answerDialogue(wheelBet,1)->NumberSelectionMenu(wager_1_to_festivalScore).receiveLeftClick(ok)->Event.betStarTokens->WheelSpinGame(1000ms,green)->native_random_spin->festivalScore+(win?wager:-wager)->native_result_text_and_exit";
    private static readonly FieldInfo? RuntimeFairWheelTimerField = RuntimePrivateField<WheelSpinGame>("timerBeforeStart");
    private static readonly FieldInfo? RuntimeFairWheelWagerField = RuntimePrivateField<WheelSpinGame>("wager");
    private static readonly FieldInfo? RuntimeFairWheelResultTextField = RuntimePrivateField<WheelSpinGame>("resultText");
    private static readonly FieldInfo? RuntimeFairWheelDoneField = RuntimePrivateField<WheelSpinGame>("doneSpinning");
    private static readonly FieldInfo? RuntimeFairWheelNumberMinimumField = RuntimePrivateField<NumberSelectionMenu>("minValue");
    private static readonly FieldInfo? RuntimeFairWheelNumberMaximumField = RuntimePrivateField<NumberSelectionMenu>("maxValue");
    private static readonly FieldInfo? RuntimeFairWheelNumberCurrentField = RuntimePrivateField<NumberSelectionMenu>("currentValue");
    private static readonly FieldInfo? RuntimeFairWheelNumberPriceField = RuntimePrivateField<NumberSelectionMenu>("price");
    private static readonly FieldInfo? RuntimeFairWheelNumberTextBoxField = RuntimePrivateField<NumberSelectionMenu>("numberSelectedBox");

    private enum FairWheelStage
    {
        Move,
        WaitDialogue,
        WaitNumberSelection,
        WaitWagerParse,
        WaitWheel,
        WaitSettlement,
        WaitCleanup
    }

    private sealed class ActiveFairWheelSpin : INativeObjectInteractionMovement
    {
        public ActiveFairWheelSpin(
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
        public int MaxTicks => 1300;
        public string StartedAt { get; }
        public FairWheelStage Stage { get; set; }
        public WheelSpinGame? Game { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public int ElapsedTicks { get; set; }
        public int StageTicks { get; set; }
        public bool WagerTextSet { get; set; }
        public bool SawNativeWheel { get; set; }
        public bool SawNativeSettlement { get; set; }
        public bool SawNativeCleanup { get; set; }
        public bool Won { get; set; }
        public double InitialVelocity { get; set; } = double.NaN;
        public double FinalRotation { get; set; } = double.NaN;
        public int FinalScore { get; set; } = -1;
    }

    private void StartFairWheelSpin(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!FairWheelRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "spin_fair_wheel",
                "fair_wheel_spin=requested", FairWheelObservedEffect(null),
                "fair_wheel_typed_request_required"));
            return;
        }
        if (activeFairWheelSpin is not null || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "spin_fair_wheel",
                "fair_wheel_spin=requested", FairWheelObservedEffect(null),
                "fair_wheel_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var festival = location?.currentEvent;
        if (location is null || festival is null || !festival.isFestival || festival.id != "festival_fall16" ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.Ordinal) ||
            Game1.player.festivalScore != request.FairWheelFestivalScoreBefore ||
            Game1.player.LuckLevel != request.FairWheelLuckLevel)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "spin_fair_wheel",
                "festival=fall16", FairWheelObservedEffect(null),
                "fair_wheel_festival_score_or_luck_drifted"));
            return;
        }
        var interaction = new Point(request.FairWheelInteractionTileX!.Value, request.FairWheelInteractionTileY!.Value);
        var stand = new Point(request.FairWheelStandTileX!.Value, request.FairWheelStandTileY!.Value);
        var tileIndex = location.getTileIndexAt(interaction.X, interaction.Y, "Buildings", "untitled tile sheet");
        if (tileIndex is not (308 or 309) || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "spin_fair_wheel",
                "interaction=fair_wheel_stand", FairWheelObservedEffect(null),
                "fair_wheel_endpoint_drifted"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "spin_fair_wheel",
                "route=stand", FairWheelObservedEffect(null),
                "fair_wheel_path_unavailable:" + pathReason));
            return;
        }
        activeFairWheelSpin = new ActiveFairWheelSpin(
            pending, location, festival, interaction, stand, path, maxMovementTiles);
    }

    private static bool FairWheelRequestIsTyped(TrainingExecutionRequest request) =>
        request.FairWheelInteractionTileX.HasValue && request.FairWheelInteractionTileY.HasValue &&
        request.FairWheelStandTileX.HasValue && request.FairWheelStandTileY.HasValue &&
        request.FairWheelFestivalScoreBefore is >= 2 && request.FairWheelStardropPriceStarTokens == 2000 &&
        request.FairWheelProjectedUnclaimedGrangeTokens is >= 0 && request.FairWheelRemainingStarTokenDemand is >= 2 &&
        request.FairWheelSelectedColor == "green" && request.FairWheelWagerStarTokens is >= 1 &&
        request.FairWheelWagerStarTokens == Math.Min(
            request.FairWheelRemainingStarTokenDemand.Value,
            request.FairWheelFestivalScoreBefore.Value * 7 / 15) &&
        request.FairWheelLuckLevel.HasValue && request.FairWheelBaseGreenWins == 22 &&
        request.FairWheelBaseOrangeWins == 8 && request.FairWheelBaseOutcomeCount == 30 &&
        request.FairWheelPrestartDurationMs == 1000 && request.FairWheelResultDurationMs == 2500 &&
        request.FairWheelDialogueKey == "wheelBet" && request.FairWheelResponseKey == "Green" &&
        request.FairWheelWagerPolicy == "green_zero_luck_kelly_7_of_15_capped_by_remaining_stardrop_demand" &&
        !string.IsNullOrWhiteSpace(request.FairWheelProjectionFingerprint) &&
        request.NativeContract == RuntimeFairWheelNativeContract;

    private void TickFairWheelSpin()
    {
        var active = activeFairWheelSpin;
        if (active is null)
            return;
        if (active.Stage == FairWheelStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "fair_wheel", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockFairWheelSpin(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenFairWheelDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockFairWheelSpin(active, "fair_wheel_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation?.currentEvent, active.Festival))
        {
            BlockFairWheelSpin(active, "fair_wheel_festival_event_changed");
            return;
        }

        switch (active.Stage)
        {
            case FairWheelStage.WaitDialogue:
                TickFairWheelDialogue(active);
                break;
            case FairWheelStage.WaitNumberSelection:
            case FairWheelStage.WaitWagerParse:
                TickFairWheelNumberSelection(active);
                break;
            case FairWheelStage.WaitWheel:
                TickFairWheelStart(active);
                break;
            case FairWheelStage.WaitSettlement:
                TickFairWheelSettlement(active);
                break;
            case FairWheelStage.WaitCleanup:
                TickFairWheelCleanup(active);
                break;
        }
    }

    private void OpenFairWheelDialogue(ActiveFairWheelSpin active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Festival.checkAction(
                new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport,
                Game1.player))
        {
            BlockFairWheelSpin(active, "fair_wheel_native_check_action_rejected");
            return;
        }
        active.Stage = FairWheelStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickFairWheelDialogue(ActiveFairWheelSpin active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockFairWheelSpin(active, "fair_wheel_dialogue_open_timeout");
            return;
        }
        if (active.Location.lastQuestionKey != "wheelBet")
        {
            BlockFairWheelSpin(active, "fair_wheel_dialogue_key_mismatch");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses, row => row.responseKey == "Green");
        if (responseIndex < 0)
        {
            BlockFairWheelSpin(active, "fair_wheel_green_response_missing");
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null ||
            responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockFairWheelSpin(active, "fair_wheel_dialogue_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        if (menu.selectedResponse != responseIndex)
        {
            BlockFairWheelSpin(active, "fair_wheel_green_hover_rejected");
            return;
        }
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        active.Stage = FairWheelStage.WaitNumberSelection;
        active.StageTicks = 0;
    }

    private void TickFairWheelNumberSelection(ActiveFairWheelSpin active)
    {
        if (Game1.activeClickableMenu is not NumberSelectionMenu menu)
        {
            if (active.StageTicks > 180)
                BlockFairWheelSpin(active, "fair_wheel_number_selection_timeout");
            return;
        }
        var request = active.Pending.Request;
        var minimum = ReadRuntimeFairWheelInt(menu, RuntimeFairWheelNumberMinimumField);
        var maximum = ReadRuntimeFairWheelInt(menu, RuntimeFairWheelNumberMaximumField);
        var price = ReadRuntimeFairWheelInt(menu, RuntimeFairWheelNumberPriceField);
        var textBox = RuntimeFairWheelNumberTextBoxField?.GetValue(menu) as TextBox;
        if (minimum != 1 || maximum != request.FairWheelFestivalScoreBefore || price != -1 || textBox is null)
        {
            BlockFairWheelSpin(active, "fair_wheel_number_selection_contract_mismatch");
            return;
        }
        if (!active.WagerTextSet)
        {
            textBox.Text = request.FairWheelWagerStarTokens!.Value.ToString();
            if (textBox.Text != request.FairWheelWagerStarTokens.Value.ToString())
            {
                BlockFairWheelSpin(active, "fair_wheel_wager_text_rejected");
                return;
            }
            active.WagerTextSet = true;
            active.Stage = FairWheelStage.WaitWagerParse;
            active.StageTicks = 0;
            return;
        }
        if (ReadRuntimeFairWheelInt(menu, RuntimeFairWheelNumberCurrentField) != request.FairWheelWagerStarTokens)
        {
            if (active.StageTicks > 30)
                BlockFairWheelSpin(active, "fair_wheel_wager_parse_timeout");
            return;
        }
        menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
        if (Game1.player.festivalScore != request.FairWheelFestivalScoreBefore || !active.Festival.specialEventVariable2)
        {
            BlockFairWheelSpin(active, "fair_wheel_native_wager_submission_receipt_mismatch");
            return;
        }
        active.Stage = FairWheelStage.WaitWheel;
        active.StageTicks = 0;
    }

    private void TickFairWheelStart(ActiveFairWheelSpin active)
    {
        if (Game1.activeClickableMenu is WheelSpinGame game)
        {
            var wager = ReadRuntimeFairWheelInt(game, RuntimeFairWheelWagerField);
            var timer = ReadRuntimeFairWheelInt(game, RuntimeFairWheelTimerField);
            if (wager != active.Pending.Request.FairWheelWagerStarTokens || timer is < 0 or > 1000 ||
                !active.Festival.specialEventVariable2 || game.arrowRotationVelocity <= 0d ||
                game.arrowRotationDeceleration != -0.0006283185307179586d)
            {
                BlockFairWheelSpin(active, "fair_wheel_native_instance_mismatch");
                return;
            }
            active.Game = game;
            active.InitialVelocity = game.arrowRotationVelocity;
            active.SawNativeWheel = true;
            active.Stage = FairWheelStage.WaitSettlement;
            active.StageTicks = 0;
            return;
        }
        if (active.StageTicks > 120)
            BlockFairWheelSpin(active, "fair_wheel_native_start_timeout");
    }

    private void TickFairWheelSettlement(ActiveFairWheelSpin active)
    {
        var game = active.Game!;
        if (ReferenceEquals(Game1.activeClickableMenu, game))
        {
            if (!ReadRuntimeFairWheelBool(game, RuntimeFairWheelDoneField))
                return;
            var request = active.Pending.Request;
            var winScore = request.FairWheelFestivalScoreBefore + request.FairWheelWagerStarTokens;
            var lossScore = request.FairWheelFestivalScoreBefore - request.FairWheelWagerStarTokens;
            if (Game1.player.festivalScore != winScore && Game1.player.festivalScore != lossScore ||
                RuntimeFairWheelResultTextField?.GetValue(game) is null || game.arrowRotationVelocity != 0d)
            {
                BlockFairWheelSpin(active, "fair_wheel_native_settlement_mismatch");
                return;
            }
            active.SawNativeSettlement = true;
            active.Won = Game1.player.festivalScore == winScore;
            active.FinalScore = Game1.player.festivalScore;
            active.FinalRotation = game.arrowRotation;
            active.Stage = FairWheelStage.WaitCleanup;
            active.StageTicks = 0;
            return;
        }
        BlockFairWheelSpin(active, "fair_wheel_menu_closed_before_settlement_receipt");
    }

    private void TickFairWheelCleanup(ActiveFairWheelSpin active)
    {
        if (ReferenceEquals(Game1.activeClickableMenu, active.Game))
            return;
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || !Game1.player.CanMove)
        {
            if (active.StageTicks > 240)
                BlockFairWheelSpin(active, "fair_wheel_native_cleanup_timeout");
            return;
        }
        active.SawNativeCleanup = true;
        CompleteFairWheelSpin(active);
    }

    private void CompleteFairWheelSpin(ActiveFairWheelSpin active)
    {
        var request = active.Pending.Request;
        var wager = request.FairWheelWagerStarTokens!.Value;
        var expectedFinal = request.FairWheelFestivalScoreBefore!.Value + (active.Won ? wager : -wager);
        var verified = active.SawNativeWheel && active.SawNativeSettlement && active.SawNativeCleanup &&
            active.FinalScore == expectedFinal && Game1.player.festivalScore == expectedFinal &&
            ReferenceEquals(active.Location.currentEvent, active.Festival);
        activeFairWheelSpin = null;
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
            PrimitiveKind = "spin_fair_wheel",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_festival_checkAction_wheelBet_dialogue_verified",
                    "native_Green_response_and_NumberSelectionMenu_exact_wager_verified",
                    "real_WheelSpinGame_random_rotation_and_settlement_verified",
                    active.Won ? "native_random_win_plus_wager_verified" : "native_random_loss_minus_wager_verified",
                    "native_result_text_and_menu_cleanup_verified"
                }
                : new[] { "fair_wheel_post_state_mismatch" },
            RequestedEffect = "selected_color=green;wager=" + wager + ";festival_score=stochastic_plus_or_minus_wager",
            ObservedEffect = FairWheelObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fair_wheel_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.festival_score",
                        Before = request.FairWheelFestivalScoreBefore.Value.ToString(),
                        After = Game1.player.festivalScore.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockFairWheelSpin(ActiveFairWheelSpin active, string reason)
    {
        activeFairWheelSpin = null;
        StopAllMovement();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "spin_fair_wheel", "selected_color=green;festival_score=stochastic_plus_or_minus_wager",
            FairWheelObservedEffect(active), reason));
    }

    private static int ReadRuntimeFairWheelInt(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is int value ? value : int.MinValue;

    private static bool ReadRuntimeFairWheelBool(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is bool value && value;

    private static string FairWheelObservedEffect(ActiveFairWheelSpin? active)
    {
        var game = active?.Game ?? Game1.activeClickableMenu as WheelSpinGame;
        return "festival=" + (active?.Festival.id ?? Game1.currentLocation?.currentEvent?.id ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";selected_color=" + ((active?.Festival ?? Game1.currentLocation?.currentEvent)?.specialEventVariable2 == true ? "green" : "orange") +
            ";wager=" + (active?.Pending.Request.FairWheelWagerStarTokens?.ToString() ?? "unavailable") +
            ";festival_score=" + Game1.player.festivalScore +
            ";outcome=" + (active?.SawNativeSettlement == true ? active.Won ? "win" : "loss" : "unsettled") +
            ";initial_velocity=" + (active?.InitialVelocity.ToString("R") ?? "unavailable") +
            ";final_rotation=" + (active?.FinalRotation.ToString("R") ?? "unavailable") +
            ";timer_before_start=" + (game is null ? "unavailable" : ReadRuntimeFairWheelInt(game, RuntimeFairWheelTimerField).ToString()) +
            ";done_spinning=" + (game is not null && ReadRuntimeFairWheelBool(game, RuntimeFairWheelDoneField)).ToString().ToLowerInvariant() +
            ";result_text=" + (game is not null && RuntimeFairWheelResultTextField?.GetValue(game) is not null).ToString().ToLowerInvariant();
    }
}
