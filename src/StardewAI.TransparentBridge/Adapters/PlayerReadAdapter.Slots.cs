using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string SlotsNativeContract =
        "ClubSlots_checkAction_then_native_Slots_10_or_100_spin_then_native_random_settlement_then_done";

    private static readonly FieldInfo? SlotsValuesField = PrivateField<Slots>("slots");
    private static readonly FieldInfo? SlotsResultsField = PrivateField<Slots>("slotResults");
    private static readonly FieldInfo? SlotsSpin10Field = PrivateField<Slots>("spinButton10");
    private static readonly FieldInfo? SlotsSpin100Field = PrivateField<Slots>("spinButton100");
    private static readonly FieldInfo? SlotsDoneField = PrivateField<Slots>("doneButton");

    private static object ReadSlotsContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var demand = ReadCasinoCurrencyDemand(player);
        var activeGame = Game1.currentMinigame as Slots;
        var interactionTiles = ReadSlotsInteractionTiles(player.currentLocation);
        var luckMultiplier = 1d + player.DailyLuck * 2d + player.LuckLevel * 0.08d;
        var payoutRows = BuildSlotsPayoutRows(luckMultiplier);
        var expectedPayoutMultiplier = payoutRows.Sum(row => row.Probability * row.PayoutMultiplier);
        var recommendedBet = demand.RemainingClubCoinDemand <= 0
            ? 0
            : demand.RemainingClubCoinDemand < 100 && player.clubCoins >= 10
                ? 10
                : player.clubCoins >= 100
                    ? 100
                    : player.clubCoins >= 10
                        ? 10
                        : 0;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var inClub = string.Equals(player.currentLocation.NameOrUniqueName, "Club", StringComparison.OrdinalIgnoreCase);
        var gateStatus = activeGame is not null
            ? "active_native_slots"
            : !demand.TargetRequired
                ? "complete_no_slots_currency_demand"
                : demand.RemainingClubCoinDemand == 0
                    ? "ready_to_purchase_casino_rarecrow"
                    : !player.hasClubCard
                        ? "blocked_club_card_required"
                        : recommendedBet == 0
                            ? "blocked_slots_seed_coins_required"
                            : !inClub
                                ? "route_to_club_required"
                                : !menuClear || Game1.eventUp || player.UsingTool || !player.CanMove
                                    ? "blocked_player_busy"
                                    : interactionTiles.Length == 0
                                        ? "blocked_slots_interaction_tile_unavailable"
                                        : "ready";
        var activeState = activeGame is null ? null : ReadActiveSlots(activeGame, player);
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "slots.v1",
            player.clubCoins,
            player.hasClubCard,
            demand,
            times_played = Club.timesPlayedSlots,
            daily_luck = player.DailyLuck,
            luck_level = player.LuckLevel,
            luckMultiplier,
            recommendedBet,
            payoutRows,
            interactionTiles,
            activeState
        }));

        return new
        {
            schema_version = "slots.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            location_id = "Club",
            is_current_location = inClub,
            has_club_card = player.hasClubCard,
            club_coins = player.clubCoins,
            target_qualified_item_id = CasinoCurrencyTargetItemId,
            target_item_exists_anywhere = demand.TargetExistsAnywhere,
            deluxe_scarecrow_recipe_unlocked = demand.DeluxeScarecrowRecipeUnlocked,
            rarecrow_society_received_or_pending = demand.RarecrowSocietyReceivedOrPending,
            automatic_currency_demand = demand.TargetRequired,
            target_club_coins = CasinoCurrencyTargetCoins,
            remaining_club_coin_demand = demand.RemainingClubCoinDemand,
            recommended_bet = recommendedBet,
            low_bet = 10,
            high_bet = 100,
            times_played = Club.timesPlayedSlots,
            daily_luck = player.DailyLuck,
            luck_level = player.LuckLevel,
            luck_multiplier = luckMultiplier,
            expected_payout_multiplier = expectedPayoutMultiplier,
            expected_net_coin_delta = recommendedBet * (expectedPayoutMultiplier - 1d),
            rng_contract = "shared_Game1.random_live_feedback_not_stable_future_prediction",
            payout_rows = payoutRows,
            interaction_tiles = interactionTiles,
            casino_shop_currency = 2,
            casino_shop_rows = ReadCasinoShopRows(),
            active_spin = activeState,
            demand_policy = "automatic_only_for_missing_(BC)126_rarecrow_dependency;one_native_spin_per_candidate;100_coin_bet_unless_shortfall_or_balance_requires_10;fresh_snapshot_after_settlement",
            exit_policy = "done_after_one_native_settlement",
            native_contract = SlotsNativeContract
        };
    }

    private static object[] ReadSlotsInteractionTiles(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
            return Array.Empty<object>();
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            var token = action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.Equals(token, "ClubSlots", StringComparison.Ordinal))
                continue;
            result.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = token });
        }
        return result.ToArray();
    }

    private static SlotPayoutRow[] BuildSlotsPayoutRows(double luckMultiplier)
    {
        var rows = new[]
        {
            Payout("triple_stardrop", 0d, 0.001d, 2500d, "[5,5,5]"),
            Payout("triple_diamond", 0.001d, 0.0016d, 1000d, "[6,6,6]"),
            Payout("triple_seven", 0.0016d, 0.0025d, 500d, "[7,7,7]"),
            Payout("triple_melon", 0.0025d, 0.005d, 200d, "[4,4,4]"),
            Payout("triple_orange", 0.005d, 0.007d, 120d, "[3,3,3]"),
            Payout("triple_parsnip", 0.007d, 0.01d, 80d, "[2,2,2]"),
            Payout("triple_cherry", 0.01d, 0.02d, 30d, "[1,1,1]"),
            Payout("two_sevens", 0.02d, 0.12d, 3d, "exactly_two_7"),
            Payout("triple_coin", 0.12d, 0.2d, 5d, "[0,0,0]"),
            Payout("one_seven", 0.2d, 0.4d, 2d, "exactly_one_7"),
            Payout("no_payout", 0.4d, 1d, 0d, "no_7_and_no_triple")
        };
        return rows.Select(row => row with
        {
            Probability = SlotProbability(row.LowerThreshold, row.UpperThreshold, luckMultiplier)
        }).ToArray();
    }

    private static SlotPayoutRow Payout(string id, double lower, double upper, double multiplier, string pattern) =>
        new(id, lower, upper, 0d, multiplier, pattern);

    private static double SlotProbability(double lower, double upper, double luckMultiplier)
    {
        var low = Math.Clamp(lower * luckMultiplier, 0d, 1d);
        var high = upper >= 1d ? 1d : Math.Clamp(upper * luckMultiplier, 0d, 1d);
        return Math.Max(0d, high - low);
    }

    private static object ReadActiveSlots(Slots game, Farmer player) => new
    {
        minigame_id = game.minigameId(),
        game.spinning,
        game.showResult,
        game.payoutModifier,
        game.currentBet,
        game.spinsCount,
        game.slotsFinished,
        game.endTimer,
        reel_positions = ReadSlotsFloats(game, SlotsValuesField),
        result_icons = ReadSlotsFloats(game, SlotsResultsField),
        spin_10_button_available = SlotsSpin10Field?.GetValue(game) is ClickableComponent,
        spin_100_button_available = SlotsSpin100Field?.GetValue(game) is ClickableComponent,
        done_button_available = SlotsDoneField?.GetValue(game) is ClickableComponent,
        club_coins = player.clubCoins
    };

    private static float[] ReadSlotsFloats(Slots game, FieldInfo? field) =>
        field?.GetValue(game) is List<float> values ? values.ToArray() : Array.Empty<float>();

    private sealed record SlotPayoutRow(
        string OutcomeId,
        double LowerThreshold,
        double UpperThreshold,
        double Probability,
        double PayoutMultiplier,
        string ResultPattern);
}
