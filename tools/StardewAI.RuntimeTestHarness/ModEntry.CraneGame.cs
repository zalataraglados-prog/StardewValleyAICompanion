using System.Globalization;
using System.Reflection;
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
    private const string RuntimeCraneNativeContract =
        "MovieTheater_CraneGame_checkAction_then_yes_500g_then_native_CraneGame_directional_input_then_native_ItemGrabMenu_rewards";

    private static readonly FieldInfo? RuntimeCraneLogicClawField = RuntimePrivateField<CraneGame.GameLogic>("_claw");
    private static readonly FieldInfo? RuntimeCranePrizeItemField = RuntimePrivateField<CraneGame.Prize>("_item");
    private static readonly FieldInfo? RuntimeCranePrizeConveyorField = RuntimePrivateField<CraneGame.Prize>("_conveyerBeltMove");

    private void StartCraneGame(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!CraneGameRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_crane_game",
                "crane_game=one_native_paid_session", CraneGameObservedEffect(null),
                "crane_game_typed_request_required"));
            return;
        }
        if (RuntimeCraneLogicClawField is null || RuntimeCranePrizeItemField is null ||
            RuntimeCranePrizeConveyorField is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_crane_game",
                "crane_game=one_native_paid_session", CraneGameObservedEffect(null),
                "crane_game_1_6_15_reflection_contract_unavailable"));
            return;
        }
        if (activeCraneGame is not null || HasActiveExecutorOperation() || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_crane_game",
                "crane_game=one_native_paid_session", CraneGameObservedEffect(null),
                "crane_game_player_busy"));
            return;
        }

        var location = Game1.currentLocation as MovieTheater;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var occupied = location?.Map?.GetLayer("Buildings")?.Tiles[2, 9] is not null;
        var action = location?.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
        var exact = location is not null && location.NameOrUniqueName == "MovieTheater" &&
            request.LocationId == "MovieTheater" && Game1.player.Money == request.CraneMoneyBefore &&
            Game1.player.Money >= request.CraneFeeGold &&
            Game1.player.Items.Count(item => item is null) == request.CraneEmptySlotsBefore &&
            !occupied && string.Equals(action, request.CraneActionRaw, StringComparison.Ordinal) &&
            AreAdjacent(stand, interaction) && IsTileOnMap(location, stand) &&
            IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand);
        if (!exact)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_crane_game",
                "crane_game=one_native_paid_session", CraneGameObservedEffect(null),
                "crane_game_endpoint_or_transparent_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location!, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_crane_game",
                "route=crane_game_machine_stand", CraneGameObservedEffect(null),
                "crane_game_path_unavailable:" + pathReason));
            return;
        }

        activeCraneGame = new ActiveCraneGame(
            pending, location!, interaction, stand, path, maxMovementTiles, CraneInventoryCounts());
    }

    private static bool CraneGameRequestIsTyped(TrainingExecutionRequest request) =>
        request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue &&
        request.LocationId == "MovieTheater" && request.CraneActionRaw == "CraneGame" &&
        request.CraneActionToken == "CraneGame" && request.CraneYesResponseKey == "Yes" &&
        request.CraneFeeGold == 500 && request.CraneMoneyBefore is >= 500 &&
        request.CraneEmptySlotsBefore is >= 3 && request.CraneAttempts == 3 &&
        request.CraneTimerTicksPerAttempt == 900 &&
        request.CraneSelectionPolicy == "best_reachable_live_prize_nonlarge_stationary_then_distance;refresh_each_attempt" &&
        request.CraneExitPolicy == "finish_three_attempts_then_collect_all_native_rewards" &&
        !string.IsNullOrWhiteSpace(request.CraneProjectionFingerprint) &&
        request.NativeContract == RuntimeCraneNativeContract;

    private void TickCraneGame()
    {
        var active = activeCraneGame;
        if (active is null)
            return;
        if (active.Stage == CraneGameStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "crane_game", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockCraneGame(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenCraneGameDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockCraneGame(active, "crane_game_timeout");
            return;
        }

        switch (active.Stage)
        {
            case CraneGameStage.WaitDialogue:
                TickCraneGameDialogue(active);
                break;
            case CraneGameStage.WaitMinigame:
                TickCraneGameStart(active);
                break;
            case CraneGameStage.Play:
                TickCraneGamePlay(active);
                break;
            case CraneGameStage.WaitRewardMenu:
                TickCraneGameRewardMenu(active);
                break;
            case CraneGameStage.TransferRewards:
                TickCraneGameRewardTransfer(active);
                break;
            case CraneGameStage.Verify:
                CompleteCraneGame(active);
                break;
        }
    }

    private void OpenCraneGameDialogue(ActiveCraneGame active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        active.NativeCheckActionHandled = active.Location.checkAction(
            new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
            Game1.viewport,
            Game1.player);
        if (!active.NativeCheckActionHandled)
        {
            BlockCraneGame(active, "crane_game_native_check_action_rejected");
            return;
        }
        active.Stage = CraneGameStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickCraneGameDialogue(ActiveCraneGame active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockCraneGame(active, "crane_game_dialogue_open_timeout");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses,
            response => string.Equals(response.responseKey, active.Pending.Request.CraneYesResponseKey, StringComparison.OrdinalIgnoreCase));
        if (responseIndex < 0 || menu.transitioning || menu.safetyTimer > 0 ||
            menu.responseCC is null || responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockCraneGame(active, "crane_game_yes_response_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        active.NativeFeeObserved = Game1.player.Money == active.MoneyBefore - 500;
        active.Stage = CraneGameStage.WaitMinigame;
        active.StageTicks = 0;
    }

    private void TickCraneGameStart(ActiveCraneGame active)
    {
        if (Game1.currentMinigame is CraneGame game)
        {
            if (!active.NativeFeeObserved || Game1.player.Money != active.MoneyBefore - 500)
            {
                BlockCraneGame(active, "crane_game_native_fee_not_observed");
                return;
            }
            var logic = game.GetObjectOfType<CraneGame.GameLogic>();
            var claw = logic is null ? null : RuntimeCraneLogicClawField?.GetValue(logic) as CraneGame.Claw;
            if (logic is null || claw is null || logic.maxLives != 3 || logic.lives != 3)
            {
                BlockCraneGame(active, "crane_game_native_logic_contract_mismatch");
                return;
            }
            active.Game = game;
            active.Logic = logic;
            active.Claw = claw;
            active.Stage = CraneGameStage.Play;
            active.StageTicks = 0;
            return;
        }
        if (Game1.currentMinigame is not null || active.StageTicks > 360)
            BlockCraneGame(active, "crane_game_native_start_timeout_or_wrong_minigame");
    }

    private void TickCraneGamePlay(ActiveCraneGame active)
    {
        var game = active.Game!;
        var logic = active.Logic!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            ReleaseCraneGameInput();
            active.Stage = CraneGameStage.WaitRewardMenu;
            active.StageTicks = 0;
            return;
        }

        var state = logic.GetCurrentState();
        if (state == CraneGame.GameLogic.GameStates.ClawReset)
            active.Target = null;
        if (state == CraneGame.GameLogic.GameStates.EndGame)
        {
            active.Stage = CraneGameStage.WaitRewardMenu;
            active.StageTicks = 0;
        }
    }

    private bool ApplyCraneGameInput(ActiveCraneGame active, out string reason)
    {
        reason = string.Empty;
        if (active.Stage != CraneGameStage.Play ||
            active.Game is null || active.Logic is null || active.Claw is null ||
            !ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            ReleaseCraneGameInput();
            return true;
        }

        var state = active.Logic.GetCurrentState();
        var holdRight = false;
        var holdDown = false;
        if (state == CraneGame.GameLogic.GameStates.Idle)
        {
            if (active.Target is null)
            {
                active.Target = SelectCraneGameTarget(active, active.Claw);
                if (active.Target is null)
                {
                    reason = "crane_game_no_reachable_live_prize";
                    return false;
                }
                active.AttemptedPrizes.Add(active.Target);
                active.AttemptsStarted++;
            }
            holdRight = true;
        }
        else if (state == CraneGame.GameLogic.GameStates.MoveClawRight)
        {
            var targetX = active.Target?.position.X ?? active.Claw.position.X;
            holdRight = active.Claw.position.X + 0.25f < targetX;
        }
        else if (state == CraneGame.GameLogic.GameStates.WaitForMoveDown)
        {
            holdDown = true;
        }
        else if (state == CraneGame.GameLogic.GameStates.MoveClawDown)
        {
            var targetY = active.Target?.position.Y ?? active.Claw.position.Y;
            holdDown = active.Claw.position.Y + 0.25f < targetY;
        }

        if (!TryApplySmapiButtonOverride(SButton.D, holdRight, out reason))
            return false;
        return TryApplySmapiButtonOverride(SButton.S, holdDown, out reason);
    }

    private static CraneGame.Prize? SelectCraneGameTarget(ActiveCraneGame active, CraneGame.Claw claw) =>
        active.Game!.GetObjectsOfType<CraneGame.Prize>()
            .Where(prize => prize.CanBeGrabbed() && !active.AttemptedPrizes.Contains(prize) &&
                prize.position.X >= claw.position.X - 0.25f && prize.position.Y >= claw.position.Y - 0.25f)
            .OrderBy(prize => prize.isLargeItem)
            .ThenBy(prize => CraneConveyorMagnitude(prize) > 0.001f)
            .ThenBy(prize => Vector2.DistanceSquared(prize.position, claw.position))
            .FirstOrDefault();

    private static float CraneConveyorMagnitude(CraneGame.Prize prize) =>
        RuntimeCranePrizeConveyorField?.GetValue(prize) is Vector2 movement ? movement.LengthSquared() : 0f;

    private void TickCraneGameRewardMenu(ActiveCraneGame active)
    {
        if (Game1.currentMinigame is not null)
            return;
        if (Game1.activeClickableMenu is ItemGrabMenu menu)
        {
            foreach (var item in menu.ItemsToGrabMenu.actualInventory.Where(item => item is not null))
            {
                active.ExpectedRewards.TryGetValue(item!.QualifiedItemId, out var count);
                active.ExpectedRewards[item.QualifiedItemId] = count + item.Stack;
            }
            active.Stage = CraneGameStage.TransferRewards;
            active.StageTicks = 0;
            return;
        }
        if (Game1.activeClickableMenu is null)
        {
            active.Stage = CraneGameStage.Verify;
            return;
        }
        if (active.StageTicks > 300)
            BlockCraneGame(active, "crane_game_unexpected_reward_menu");
    }

    private void TickCraneGameRewardTransfer(ActiveCraneGame active)
    {
        if (Game1.activeClickableMenu is not ItemGrabMenu menu)
        {
            active.Stage = CraneGameStage.Verify;
            return;
        }
        var inventory = menu.ItemsToGrabMenu.actualInventory;
        var slot = inventory.Select((item, index) => new { item, index }).FirstOrDefault(row => row.item is not null);
        if (slot is null)
        {
            if (!menu.readyToClose())
            {
                BlockCraneGame(active, "crane_game_reward_menu_not_ready_to_close");
                return;
            }
            Game1.exitActiveMenu();
            active.Stage = CraneGameStage.Verify;
            return;
        }
        if (!Game1.player.couldInventoryAcceptThisItem(slot.item!))
        {
            BlockCraneGame(active, "crane_game_reward_inventory_capacity_drifted");
            return;
        }
        var position = InventorySlotScreenPosition(menu.ItemsToGrabMenu, slot.index);
        if (!position.HasValue)
        {
            BlockCraneGame(active, "crane_game_reward_slot_position_unavailable");
            return;
        }
        menu.receiveLeftClick(position.Value.X, position.Value.Y, playSound: true);
        active.RewardsTransferred++;
    }

    private void CompleteCraneGame(ActiveCraneGame active)
    {
        var after = CraneInventoryCounts();
        var rewardMatch = active.ExpectedRewards.All(pair =>
            after.GetValueOrDefault(pair.Key) - active.InventoryBefore.GetValueOrDefault(pair.Key) == pair.Value);
        var verified = active.NativeCheckActionHandled && active.NativeFeeObserved &&
            active.MoneyBefore == active.Pending.Request.CraneMoneyBefore &&
            Game1.player.Money == active.MoneyBefore - 500 && active.AttemptsStarted == 3 &&
            Game1.currentMinigame is null && Game1.activeClickableMenu is null && rewardMatch;
        ReleaseCraneGameInput();
        activeCraneGame = null;
        var reasons = verified
            ? new[]
            {
                "shared_native_object_interaction_movement_reached_exact_adjacent_stand",
                "native_MovieTheater_CraneGame_action_and_yes_response_deducted_exact_500g",
                "three_native_CraneGame_attempts_used_live_prize_physics_and_directional_input",
                "native_ItemGrabMenu_transferred_every_collected_reward",
                "money_inventory_reward_and_minigame_cleanup_receipt_verified"
            }
            : new[] { "crane_game_native_receipt_mismatch" };
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "play_crane_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "money_delta=-500;native_crane_attempts=3;native_reward_menu_settled=true",
            ObservedEffect = CraneGameObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : reasons
        });
    }

    private void BlockCraneGame(ActiveCraneGame active, string reason)
    {
        ReleaseCraneGameInput();
        if (ReferenceEquals(Game1.currentMinigame, active.Game))
            active.Game?.forceQuit();
        activeCraneGame = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_crane_game", "crane_game=one_native_paid_session",
            CraneGameObservedEffect(active), reason));
    }

    private void ReleaseCraneGameInput()
    {
        TryApplySmapiButtonOverride(SButton.D, pressed: false, out _);
        TryApplySmapiButtonOverride(SButton.S, pressed: false, out _);
    }

    private static Dictionary<string, int> CraneInventoryCounts() =>
        Game1.player.Items.Where(item => item is not null)
            .GroupBy(item => item!.QualifiedItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item!.Stack), StringComparer.Ordinal);

    private static string CraneGameObservedEffect(ActiveCraneGame? active) =>
        "money=" + Game1.player.Money.ToString(CultureInfo.InvariantCulture) +
        ";attempts_started=" + (active?.AttemptsStarted ?? 0).ToString(CultureInfo.InvariantCulture) +
        ";rewards_transferred=" + (active?.RewardsTransferred ?? 0).ToString(CultureInfo.InvariantCulture) +
        ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
        ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
}
