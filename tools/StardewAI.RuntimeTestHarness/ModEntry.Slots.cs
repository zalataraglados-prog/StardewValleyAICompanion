using System.Globalization;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeSlotsNativeContract =
        "ClubSlots_checkAction_then_native_Slots_10_or_100_spin_then_native_random_settlement_then_done";
    private static readonly FieldInfo? RuntimeSlotsResultsField = RuntimePrivateField<Slots>("slotResults");
    private static readonly FieldInfo? RuntimeSlotsSpin10Field = RuntimePrivateField<Slots>("spinButton10");
    private static readonly FieldInfo? RuntimeSlotsSpin100Field = RuntimePrivateField<Slots>("spinButton100");
    private static readonly FieldInfo? RuntimeSlotsDoneField = RuntimePrivateField<Slots>("doneButton");

    private void StartSlots(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!SlotsRequestIsTyped(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_slots",
                "slots=one_native_spin", SlotsObservedEffect(null), "slots_typed_request_required"));
            return;
        }
        if (RuntimeSlotsResultsField is null || RuntimeSlotsSpin10Field is null ||
            RuntimeSlotsSpin100Field is null || RuntimeSlotsDoneField is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_slots",
                "slots=one_native_spin", SlotsObservedEffect(null), "slots_1_6_15_reflection_contract_unavailable"));
            return;
        }
        if (activeSlots is not null || HasActiveExecutorOperation() || Game1.currentMinigame is not null ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.eventUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_slots",
                "slots=one_native_spin", SlotsObservedEffect(null), "slots_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var currentAction = location?.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
        var exactWorldState = location is Club &&
            string.Equals(location.NameOrUniqueName, "Club", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.LocationId, "Club", StringComparison.OrdinalIgnoreCase) &&
            Game1.player.hasClubCard && Game1.player.clubCoins == request.SlotsClubCoinsBefore &&
            Club.timesPlayedSlots == request.SlotsTimesPlayedBefore &&
            Math.Abs(Game1.player.DailyLuck - request.SlotsDailyLuck!.Value) < 0.000000001d &&
            Game1.player.LuckLevel == request.SlotsLuckLevel &&
            !Game1.player.craftingRecipes.ContainsKey("Deluxe Scarecrow") &&
            !Game1.player.hasOrWillReceiveMail("RarecrowSociety") &&
            !Utility.doesItemExistAnywhere("(BC)126");
        if (!exactWorldState || !string.Equals(currentAction, request.SlotsActionRaw, StringComparison.Ordinal) ||
            !AreAdjacent(stand, interaction) || location is null || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_slots",
                "slots=one_native_spin", SlotsObservedEffect(null), "slots_endpoint_or_transparent_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_slots",
                "route=slots_machine_stand", SlotsObservedEffect(null), "slots_path_unavailable:" + pathReason));
            return;
        }
        activeSlots = new ActiveSlots(pending, location, interaction, stand, path, maxMovementTiles);
    }

    private static bool SlotsRequestIsTyped(TrainingExecutionRequest request)
    {
        var bet = request.SlotsBet;
        var coins = request.SlotsClubCoinsBefore;
        var luckMultiplier = request.SlotsLuckMultiplier;
        var expectedPayout = request.SlotsExpectedPayoutMultiplier;
        var expectedNet = request.SlotsExpectedNetCoinDelta;
        return request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
            request.StandTileX.HasValue && request.StandTileY.HasValue && request.LocationId == "Club" &&
            request.SlotsActionToken == "ClubSlots" && request.SlotsActionRaw == "ClubSlots" &&
            bet is 10 or 100 && coins.HasValue && coins.Value >= bet.Value &&
            request.SlotsTargetClubCoins == 10000 && request.SlotsRemainingClubCoinDemand is > 0 &&
            request.SlotsTargetItemId == "(BC)126" && request.SlotsTimesPlayedBefore is >= 0 &&
            request.SlotsDailyLuck.HasValue && request.SlotsLuckLevel.HasValue && luckMultiplier.HasValue &&
            expectedPayout is > 0d && expectedNet.HasValue &&
            Math.Abs(luckMultiplier.Value - (1d + request.SlotsDailyLuck.Value * 2d + request.SlotsLuckLevel.Value * 0.08d)) < 1e-12 &&
            Math.Abs(expectedNet.Value - bet.Value * (expectedPayout.Value - 1d)) < 1e-8 &&
            !string.IsNullOrWhiteSpace(request.SlotsProjectionFingerprint) &&
            !string.IsNullOrWhiteSpace(request.SlotsPayoutRowsJson) &&
            request.SlotsRngContract == "shared_Game1.random_live_feedback_not_stable_future_prediction" &&
            request.SlotsExitPolicy == "done_after_one_native_settlement" &&
            request.NativeContract == RuntimeSlotsNativeContract;
    }

    private void TickSlots()
    {
        var active = activeSlots;
        if (active is null)
            return;
        if (active.Stage == SlotsStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "slots", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockSlots(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenSlots(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockSlots(active, "slots_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation, active.Location))
        {
            BlockSlots(active, "slots_location_changed");
            return;
        }
        switch (active.Stage)
        {
            case SlotsStage.WaitStart:
                TickSlotsStart(active);
                break;
            case SlotsStage.WaitSettlement:
                TickSlotsSettlement(active);
                break;
            case SlotsStage.WaitDone:
                TickSlotsDone(active);
                break;
        }
    }

    private void OpenSlots(ActiveSlots active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Location.checkAction(new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport, Game1.player))
        {
            BlockSlots(active, "slots_native_check_action_rejected");
            return;
        }
        active.Stage = SlotsStage.WaitStart;
        active.StageTicks = 0;
    }

    private void TickSlotsStart(ActiveSlots active)
    {
        if (Game1.currentMinigame is not Slots game)
        {
            if (Game1.currentMinigame is not null || active.StageTicks > 180)
                BlockSlots(active, "slots_native_start_timeout_or_wrong_minigame");
            return;
        }
        var request = active.Pending.Request;
        if (game.spinning || Game1.player.clubCoins != request.SlotsClubCoinsBefore ||
            Club.timesPlayedSlots != request.SlotsTimesPlayedBefore)
        {
            BlockSlots(active, "slots_native_initial_state_mismatch");
            return;
        }
        active.Game = game;
        var button = request.SlotsBet == 100 ? RuntimeSlotsSpin100Field : RuntimeSlotsSpin10Field;
        if (!ClickSlotsComponent(game, button) || !game.spinning || game.currentBet != request.SlotsBet ||
            Club.timesPlayedSlots != request.SlotsTimesPlayedBefore + 1 ||
            Game1.player.clubCoins != request.SlotsClubCoinsBefore - request.SlotsBet)
        {
            BlockSlots(active, "slots_native_spin_button_or_debit_mismatch");
            return;
        }
        active.NativeSpinStarted = true;
        active.Stage = SlotsStage.WaitSettlement;
        active.StageTicks = 0;
    }

    private void TickSlotsSettlement(ActiveSlots active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            BlockSlots(active, "slots_minigame_disappeared_before_settlement");
            return;
        }
        if (game.spinning || game.endTimer > 0)
            return;
        var results = ReadSlotsResults(game);
        var payout = game.payoutModifier;
        var request = active.Pending.Request;
        var expectedCoins = request.SlotsClubCoinsBefore!.Value - request.SlotsBet!.Value +
            (int)(request.SlotsBet.Value * payout);
        if (results.Length != 3 || !SlotsPayoutMatchesResults(results, payout) ||
            game.currentBet != request.SlotsBet || Game1.player.clubCoins != expectedCoins ||
            Club.timesPlayedSlots != request.SlotsTimesPlayedBefore + 1 || game.showResult != (payout > 0f))
        {
            BlockSlots(active, "slots_native_result_pattern_or_settlement_mismatch");
            return;
        }
        active.ResultIcons = results;
        active.ObservedPayoutMultiplier = payout;
        active.ObservedCoinDelta = Game1.player.clubCoins - request.SlotsClubCoinsBefore.Value;
        active.SettlementVerified = true;
        if (!ClickSlotsComponent(game, RuntimeSlotsDoneField))
        {
            BlockSlots(active, "slots_native_done_component_unavailable");
            return;
        }
        active.Stage = SlotsStage.WaitDone;
        active.StageTicks = 0;
    }

    private void TickSlotsDone(ActiveSlots active)
    {
        if (Game1.currentMinigame is not null)
        {
            if (active.StageTicks > 180)
                BlockSlots(active, "slots_native_done_timeout");
            return;
        }
        var request = active.Pending.Request;
        var verified = active.NativeSpinStarted && active.SettlementVerified &&
            Club.timesPlayedSlots == request.SlotsTimesPlayedBefore + 1 &&
            Game1.player.clubCoins - request.SlotsClubCoinsBefore == active.ObservedCoinDelta;
        activeSlots = null;
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
            PrimitiveKind = "play_slots",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_ClubSlots_checkAction_and_exact_bet_button_verified",
                    "native_shared_rng_result_icons_and_payout_multiplier_verified",
                    "native_club_coin_settlement_times_played_and_done_cleanup_verified"
                }
                : new[] { "slots_post_state_mismatch" },
            RequestedEffect = "slots_spin=1;bet=" + request.SlotsBet,
            ObservedEffect = SlotsObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "slots_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.club_coins",
                        Before = request.SlotsClubCoinsBefore!.Value.ToString(CultureInfo.InvariantCulture),
                        After = Game1.player.clubCoins.ToString(CultureInfo.InvariantCulture)
                    },
                    new SimulatedFactChange
                    {
                        Path = "world.club.times_played_slots",
                        Before = request.SlotsTimesPlayedBefore!.Value.ToString(CultureInfo.InvariantCulture),
                        After = Club.timesPlayedSlots.ToString(CultureInfo.InvariantCulture)
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockSlots(ActiveSlots active, string reason)
    {
        activeSlots = null;
        StopAllMovement();
        if (active.Game is { } game && ReferenceEquals(Game1.currentMinigame, game))
        {
            if (!game.spinning)
                ClickSlotsComponent(game, RuntimeSlotsDoneField);
            if (ReferenceEquals(Game1.currentMinigame, game))
                Game1.currentMinigame = null;
        }
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_slots", "slots_spin=1", SlotsObservedEffect(active), reason));
    }

    private static bool ClickSlotsComponent(Slots game, FieldInfo? field)
    {
        if (field?.GetValue(game) is not ClickableComponent component)
            return false;
        game.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y, playSound: false);
        return true;
    }

    private static float[] ReadSlotsResults(Slots game) =>
        RuntimeSlotsResultsField?.GetValue(game) is List<float> values ? values.ToArray() : Array.Empty<float>();

    private static bool SlotsPayoutMatchesResults(float[] values, float payout)
    {
        var icons = values.Select(value => (int)value).ToArray();
        var sevens = icons.Count(value => value == 7);
        return payout switch
        {
            2500f => icons.All(value => value == 5),
            1000f => icons.All(value => value == 6),
            500f => icons.All(value => value == 7),
            200f => icons.All(value => value == 4),
            120f => icons.All(value => value == 3),
            80f => icons.All(value => value == 2),
            30f => icons.All(value => value == 1),
            5f => icons.All(value => value == 0),
            3f => sevens == 2,
            2f => sevens == 1,
            0f => sevens == 0 && icons.GroupBy(value => value).All(group => group.Count() <= 2),
            _ => false
        };
    }

    private static string SlotsObservedEffect(ActiveSlots? active)
    {
        var game = active?.Game ?? Game1.currentMinigame as Slots;
        var results = active?.ResultIcons ?? (game is null ? Array.Empty<float>() : ReadSlotsResults(game));
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
            ";club_coins=" + Game1.player.clubCoins +
            ";times_played=" + Club.timesPlayedSlots +
            ";bet=" + (game?.currentBet.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";payout_multiplier=" + (active?.ObservedPayoutMultiplier.ToString(CultureInfo.InvariantCulture) ?? game?.payoutModifier.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";result_icons=" + string.Join(",", results.Select(value => ((int)value).ToString(CultureInfo.InvariantCulture))) +
            ";coin_delta=" + (active?.ObservedCoinDelta.ToString(CultureInfo.InvariantCulture) ?? "unavailable");
    }
}
