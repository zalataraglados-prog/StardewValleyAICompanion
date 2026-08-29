using System.Reflection;
using System.Text.Json;
using StardewValley;
using StardewValley.Internal;
using StardewValley.Minigames;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FairFishingNativeContract =
        "Event.checkAction(festival_fall16_buildings_503_504)->DialogueBox(fishingGame:Play).receiveLeftClick->Event.answerDialogue(fishingGame,0)->Money-50->globalFadeToBlack(FishingGame.startMe)->native_100000ms_FishingGame_input_session->perfection_score_reward->festivalScore";

    private const string FairTokenShopId = "Festival_StardewValleyFair_StarTokens";
    private const int FairStardropPrice = 2000;

    private static readonly FieldInfo? FairFishingTimerToStartField = typeof(FishingGame)
        .GetField("timerToStart", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? FairFishingGameEndTimerField = typeof(FishingGame)
        .GetField("gameEndTimer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? FairFishingShowResultsTimerField = typeof(FishingGame)
        .GetField("showResultsTimer", BindingFlags.Instance | BindingFlags.NonPublic);

    private static object ReadFairFishingGameContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var activeGame = Game1.currentMinigame as FishingGame;
        var festivalLocation = activeGame?.originalLocation ?? player.currentLocation;
        var festival = festivalLocation.currentEvent ?? player.currentLocation.currentEvent;
        var activeFair = festival is not null && festival.isFestival &&
            string.Equals(festival.id, "festival_fall16", StringComparison.Ordinal);
        if (!activeFair)
        {
            return new
            {
                schema_version = "fair_fishing_game.v1",
                projection_status = "contextual_inactive_festival_fall16",
                native_contract = FairFishingNativeContract,
                interaction_tiles = Array.Empty<object>(),
                shop_rows = Array.Empty<object>()
            };
        }

        var interactionTiles = ReadFairFishingInteractionTiles(festivalLocation);
        var shopRows = ReadFairStarTokenShopRows();
        var stardropAcquired = player.hasOrWillReceiveMail("CF_Fair");
        var projectedGrangeTokens = ProjectUnclaimedGrangeTokens(player, festival!);
        var remainingDemand = stardropAcquired
            ? 0
            : Math.Max(0, FairStardropPrice - player.festivalScore - projectedGrangeTokens);
        var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "none";
        var activeMinigame = Game1.currentMinigame?.minigameId() ?? "none";
        var gateStatus = activeGame is not null
            ? "active_native_fishing_game"
            : Game1.currentMinigame is not null
                ? "blocked_other_minigame_active"
                : Game1.activeClickableMenu is not null || Game1.dialogueUp
                    ? "blocked_active_menu_or_dialogue"
                    : !player.CanMove || player.UsingTool
                        ? "blocked_player_busy"
                        : player.Money < 50
                            ? "blocked_entry_fee_unavailable"
                            : remainingDemand <= 0
                                ? stardropAcquired
                                    ? "complete_fair_stardrop_acquired"
                                    : "complete_projected_tokens_cover_stardrop"
                                : interactionTiles.Length == 0
                                    ? "blocked_fishing_game_interaction_tile_unavailable"
                                    : "ready";

        var rod = player.CurrentTool as FishingRod;
        var activeState = activeGame is null
            ? null
            : new
            {
                minigame_id = activeGame.minigameId(),
                activeGame.exit,
                activeGame.gameDone,
                activeGame.score,
                activeGame.fishCaught,
                activeGame.perfections,
                activeGame.perfectionBonus,
                activeGame.starTokensWon,
                timer_to_start_ms = ReadFairFishingPrivateInt(activeGame, FairFishingTimerToStartField),
                game_end_timer_ms = ReadFairFishingPrivateInt(activeGame, FairFishingGameEndTimerField),
                show_results_timer_ms = ReadFairFishingPrivateInt(activeGame, FairFishingShowResultsTimerField),
                temporary_location_id = player.currentLocation.NameOrUniqueName,
                original_location_id = activeGame.originalLocation?.NameOrUniqueName ?? string.Empty,
                rod_qualified_item_id = rod?.QualifiedItemId ?? string.Empty,
                rod_attachment_slot_count = rod?.AttachmentSlotsCount ?? 0,
                bait_qualified_item_id = rod?.attachments.ElementAtOrDefault(0)?.QualifiedItemId ?? string.Empty,
                bait_stack = rod?.attachments.ElementAtOrDefault(0)?.Stack ?? 0,
                tackle_qualified_item_id = rod?.attachments.ElementAtOrDefault(1)?.QualifiedItemId ?? string.Empty,
                rod_state = rod is null ? null : new
                {
                    rod.isTimingCast,
                    rod.isCasting,
                    rod.castedButBobberStillInAir,
                    rod.isFishing,
                    rod.isNibbling,
                    rod.isReeling,
                    rod.pullingOutOfWater,
                    rod.fishCaught,
                    rod.castingPower
                }
            };
        var returnTile = Game1.year % 2 == 0
            ? new { tile_x = 36, tile_y = 68 }
            : new { tile_x = 24, tile_y = 71 };
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "fair_fishing_game.v1",
            festival_id = festival!.id,
            location = festivalLocation.NameOrUniqueName,
            player.Money,
            player.festivalScore,
            stardropAcquired,
            projectedGrangeTokens,
            remainingDemand,
            interactionTiles,
            activeMenu,
            activeMinigame,
            activeState
        }));

        return new
        {
            schema_version = "fair_fishing_game.v1",
            projection_status = activeGame is null
                ? "complete_current_festival_fall16_fishing_game_context"
                : "complete_active_native_fishing_game_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            festival_id = festival.id,
            festival_location_id = festivalLocation.NameOrUniqueName,
            player_money = player.Money,
            entry_fee_money = 50,
            festival_score = player.festivalScore,
            stardrop_acquired = stardropAcquired,
            stardrop_price_star_tokens = FairStardropPrice,
            projected_unclaimed_grange_tokens = projectedGrangeTokens,
            remaining_star_token_demand = remainingDemand,
            demand_policy = "automatic_until_current_tokens_plus_unclaimed_grange_prize_cover_unacquired_fair_stardrop;other_shop_rows_are_transparent_strategy_inputs_not_automatic_repeat_demand",
            game_duration_ms = 100000,
            prestart_duration_ms = 1000,
            post_game_delay_ms = 1000,
            results_duration_ms = 11100,
            scoring_contract = new
            {
                junk_or_zero_size_points = 1,
                fish_points = "size+5",
                perfect_points = 10,
                triple_perfect_multiplier = 2,
                triple_perfect_gate = "fishCaught>=3 && perfections>=3",
                reward_minimum_score = 10,
                star_token_formula = "score>=10 ? ((score+5)/10*6)*2 : 0"
            },
            native_loadout = new
            {
                rod = "(T)BambooPole",
                attachment_slots = 2,
                bait = "(O)690",
                bait_stack = 99,
                tackle = "(O)687"
            },
            execution_strategy = "native_predictive_legal_input",
            dialogue_key = "fishingGame",
            play_response_key = "Play",
            repeatable_while_money_available = true,
            available_after_grange_judging = true,
            return_tile = returnTile,
            interaction_tiles = interactionTiles,
            shop_id = FairTokenShopId,
            shop_currency = "star_tokens",
            shop_rows = shopRows,
            active_minigame = activeState,
            native_contract = FairFishingNativeContract
        };
    }

    private static object[] ReadFairFishingInteractionTiles(GameLocation location)
    {
        if (location.Map?.Layers.Count is not > 0)
            return Array.Empty<object>();
        var layer = location.Map.Layers[0];
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
            if (tileIndex is 503 or 504)
                result.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex });
        }
        return result.ToArray();
    }

    private static object[] ReadFairStarTokenShopRows()
    {
        if (!DataLoader.Shops(Game1.content).TryGetValue(FairTokenShopId, out var shop))
            return Array.Empty<object>();
        return ShopBuilder.GetShopStock(FairTokenShopId, shop)
            .OrderBy(entry => entry.Value.Price)
            .ThenBy(entry => entry.Key.QualifiedItemId, StringComparer.Ordinal)
            .Select(entry => new
            {
                qualified_item_id = entry.Key.QualifiedItemId,
                item_id = entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId,
                display_name = entry.Key.DisplayName,
                price_star_tokens = entry.Value.Price,
                stock = entry.Value.Stock,
                infinite_stock = entry.Value.Stock == StardewValley.Menus.ShopMenu.infiniteStock,
                limited_stock_mode = entry.Value.LimitedStockMode.ToString(),
                synced_key = entry.Value.SyncedKey,
                can_buy_item = entry.Key.CanBuyItem(Game1.player),
                can_afford_now = Game1.player.festivalScore >= entry.Value.Price
            })
            .Cast<object>()
            .ToArray();
    }

    private static int ProjectUnclaimedGrangeTokens(Farmer player, StardewValley.Event festival)
    {
        if (festival.grangeJudged)
            return 0;
        var display = Enumerable.Range(0, 9)
            .Select(slot => slot < player.team.grangeDisplay.Count ? player.team.grangeDisplay[slot] : null)
            .ToArray();
        var bestScore = ScoreSelectedGrange(SelectBestGrangeChoices(BuildGrangeChoices(player, display)));
        return bestScore >= 90 ? 1000 : bestScore >= 75 ? 500 : bestScore >= 60 ? 250 : bestScore == -666 ? 750 : 50;
    }

    private static int? ReadFairFishingPrivateInt(FishingGame game, FieldInfo? field) =>
        field?.GetValue(game) as int?;
}
