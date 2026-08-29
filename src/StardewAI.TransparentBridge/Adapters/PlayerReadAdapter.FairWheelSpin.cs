using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FairWheelNativeContract =
        "Event.checkAction(festival_fall16_buildings_308_309)->DialogueBox(wheelBet:Green).receiveLeftClick->Event.answerDialogue(wheelBet,1)->NumberSelectionMenu(wager_1_to_festivalScore).receiveLeftClick(ok)->Event.betStarTokens->WheelSpinGame(1000ms,green)->native_random_spin->festivalScore+(win?wager:-wager)->native_result_text_and_exit";
    private const string FairWheelWagerPolicy =
        "green_zero_luck_kelly_7_of_15_capped_by_remaining_stardrop_demand";

    private static readonly FieldInfo? FairWheelTimerField = WheelField("timerBeforeStart");
    private static readonly FieldInfo? FairWheelWagerField = WheelField("wager");
    private static readonly FieldInfo? FairWheelResultTextField = WheelField("resultText");
    private static readonly FieldInfo? FairWheelDoneField = WheelField("doneSpinning");
    private static readonly FieldInfo? FairWheelNumberMinimumField = NumberSelectionField("minValue");
    private static readonly FieldInfo? FairWheelNumberMaximumField = NumberSelectionField("maxValue");
    private static readonly FieldInfo? FairWheelNumberCurrentField = NumberSelectionField("currentValue");
    private static readonly FieldInfo? FairWheelNumberPriceField = NumberSelectionField("price");
    private static readonly FieldInfo? FairWheelNumberTextBoxField = NumberSelectionField("numberSelectedBox");

    private static object ReadFairWheelSpinContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var activeWheel = Game1.activeClickableMenu as WheelSpinGame;
        var activeNumber = Game1.activeClickableMenu as NumberSelectionMenu;
        var location = player.currentLocation;
        var festival = location.currentEvent;
        var activeFair = festival is not null && festival.isFestival &&
            string.Equals(festival.id, "festival_fall16", StringComparison.Ordinal);
        if (!activeFair)
        {
            return new
            {
                schema_version = "fair_wheel_spin.v1",
                projection_status = "contextual_inactive_festival_fall16",
                native_contract = FairWheelNativeContract,
                interaction_tiles = Array.Empty<object>(),
                shop_rows = Array.Empty<object>()
            };
        }

        var interactionTiles = ReadFairWheelInteractionTiles(location);
        var shopRows = ReadFairStarTokenShopRows();
        var (stardropAcquired, projectedGrangeTokens, remainingDemand) =
            ReadFairStardropDemand(player, festival!);
        var wager = remainingDemand >= 2
            ? Math.Min(remainingDemand, player.festivalScore * 7 / 15)
            : 0;
        var menuType = Game1.activeClickableMenu?.GetType().FullName ?? "none";
        var gateStatus = activeWheel is not null
            ? "active_native_wheel_spin"
            : activeNumber is not null
                ? "active_native_wager_selection"
                : Game1.currentMinigame is not null
                    ? "blocked_other_minigame_active"
                    : Game1.activeClickableMenu is not null || Game1.dialogueUp
                        ? "blocked_active_menu_or_dialogue"
                        : !player.CanMove || player.UsingTool
                            ? "blocked_player_busy"
                            : remainingDemand <= 0
                                ? stardropAcquired
                                    ? "complete_fair_stardrop_acquired"
                                    : "complete_projected_tokens_cover_stardrop"
                                : remainingDemand == 1
                                    ? "deferred_exact_one_token_uses_free_strength_game"
                                    : wager < 1
                                        ? "deferred_wheel_requires_positive_zero_luck_kelly_wager"
                                        : interactionTiles.Length == 0
                                            ? "blocked_wheel_interaction_tile_unavailable"
                                            : "ready";

        var activeWheelState = activeWheel is null
            ? null
            : new
            {
                runtime_identity = RuntimeHelpers.GetHashCode(activeWheel).ToString("X8"),
                selected_color = festival!.specialEventVariable2 ? "green" : "orange",
                wager_star_tokens = ReadWheelInt(activeWheel, FairWheelWagerField),
                timer_before_start_ms = ReadWheelInt(activeWheel, FairWheelTimerField),
                arrow_rotation_radians = activeWheel.arrowRotation,
                arrow_rotation_velocity = activeWheel.arrowRotationVelocity,
                arrow_rotation_deceleration = activeWheel.arrowRotationDeceleration,
                done_spinning = ReadWheelBool(activeWheel, FairWheelDoneField),
                result_text_active = FairWheelResultTextField?.GetValue(activeWheel) is not null
            };
        var activeNumberState = activeNumber is null
            ? null
            : new
            {
                minimum = ReadWheelInt(activeNumber, FairWheelNumberMinimumField),
                maximum = ReadWheelInt(activeNumber, FairWheelNumberMaximumField),
                current = ReadWheelInt(activeNumber, FairWheelNumberCurrentField),
                price = ReadWheelInt(activeNumber, FairWheelNumberPriceField),
                text = (FairWheelNumberTextBoxField?.GetValue(activeNumber) as TextBox)?.Text ?? string.Empty,
                ok_bounds = FairWheelRect(activeNumber.okButton.bounds),
                cancel_bounds = FairWheelRect(activeNumber.cancelButton.bounds)
            };
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "fair_wheel_spin.v1",
            festival_id = festival!.id,
            location = location.NameOrUniqueName,
            player.festivalScore,
            luck_level = player.LuckLevel,
            stardropAcquired,
            projectedGrangeTokens,
            remainingDemand,
            wager,
            interactionTiles,
            menuType,
            activeWheelState,
            activeNumberState
        }));

        return new
        {
            schema_version = "fair_wheel_spin.v1",
            projection_status = activeWheel is not null
                ? "complete_active_native_wheel_spin_context"
                : activeNumber is not null
                    ? "complete_active_native_wager_selection_context"
                    : "complete_current_festival_fall16_wheel_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            festival_id = festival.id,
            festival_location_id = location.NameOrUniqueName,
            festival_score = player.festivalScore,
            stardrop_acquired = stardropAcquired,
            stardrop_price_star_tokens = FairStardropPrice,
            projected_unclaimed_grange_tokens = projectedGrangeTokens,
            remaining_star_token_demand = remainingDemand,
            demand_policy = "automatic_only_for_unacquired_fair_stardrop_demand_at_least_two;exact_one_uses_free_strength_game",
            selected_color = "green",
            wager_star_tokens = wager,
            wager_policy = FairWheelWagerPolicy,
            zero_luck_kelly_fraction = "7/15",
            zero_luck_kelly_wager_before_demand_cap = player.festivalScore * 7 / 15,
            projected_win_festival_score = player.festivalScore + wager,
            projected_loss_festival_score = player.festivalScore - wager,
            effective_luck_level = player.LuckLevel,
            daily_luck_not_used = true,
            base_zero_luck_distribution = new
            {
                constructor_outcomes = 30,
                green_wins = 22,
                orange_wins = 8,
                green_probability = 22d / 30d,
                orange_probability = 8d / 30d,
                expected_green_delta_per_wager = 7d / 15d
            },
            random_contract = new
            {
                initial_velocity = "pi/16 + Next(0,15)*pi/256 + (NextBool?pi/64:0)",
                deceleration_per_update = -0.0006283185307179586d,
                luck_green_retry_gate = "selected_green && rotation>pi/2 && rotation<=4.319689898685965 && NextDouble<LuckLevel/15",
                luck_orange_retry_gate = "selected_orange && (rotation+pi)%(2*pi)<=4.319689898685965 && NextDouble<LuckLevel/20",
                green_win_rotation = "not(rotation>pi/2 && rotation<=3*pi/2)",
                orange_win_rotation = "rotation>pi/2 && rotation<=3*pi/2"
            },
            prestart_duration_ms = 1000,
            result_duration_ms = 2500,
            result_contract = "win:festivalScore+=wager;loss:festivalScore-=wager;both_native_SparklingText_then_exit",
            dialogue_key = "wheelBet",
            response_key = "Green",
            number_selection_minimum = 1,
            number_selection_maximum = player.festivalScore,
            interaction_tile_indexes = new[] { 308, 309 },
            interaction_tiles = interactionTiles,
            shop_id = FairTokenShopId,
            shop_currency = "star_tokens",
            shop_rows = shopRows,
            active_wheel = activeWheelState,
            active_number_selection = activeNumberState,
            native_contract = FairWheelNativeContract
        };
    }

    private static object[] ReadFairWheelInteractionTiles(GameLocation location)
    {
        if (location.Map?.Layers.Count is not > 0)
            return Array.Empty<object>();
        var layer = location.Map.Layers[0];
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
            if (tileIndex is not (308 or 309))
                continue;
            foreach (var stand in new[]
            {
                new Microsoft.Xna.Framework.Point(x + 1, y),
                new Microsoft.Xna.Framework.Point(x - 1, y),
                new Microsoft.Xna.Framework.Point(x, y + 1),
                new Microsoft.Xna.Framework.Point(x, y - 1)
            })
            {
                result.Add(new
                {
                    tile_x = x,
                    tile_y = y,
                    tile_index = tileIndex,
                    stand_tile_x = stand.X,
                    stand_tile_y = stand.Y
                });
            }
        }
        return result.ToArray();
    }

    private static FieldInfo? WheelField(string name) =>
        typeof(WheelSpinGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static FieldInfo? NumberSelectionField(string name) =>
        typeof(NumberSelectionMenu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static int? ReadWheelInt(object source, FieldInfo? field) =>
        field?.GetValue(source) is int value ? value : null;

    private static bool? ReadWheelBool(object source, FieldInfo? field) =>
        field?.GetValue(source) is bool value ? value : null;

    private static object FairWheelRect(Microsoft.Xna.Framework.Rectangle value) => new
    {
        x = value.X,
        y = value.Y,
        width = value.Width,
        height = value.Height
    };
}
