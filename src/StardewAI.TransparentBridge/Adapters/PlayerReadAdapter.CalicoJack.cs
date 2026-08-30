using System.Reflection;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Minigames;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string CalicoJackNativeContract =
        "ClubCards_or_BlackJack_checkAction_then_CalicoJack_Play_then_native_CalicoJack_hit_or_stand_then_native_settlement_then_quit";
    private static readonly FieldInfo? CalicoCurrentBetField = PrivateField<CalicoJack>("currentBet");
    private static readonly FieldInfo? CalicoStartTimerField = PrivateField<CalicoJack>("startTimer");
    private static readonly FieldInfo? CalicoDealerTurnTimerField = PrivateField<CalicoJack>("dealerTurnTimer");
    private static readonly FieldInfo? CalicoBustTimerField = PrivateField<CalicoJack>("bustTimer");
    private static readonly FieldInfo? CalicoShowingResultsField = PrivateField<CalicoJack>("showingResultsScreen");
    private static readonly FieldInfo? CalicoPlayerWonField = PrivateField<CalicoJack>("playerWon");
    private static readonly FieldInfo? CalicoHighStakesField = PrivateField<CalicoJack>("highStakes");

    private static object ReadCalicoJackContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var hasClubCard = player.hasClubCard;
        var demand = ReadCasinoCurrencyDemand(player);
        var recipeUnlocked = demand.DeluxeScarecrowRecipeUnlocked;
        var societyReceivedOrPending = demand.RarecrowSocietyReceivedOrPending;
        var targetExistsAnywhere = demand.TargetExistsAnywhere;
        var targetRequired = demand.TargetRequired;
        var coinShortfall = demand.RemainingClubCoinDemand;
        var activeGame = Game1.currentMinigame as CalicoJack;
        var interactionTiles = ReadCalicoJackInteractionTiles(player.currentLocation);
        var nextProjection = activeGame is null
            ? ProjectNextCalicoJackRound(player)
            : null;
        var recommendedBet = nextProjection is null || coinShortfall <= 0
            ? 0
            : nextProjection.CoinDeltaPerLowBet > 0 && player.clubCoins >= 1000
                ? 1000
                : player.clubCoins >= 100
                    ? 100
                    : 0;
        var preserveLastSeedCoins = nextProjection is not null && nextProjection.CoinDeltaPerLowBet < 0 &&
            player.clubCoins < 200;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var inClub = string.Equals(player.currentLocation.NameOrUniqueName, "Club", StringComparison.OrdinalIgnoreCase);
        var gateStatus = activeGame is not null
            ? "active_native_calico_jack"
            : !targetRequired
                ? "complete_no_calico_jack_currency_demand"
                : coinShortfall == 0
                    ? "ready_to_purchase_casino_rarecrow"
                    : !hasClubCard
                        ? "blocked_club_card_required"
                        : preserveLastSeedCoins
                            ? "deferred_projected_loss_preserves_last_seed_coins"
                            : recommendedBet == 0
                                ? "blocked_calico_jack_seed_coins_required"
                                : !inClub
                                    ? "route_to_club_required"
                                    : !menuClear || Game1.eventUp || player.UsingTool || !player.CanMove
                                        ? "blocked_player_busy"
                                        : interactionTiles.Length == 0
                                            ? "blocked_calico_jack_interaction_tile_unavailable"
                                            : "ready";

        var activeState = activeGame is null ? null : ReadActiveCalicoJack(activeGame, player);
        var shopRows = ReadCasinoShopRows();
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "calico_jack.v1",
            player.clubCoins,
            hasClubCard,
            recipeUnlocked,
            societyReceivedOrPending,
            targetExistsAnywhere,
            targetRequired,
            coinShortfall,
            times_played = Club.timesPlayedCalicoJack,
            days_played = Game1.stats.DaysPlayed,
            Game1.uniqueIDForThisGame,
            daily_luck = player.DailyLuck,
            luck_level = player.LuckLevel,
            interactionTiles,
            nextProjection,
            activeState
        }));

        return new
        {
            schema_version = "calico_jack.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            location_id = "Club",
            is_current_location = inClub,
            has_club_card = hasClubCard,
            club_coins = player.clubCoins,
            target_qualified_item_id = CasinoCurrencyTargetItemId,
            target_item_exists_anywhere = targetExistsAnywhere,
            deluxe_scarecrow_recipe_unlocked = recipeUnlocked,
            rarecrow_society_received_or_pending = societyReceivedOrPending,
            automatic_currency_demand = targetRequired,
            target_club_coins = CasinoCurrencyTargetCoins,
            remaining_club_coin_demand = coinShortfall,
            recommended_bet = recommendedBet,
            recommended_table_kind = recommendedBet == 1000 ? "high_stakes" : recommendedBet == 100 ? "low_stakes" : "none",
            demand_policy = "automatic_only_for_missing_(BC)126_rarecrow_dependency;one_native_round_per_candidate;winning_seed_uses_high_stakes;loss_or_draw_advances_with_low_stakes;preserve_last_100_coins_on_projected_loss",
            low_stakes_bet = 100,
            high_stakes_bet = 1000,
            dialogue_low_key = "CalicoJack",
            dialogue_high_key = "CalicoJackHS",
            play_response_key = "Play",
            next_times_played_seed = Club.timesPlayedCalicoJack + 1,
            days_played_seed = Game1.stats.DaysPlayed,
            unique_game_id_seed = Game1.uniqueIDForThisGame.ToString(CultureInfo.InvariantCulture),
            daily_luck = player.DailyLuck,
            luck_level = player.LuckLevel,
            wiki_luck_claim_conflict = "wiki_says_luck_does_not_affect_results_but_1.6.15_CalicoJack.tick_uses_DailyLuck_and_LuckLevel_for_999_qi_fruit_draw",
            interaction_tiles = interactionTiles,
            casino_shop_currency = 2,
            casino_shop_rows = shopRows,
            next_round = nextProjection,
            active_round = activeState,
            native_contract = CalicoJackNativeContract
        };
    }

    private static object[] ReadCalicoJackInteractionTiles(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
            return Array.Empty<object>();
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (string.IsNullOrWhiteSpace(action))
                continue;
            var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts[0] is not ("ClubCards" or "BlackJack"))
                continue;
            var highStakes = parts.Length > 1 && parts[1] == "1000";
            result.Add(new
            {
                tile_x = x,
                tile_y = y,
                action_raw = action,
                action_token = parts[0],
                table_kind = highStakes ? "high_stakes" : "low_stakes",
                bet = highStakes ? 1000 : 100,
                dialogue_key = highStakes ? "CalicoJackHS" : "CalicoJack",
                play_response_key = "Play"
            });
        }
        return result.ToArray();
    }

    private static CalicoNextRoundProjection? ProjectNextCalicoJackRound(Farmer player)
    {
        var timesPlayed = Club.timesPlayedCalicoJack + 1;
        var daysPlayed = Game1.stats.DaysPlayed;
        var uniqueId = Game1.uniqueIDForThisGame;
        Func<Random> factory = () => Utility.CreateRandom(timesPlayed, daysPlayed, uniqueId);
        var cursor = new CalicoJackRandomCursor(factory);
        var dealerCards = new[] { cursor.Next(1, 12), cursor.Next(1, 10) };
        var playerCards = new[] { cursor.Next(1, 12), cursor.Next(1, 10) };
        var decision = CalicoJackDecisionModel.Recommend(
            cursor,
            playerCards,
            dealerCards,
            100,
            player.DailyLuck,
            player.LuckLevel);
        return new CalicoNextRoundProjection(
            timesPlayed,
            playerCards,
            dealerCards,
            decision.RecommendedAction,
            decision.StandCoinDelta,
            decision.HitCoinDelta,
            decision.ProjectedNextHitCard,
            decision.RecommendedAction == "hit" ? decision.HitCoinDelta : decision.StandCoinDelta,
            decision.RecommendedAction == "hit" ? decision.HitOutcome : decision.StandOutcome);
    }

    private static object ReadActiveCalicoJack(CalicoJack game, Farmer player)
    {
        var playerCards = game.playerCards.Select(card => card[0]).ToArray();
        var dealerCards = game.dealerCards.Select(card => card[0]).ToArray();
        return new
        {
            runtime_identity = RuntimeHelpers.GetHashCode(game).ToString("X8"),
            minigame_id = game.minigameId(),
            current_bet = ReadPrivateInt(game, CalicoCurrentBetField),
            high_stakes = ReadPrivateBool(game, CalicoHighStakesField),
            start_timer_ms = ReadPrivateInt(game, CalicoStartTimerField),
            dealer_turn_timer_ms = ReadPrivateInt(game, CalicoDealerTurnTimerField),
            bust_timer_ms = ReadPrivateInt(game, CalicoBustTimerField),
            showing_results_screen = ReadPrivateBool(game, CalicoShowingResultsField),
            player_won = ReadPrivateBool(game, CalicoPlayerWonField),
            play_buttons_active = game.playButtonsActive(),
            player_cards = game.playerCards.Select((card, index) => new { index, value = card[0], animation_state_ms = card[1] }).ToArray(),
            dealer_cards = game.dealerCards.Select((card, index) => new { index, value = card[0], animation_state_ms = card[1], face_down = card[1] == -1 }).ToArray(),
            player_total = playerCards.Sum(),
            dealer_total_including_hidden = dealerCards.Sum(),
            club_coins = player.clubCoins
        };
    }

    private sealed record CalicoNextRoundProjection(
        [property: JsonPropertyName("times_played_seed")] int TimesPlayedSeed,
        [property: JsonPropertyName("player_cards")] int[] PlayerCards,
        [property: JsonPropertyName("dealer_cards_including_hidden")] int[] DealerCardsIncludingHidden,
        [property: JsonPropertyName("recommended_first_action")] string RecommendedFirstAction,
        [property: JsonPropertyName("stand_coin_delta")] int StandCoinDelta,
        [property: JsonPropertyName("hit_coin_delta")] int HitCoinDelta,
        [property: JsonPropertyName("projected_next_hit_card")] int ProjectedNextHitCard,
        [property: JsonPropertyName("coin_delta_per_low_bet")] int CoinDeltaPerLowBet,
        [property: JsonPropertyName("projected_outcome")] string ProjectedOutcome);
}
