using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeCalicoJackNativeContract =
        "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit";

    private static readonly FieldInfo? RuntimeCalicoCurrentBetField = RuntimePrivateField<CalicoJack>("currentBet");
    private static readonly FieldInfo? RuntimeCalicoStartTimerField = RuntimePrivateField<CalicoJack>("startTimer");
    private static readonly FieldInfo? RuntimeCalicoDealerTurnTimerField = RuntimePrivateField<CalicoJack>("dealerTurnTimer");
    private static readonly FieldInfo? RuntimeCalicoBustTimerField = RuntimePrivateField<CalicoJack>("bustTimer");
    private static readonly FieldInfo? RuntimeCalicoShowingResultsField = RuntimePrivateField<CalicoJack>("showingResultsScreen");
    private static readonly FieldInfo? RuntimeCalicoPlayerWonField = RuntimePrivateField<CalicoJack>("playerWon");
    private static readonly FieldInfo? RuntimeCalicoHighStakesField = RuntimePrivateField<CalicoJack>("highStakes");
    private static readonly FieldInfo? RuntimeCalicoHitField = RuntimePrivateField<CalicoJack>("hit");
    private static readonly FieldInfo? RuntimeCalicoStandField = RuntimePrivateField<CalicoJack>("stand");
    private static readonly FieldInfo? RuntimeCalicoQuitField = RuntimePrivateField<CalicoJack>("quit");

    private void StartCalicoJack(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!CalicoJackRequestIsTyped(request, out var expectedPlayerCards, out var expectedDealerCards))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_calico_jack",
                "calico_jack=one_native_round", CalicoJackObservedEffect(null),
                "calico_jack_typed_request_required"));
            return;
        }
        if (RuntimeCalicoCurrentBetField is null || RuntimeCalicoStartTimerField is null ||
            RuntimeCalicoDealerTurnTimerField is null || RuntimeCalicoBustTimerField is null ||
            RuntimeCalicoShowingResultsField is null || RuntimeCalicoPlayerWonField is null ||
            RuntimeCalicoHighStakesField is null || RuntimeCalicoHitField is null ||
            RuntimeCalicoStandField is null || RuntimeCalicoQuitField is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_calico_jack",
                "calico_jack=one_native_round", CalicoJackObservedEffect(null),
                "calico_jack_1_6_15_reflection_contract_unavailable"));
            return;
        }
        if (activeCalicoJack is not null || HasActiveExecutorOperation() ||
            Game1.currentMinigame is not null || Game1.activeClickableMenu is not null ||
            Game1.dialogueUp || Game1.eventUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_calico_jack",
                "calico_jack=one_native_round", CalicoJackObservedEffect(null),
                "calico_jack_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var interaction = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var currentAction = location?.doesTileHaveProperty(interaction.X, interaction.Y, "Action", "Buildings");
        var expectedTimesPlayed = request.CalicoTimesPlayedSeed!.Value;
        var exactWorldState = location is Club &&
            string.Equals(location.NameOrUniqueName, "Club", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.LocationId, "Club", StringComparison.OrdinalIgnoreCase) &&
            Game1.player.hasClubCard &&
            Game1.player.clubCoins == request.CalicoClubCoinsBefore &&
            Club.timesPlayedCalicoJack + 1 == expectedTimesPlayed &&
            Game1.stats.DaysPlayed == request.CalicoDaysPlayedSeed &&
            Game1.uniqueIDForThisGame.ToString(CultureInfo.InvariantCulture) == request.CalicoUniqueGameIdSeed &&
            Math.Abs(Game1.player.DailyLuck - request.CalicoDailyLuck!.Value) < 0.000000001d &&
            Game1.player.LuckLevel == request.CalicoLuckLevel &&
            !Game1.player.craftingRecipes.ContainsKey("Deluxe Scarecrow") &&
            !Game1.player.hasOrWillReceiveMail("RarecrowSociety") &&
            !Utility.doesItemExistAnywhere("(BC)126");
        if (!exactWorldState || !string.Equals(currentAction, request.CalicoActionRaw, StringComparison.Ordinal) ||
            !AreAdjacent(stand, interaction) || location is null || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_calico_jack",
                "calico_jack=one_native_round", CalicoJackObservedEffect(null),
                "calico_jack_endpoint_or_transparent_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "play_calico_jack",
                "route=calico_jack_table_stand", CalicoJackObservedEffect(null),
                "calico_jack_path_unavailable:" + pathReason));
            return;
        }

        activeCalicoJack = new ActiveCalicoJack(
            pending, location, interaction, stand, path, maxMovementTiles,
            expectedPlayerCards, expectedDealerCards);
    }

    private static bool CalicoJackRequestIsTyped(
        TrainingExecutionRequest request,
        out int[] expectedPlayerCards,
        out int[] expectedDealerCards)
    {
        expectedPlayerCards = ParseCalicoCards(request.CalicoPlayerCardsJson);
        expectedDealerCards = ParseCalicoCards(request.CalicoDealerCardsJson);
        var bet = request.CalicoBet;
        var coins = request.CalicoClubCoinsBefore;
        var expectedDelta = request.CalicoExpectedCoinDelta;
        var lowBetDelta = request.CalicoCoinDeltaPerLowBet;
        return request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
            request.StandTileX.HasValue && request.StandTileY.HasValue &&
            request.LocationId == "Club" &&
            request.CalicoActionToken is "ClubCards" or "BlackJack" &&
            request.CalicoActionRaw.StartsWith(request.CalicoActionToken, StringComparison.Ordinal) &&
            bet is 100 or 1000 && coins.HasValue && coins.Value >= bet.Value &&
            request.CalicoTableKind == (bet == 1000 ? "high_stakes" : "low_stakes") &&
            request.CalicoDialogueKey == (bet == 1000 ? "CalicoJackHS" : "CalicoJack") &&
            request.CalicoPlayResponseKey == "Play" &&
            !string.IsNullOrWhiteSpace(request.CalicoProjectionFingerprint) &&
            request.CalicoTargetClubCoins == 10000 &&
            request.CalicoRemainingClubCoinDemand is > 0 &&
            request.CalicoTargetItemId == "(BC)126" &&
            request.CalicoTimesPlayedSeed is > 0 && request.CalicoDaysPlayedSeed is >= 0 &&
            double.TryParse(request.CalicoUniqueGameIdSeed, NumberStyles.Float, CultureInfo.InvariantCulture, out var uniqueId) &&
            double.IsFinite(uniqueId) && request.CalicoDailyLuck.HasValue && request.CalicoLuckLevel.HasValue &&
            expectedPlayerCards.Length == 2 && expectedDealerCards.Length == 2 &&
            request.CalicoRecommendedFirstAction is "hit" or "stand" &&
            request.CalicoProjectedNextHitCard is >= 0 &&
            lowBetDelta.HasValue && expectedDelta.HasValue &&
            expectedDelta.Value == lowBetDelta.Value * bet.Value / 100 &&
            request.CalicoProjectedOutcome is "player_calico_jack" or "player_bust" or "dealer_bust" or
                "draw" or "player_higher" or "dealer_higher" &&
            request.CalicoDecisionPolicy == "exact_seed_replay_hidden_card_and_future_draw_max_coin_delta" &&
            request.CalicoExitPolicy == "quit_after_one_native_settlement" &&
            request.NativeContract == RuntimeCalicoJackNativeContract;
    }

    private void TickCalicoJack()
    {
        var active = activeCalicoJack;
        if (active is null)
            return;
        if (active.Stage == CalicoJackStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "calico_jack", out var movementFailure);
            if (movement == NativeObjectMovementStatus.Failed)
                BlockCalicoJack(active, movementFailure);
            else if (movement == NativeObjectMovementStatus.Ready)
                OpenCalicoJackDialogue(active);
            return;
        }

        active.ElapsedTicks++;
        active.StageTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            BlockCalicoJack(active, "calico_jack_timeout");
            return;
        }
        if (!ReferenceEquals(Game1.currentLocation, active.Location))
        {
            BlockCalicoJack(active, "calico_jack_location_changed");
            return;
        }

        switch (active.Stage)
        {
            case CalicoJackStage.WaitDialogue:
                TickCalicoJackDialogue(active);
                break;
            case CalicoJackStage.WaitMinigame:
                TickCalicoJackMinigameStart(active);
                break;
            case CalicoJackStage.WaitInitialDeal:
                TickCalicoJackInitialDeal(active);
                break;
            case CalicoJackStage.PlayerTurn:
                TickCalicoJackPlayerTurn(active);
                break;
            case CalicoJackStage.DealerTurn:
                TickCalicoJackDealerTurn(active);
                break;
            case CalicoJackStage.WaitResult:
                TickCalicoJackResult(active);
                break;
            case CalicoJackStage.WaitQuit:
                TickCalicoJackQuit(active);
                break;
        }
    }

    private void OpenCalicoJackDialogue(ActiveCalicoJack active)
    {
        Game1.player.faceDirection(DirectionTo(active.Stand, active.Interaction));
        if (!active.Location.checkAction(
                new xTile.Dimensions.Location(active.Interaction.X, active.Interaction.Y),
                Game1.viewport,
                Game1.player))
        {
            BlockCalicoJack(active, "calico_jack_native_check_action_rejected");
            return;
        }
        active.Stage = CalicoJackStage.WaitDialogue;
        active.StageTicks = 0;
    }

    private void TickCalicoJackDialogue(ActiveCalicoJack active)
    {
        if (Game1.activeClickableMenu is not DialogueBox menu)
        {
            if (active.StageTicks > 180)
                BlockCalicoJack(active, "calico_jack_dialogue_open_timeout");
            return;
        }
        var request = active.Pending.Request;
        if (active.Location.lastQuestionKey != request.CalicoDialogueKey)
        {
            BlockCalicoJack(active, "calico_jack_dialogue_key_mismatch");
            return;
        }
        var responseIndex = Array.FindIndex(menu.responses,
            response => response.responseKey == request.CalicoPlayResponseKey);
        if (responseIndex < 0)
        {
            BlockCalicoJack(active, "calico_jack_native_play_response_missing");
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0 || menu.responseCC is null ||
            responseIndex >= menu.responseCC.Count)
        {
            if (active.StageTicks > 180)
                BlockCalicoJack(active, "calico_jack_dialogue_not_clickable_timeout");
            return;
        }
        var bounds = menu.responseCC[responseIndex].bounds;
        menu.performHoverAction(bounds.Center.X, bounds.Center.Y);
        if (menu.selectedResponse != responseIndex)
        {
            BlockCalicoJack(active, "calico_jack_native_play_hover_rejected");
            return;
        }
        menu.receiveLeftClick(bounds.Center.X, bounds.Center.Y);
        active.Stage = CalicoJackStage.WaitMinigame;
        active.StageTicks = 0;
    }

    private void TickCalicoJackMinigameStart(ActiveCalicoJack active)
    {
        if (Game1.currentMinigame is CalicoJack game)
        {
            var request = active.Pending.Request;
            if (Club.timesPlayedCalicoJack != request.CalicoTimesPlayedSeed ||
                ReadCalicoInt(game, RuntimeCalicoCurrentBetField) != request.CalicoBet ||
                ReadCalicoBool(game, RuntimeCalicoHighStakesField) != (request.CalicoBet == 1000) ||
                Game1.player.clubCoins != request.CalicoClubCoinsBefore)
            {
                BlockCalicoJack(active, "calico_jack_native_instance_seed_or_bet_mismatch");
                return;
            }
            active.Game = game;
            active.Stage = CalicoJackStage.WaitInitialDeal;
            active.StageTicks = 0;
            return;
        }
        if (Game1.currentMinigame is not null || active.StageTicks > 300)
            BlockCalicoJack(active, "calico_jack_native_start_timeout_or_wrong_minigame");
    }

    private void TickCalicoJackInitialDeal(ActiveCalicoJack active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            BlockCalicoJack(active, "calico_jack_minigame_disappeared_during_deal");
            return;
        }
        if (game.playerCards.Count < 2 || game.dealerCards.Count < 2 ||
            ReadCalicoInt(game, RuntimeCalicoStartTimerField) > 0 || !game.playButtonsActive())
            return;
        if (game.playerCards.Count != 2 || game.dealerCards.Count != 2)
        {
            BlockCalicoJack(active, "calico_jack_initial_card_count_mismatch");
            return;
        }

        var request = active.Pending.Request;
        var uniqueId = double.Parse(request.CalicoUniqueGameIdSeed, CultureInfo.InvariantCulture);
        var cursor = new CalicoJackRandomCursor(() => Utility.CreateRandom(
            request.CalicoTimesPlayedSeed!.Value,
            request.CalicoDaysPlayedSeed!.Value,
            uniqueId));
        var projectedDealer = new[] { cursor.Next(1, 12), cursor.Next(1, 10) };
        var projectedPlayer = new[] { cursor.Next(1, 12), cursor.Next(1, 10) };
        var actualDealer = game.dealerCards.Select(card => card[0]).ToArray();
        var actualPlayer = game.playerCards.Select(card => card[0]).ToArray();
        if (!projectedDealer.SequenceEqual(active.ExpectedInitialDealerCards) ||
            !projectedPlayer.SequenceEqual(active.ExpectedInitialPlayerCards) ||
            !actualDealer.SequenceEqual(projectedDealer) || !actualPlayer.SequenceEqual(projectedPlayer))
        {
            BlockCalicoJack(active, "calico_jack_initial_seed_replay_mismatch");
            return;
        }

        active.RandomCursor = cursor;
        active.LastDealerCardCount = 2;
        active.LastPlayerCardCount = 2;
        var decision = CalicoJackDecisionModel.Recommend(cursor, actualPlayer, actualDealer,
            request.CalicoBet!.Value, request.CalicoDailyLuck!.Value, request.CalicoLuckLevel!.Value);
        var expectedDelta = decision.RecommendedAction == "hit" ? decision.HitCoinDelta : decision.StandCoinDelta;
        var expectedOutcome = decision.RecommendedAction == "hit" ? decision.HitOutcome : decision.StandOutcome;
        if (decision.RecommendedAction != request.CalicoRecommendedFirstAction ||
            decision.ProjectedNextHitCard != request.CalicoProjectedNextHitCard ||
            expectedDelta != request.CalicoExpectedCoinDelta || expectedOutcome != request.CalicoProjectedOutcome)
        {
            BlockCalicoJack(active, "calico_jack_first_decision_projection_mismatch");
            return;
        }
        active.FirstDecisionVerified = true;
        active.Stage = CalicoJackStage.PlayerTurn;
        active.StageTicks = 0;
    }

    private void TickCalicoJackPlayerTurn(ActiveCalicoJack active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            BlockCalicoJack(active, "calico_jack_minigame_disappeared_during_player_turn");
            return;
        }
        if (!VerifyPendingCalicoPlayerCard(active))
            return;
        if (ReadCalicoBool(game, RuntimeCalicoShowingResultsField))
        {
            SettleCalicoJack(active);
            return;
        }
        if (ReadCalicoInt(game, RuntimeCalicoBustTimerField) > 0)
        {
            active.Stage = CalicoJackStage.WaitResult;
            active.StageTicks = 0;
            return;
        }
        if (!game.playButtonsActive())
            return;
        if (game.playerCards.Count != active.LastPlayerCardCount || game.dealerCards.Count != active.LastDealerCardCount)
        {
            BlockCalicoJack(active, "calico_jack_unexpected_card_mutation_during_player_turn");
            return;
        }

        var request = active.Pending.Request;
        var playerCards = game.playerCards.Select(card => card[0]).ToArray();
        var dealerCards = game.dealerCards.Select(card => card[0]).ToArray();
        var currentBet = ReadCalicoInt(game, RuntimeCalicoCurrentBetField);
        var decision = CalicoJackDecisionModel.Recommend(active.RandomCursor!, playerCards, dealerCards,
            currentBet, request.CalicoDailyLuck!.Value, request.CalicoLuckLevel!.Value);
        active.DecisionCount++;
        if (decision.RecommendedAction == "hit")
        {
            active.PendingExpectedPlayerCard = CalicoJackDecisionModel.DrawPlayerCard(
                active.RandomCursor!, playerCards.Sum());
            if (!ClickCalicoComponent(game, RuntimeCalicoHitField))
            {
                BlockCalicoJack(active, "calico_jack_native_hit_component_unavailable");
                return;
            }
            active.NativeHitClicks++;
            return;
        }
        if (!ClickCalicoComponent(game, RuntimeCalicoStandField))
        {
            BlockCalicoJack(active, "calico_jack_native_stand_component_unavailable");
            return;
        }
        active.NativeStandClicks++;
        active.Stage = CalicoJackStage.DealerTurn;
        active.StageTicks = 0;
    }

    private bool VerifyPendingCalicoPlayerCard(ActiveCalicoJack active)
    {
        if (!active.PendingExpectedPlayerCard.HasValue)
            return true;
        var game = active.Game!;
        if (game.playerCards.Count == active.LastPlayerCardCount)
            return false;
        if (game.playerCards.Count != active.LastPlayerCardCount + 1 ||
            game.playerCards[^1][0] != active.PendingExpectedPlayerCard.Value)
        {
            BlockCalicoJack(active, "calico_jack_native_hit_rng_replay_mismatch");
            return false;
        }
        active.LastPlayerCardCount++;
        active.PendingExpectedPlayerCard = null;
        return true;
    }

    private void TickCalicoJackDealerTurn(ActiveCalicoJack active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            BlockCalicoJack(active, "calico_jack_minigame_disappeared_during_dealer_turn");
            return;
        }
        if (game.playerCards.Count != active.LastPlayerCardCount)
        {
            BlockCalicoJack(active, "calico_jack_player_cards_changed_during_dealer_turn");
            return;
        }

        var playerTotal = game.playerCards.Sum(card => card[0]);
        while (active.LastDealerCardCount < game.dealerCards.Count)
        {
            var dealerTotalBefore = game.dealerCards.Take(active.LastDealerCardCount).Sum(card => card[0]);
            var draw = CalicoJackDecisionModel.DrawDealerCard(active.RandomCursor!, dealerTotalBefore, playerTotal,
                active.ExpectedCurrentBet, active.Pending.Request.CalicoDailyLuck!.Value,
                active.Pending.Request.CalicoLuckLevel!.Value);
            var actualCard = game.dealerCards[active.LastDealerCardCount][0];
            if (actualCard != draw.Card)
            {
                BlockCalicoJack(active, "calico_jack_native_dealer_rng_replay_mismatch");
                return;
            }
            active.ExpectedCurrentBet = draw.CurrentBet;
            active.LastDealerCardCount++;
            active.NativeDealerDrawsVerified++;
        }
        if (ReadCalicoInt(game, RuntimeCalicoCurrentBetField) != active.ExpectedCurrentBet)
        {
            BlockCalicoJack(active, "calico_jack_native_qi_fruit_bet_mismatch");
            return;
        }
        if (ReadCalicoBool(game, RuntimeCalicoShowingResultsField))
        {
            SettleCalicoJack(active);
            return;
        }
        if (ReadCalicoInt(game, RuntimeCalicoBustTimerField) > 0 ||
            ReadCalicoInt(game, RuntimeCalicoDealerTurnTimerField) > 0)
            return;
        active.Stage = CalicoJackStage.WaitResult;
        active.StageTicks = 0;
    }

    private void TickCalicoJackResult(ActiveCalicoJack active)
    {
        var game = active.Game!;
        if (!ReferenceEquals(Game1.currentMinigame, game))
        {
            BlockCalicoJack(active, "calico_jack_minigame_disappeared_before_result");
            return;
        }
        if (!VerifyPendingCalicoPlayerCard(active))
            return;
        if (ReadCalicoBool(game, RuntimeCalicoShowingResultsField))
            SettleCalicoJack(active);
    }

    private void SettleCalicoJack(ActiveCalicoJack active)
    {
        var game = active.Game!;
        var request = active.Pending.Request;
        var playerTotal = game.playerCards.Sum(card => card[0]);
        var dealerTotal = game.dealerCards.Sum(card => card[0]);
        var outcome = CalicoJackOutcome(playerTotal, dealerTotal);
        var delta = Game1.player.clubCoins - request.CalicoClubCoinsBefore!.Value;
        var playerWon = ReadCalicoBool(game, RuntimeCalicoPlayerWonField);
        var shouldWin = outcome is "player_calico_jack" or "dealer_bust" or "player_higher";
        if (outcome != request.CalicoProjectedOutcome || delta != request.CalicoExpectedCoinDelta ||
            playerWon != shouldWin || ReadCalicoInt(game, RuntimeCalicoCurrentBetField) != active.ExpectedCurrentBet)
        {
            BlockCalicoJack(active, "calico_jack_native_settlement_mismatch");
            return;
        }
        active.ObservedOutcome = outcome;
        active.SettlementVerified = true;
        if (!ClickCalicoComponent(game, RuntimeCalicoQuitField))
        {
            BlockCalicoJack(active, "calico_jack_native_quit_component_unavailable");
            return;
        }
        active.Stage = CalicoJackStage.WaitQuit;
        active.StageTicks = 0;
    }

    private void TickCalicoJackQuit(ActiveCalicoJack active)
    {
        if (Game1.currentMinigame is not null)
        {
            if (active.StageTicks > 180)
                BlockCalicoJack(active, "calico_jack_native_quit_timeout");
            return;
        }
        var request = active.Pending.Request;
        var verified = active.FirstDecisionVerified && active.SettlementVerified && active.DecisionCount > 0 &&
            active.NativeHitClicks + active.NativeStandClicks > 0 &&
            Game1.player.clubCoins - request.CalicoClubCoinsBefore!.Value == request.CalicoExpectedCoinDelta;
        activeCalicoJack = null;
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
            PrimitiveKind = "play_calico_jack",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Club_checkAction_and_DialogueBox_Play_click_verified",
                    "exact_1_6_15_seed_stream_and_hidden_dealer_card_replay_verified",
                    "native_hit_or_stand_input_decisions_verified",
                    "native_club_coin_settlement_and_one_round_quit_verified"
                }
                : new[] { "calico_jack_post_state_mismatch" },
            RequestedEffect = "calico_jack_round=1;club_coins_delta=" + request.CalicoExpectedCoinDelta,
            ObservedEffect = CalicoJackObservedEffect(active),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "calico_jack_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.club_coins",
                        Before = request.CalicoClubCoinsBefore!.Value.ToString(CultureInfo.InvariantCulture),
                        After = Game1.player.clubCoins.ToString(CultureInfo.InvariantCulture)
                    },
                    new SimulatedFactChange
                    {
                        Path = "world.club.times_played_calico_jack",
                        Before = (request.CalicoTimesPlayedSeed!.Value - 1).ToString(CultureInfo.InvariantCulture),
                        After = Club.timesPlayedCalicoJack.ToString(CultureInfo.InvariantCulture)
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void BlockCalicoJack(ActiveCalicoJack active, string reason)
    {
        activeCalicoJack = null;
        StopAllMovement();
        if (active.Game is { } game && ReferenceEquals(Game1.currentMinigame, game))
        {
            if (ReadCalicoBool(game, RuntimeCalicoShowingResultsField))
                ClickCalicoComponent(game, RuntimeCalicoQuitField);
            if (ReferenceEquals(Game1.currentMinigame, game))
                Game1.currentMinigame = null;
        }
        else if (Game1.activeClickableMenu is DialogueBox)
        {
            Game1.exitActiveMenu();
        }
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request,
            "play_calico_jack", "calico_jack_round=1",
            CalicoJackObservedEffect(active), reason));
    }

    private static bool ClickCalicoComponent(CalicoJack game, FieldInfo? field)
    {
        if (field?.GetValue(game) is not ClickableComponent component)
            return false;
        game.receiveLeftClick(component.bounds.Center.X, component.bounds.Center.Y, playSound: false);
        return true;
    }

    private static int[] ParseCalicoCards(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            return Array.Empty<int>();
        }
    }

    private static int ReadCalicoInt(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is int value ? value : int.MinValue;

    private static bool ReadCalicoBool(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is bool value && value;

    private static string CalicoJackOutcome(int playerTotal, int dealerTotal)
    {
        if (playerTotal == CalicoJackDecisionModel.PlayingTo)
            return "player_calico_jack";
        if (playerTotal > CalicoJackDecisionModel.PlayingTo)
            return "player_bust";
        if (dealerTotal > CalicoJackDecisionModel.PlayingTo)
            return "dealer_bust";
        if (playerTotal == dealerTotal)
            return "draw";
        return playerTotal > dealerTotal ? "player_higher" : "dealer_higher";
    }

    private static string CalicoJackObservedEffect(ActiveCalicoJack? active)
    {
        var game = active?.Game ?? Game1.currentMinigame as CalicoJack;
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";minigame=" + (Game1.currentMinigame?.minigameId() ?? "none") +
            ";club_coins=" + Game1.player.clubCoins +
            ";times_played=" + Club.timesPlayedCalicoJack +
            ";bet=" + (game is null ? "unavailable" : ReadCalicoInt(game, RuntimeCalicoCurrentBetField)) +
            ";player_cards=" + (game?.playerCards.Count.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";dealer_cards=" + (game?.dealerCards.Count.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";native_hits=" + (active?.NativeHitClicks.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";native_stands=" + (active?.NativeStandClicks.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";dealer_draws_verified=" + (active?.NativeDealerDrawsVerified.ToString(CultureInfo.InvariantCulture) ?? "unavailable") +
            ";outcome=" + (active?.ObservedOutcome ?? "unavailable");
    }
}
