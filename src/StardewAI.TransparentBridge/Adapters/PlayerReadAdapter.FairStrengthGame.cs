using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FairStrengthNativeContract =
        "Event.checkAction(festival_fall16_buildings_540,player_tile_x_29)->StrengthGame.receiveLeftClick->FarmerSprite.animateOnce(168,80ms,8)->StrengthGame.afterSwingAnimation->power>=99->festivalScore+1->native_result_dialogue_and_exit";

    private static readonly FieldInfo? FairStrengthPowerField = StrengthField("power");
    private static readonly FieldInfo? FairStrengthChangeSpeedField = StrengthField("changeSpeed");
    private static readonly FieldInfo? FairStrengthEndTimerField = StrengthField("endTimer");
    private static readonly FieldInfo? FairStrengthTransparencyField = StrengthField("transparency");
    private static readonly FieldInfo? FairStrengthVictorySoundField = StrengthField("victorySound");
    private static readonly FieldInfo? FairStrengthClickedField = StrengthField("clicked");
    private static readonly FieldInfo? FairStrengthShowedResultField = StrengthField("showedResult");

    private static object ReadFairStrengthGameContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var activeGame = Game1.activeClickableMenu as StrengthGame;
        var festivalLocation = player.currentLocation;
        var festival = festivalLocation.currentEvent;
        var activeFair = festival is not null && festival.isFestival &&
            string.Equals(festival.id, "festival_fall16", StringComparison.Ordinal);
        if (!activeFair)
        {
            return new
            {
                schema_version = "fair_strength_game.v1",
                projection_status = "contextual_inactive_festival_fall16",
                native_contract = FairStrengthNativeContract,
                interaction_tiles = Array.Empty<object>(),
                shop_rows = Array.Empty<object>()
            };
        }

        var interactionTiles = ReadFairStrengthInteractionTiles(festivalLocation);
        var shopRows = ReadFairStarTokenShopRows();
        var (stardropAcquired, projectedGrangeTokens, remainingDemand) =
            ReadFairStardropDemand(player, festival!);
        var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "none";
        var gateStatus = activeGame is not null
            ? "active_native_strength_game"
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
                            : remainingDemand != 1
                                ? "deferred_strength_game_not_exact_one_token_top_up"
                                : interactionTiles.Length == 0
                                    ? "blocked_strength_game_interaction_tile_unavailable"
                                    : "ready";

        var activeState = activeGame is null
            ? null
            : new
            {
                runtime_identity = RuntimeHelpers.GetHashCode(activeGame).ToString("X8"),
                power = ReadStrengthFloat(activeGame, FairStrengthPowerField),
                change_speed_per_update = ReadStrengthFloat(activeGame, FairStrengthChangeSpeedField),
                end_timer_ms = ReadStrengthFloat(activeGame, FairStrengthEndTimerField),
                transparency = ReadStrengthFloat(activeGame, FairStrengthTransparencyField),
                victory_sound_played = ReadStrengthBool(activeGame, FairStrengthVictorySoundField),
                clicked = ReadStrengthBool(activeGame, FairStrengthClickedField),
                showed_result = ReadStrengthBool(activeGame, FairStrengthShowedResultField),
                dialogue_up = Game1.dialogueUp,
                dialogue_typing = Game1.dialogueTyping,
                player_tool_override_active = player.toolOverrideFunction is not null,
                player_current_tool_index = player.CurrentToolIndex,
                player_facing_direction = player.FacingDirection,
                player_on_tool_animation = player.FarmerSprite.isOnToolAnimation()
            };
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "fair_strength_game.v1",
            festival_id = festival!.id,
            location = festivalLocation.NameOrUniqueName,
            player.festivalScore,
            stardropAcquired,
            projectedGrangeTokens,
            remainingDemand,
            interactionTiles,
            activeMenu,
            activeState
        }));

        return new
        {
            schema_version = "fair_strength_game.v1",
            projection_status = activeGame is null
                ? "complete_current_festival_fall16_strength_game_context"
                : "complete_active_native_strength_game_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            festival_id = festival.id,
            festival_location_id = festivalLocation.NameOrUniqueName,
            festival_score = player.festivalScore,
            stardrop_acquired = stardropAcquired,
            stardrop_price_star_tokens = FairStardropPrice,
            projected_unclaimed_grange_tokens = projectedGrangeTokens,
            remaining_star_token_demand = remainingDemand,
            demand_policy = "automatic_only_when_unacquired_fair_stardrop_is_exactly_one_star_token_short_after_unclaimed_grange_projection;explicit_future_shop_goals_may_request_separate_policy",
            entry_fee_money = 0,
            expected_reward_star_tokens = 1,
            perfect_power_minimum = 99f,
            power_minimum = 0f,
            power_maximum = 100f,
            initial_change_speed_per_update = new { minimum = 3f, maximum = 4f, integer_only = true },
            swing_animation = new { start_frame = 168, interval_ms = 80f, frame_count = 8 },
            perfect_result_delay_ms = 2000f,
            ordinary_result_delay_ms = 1000f,
            scoring_contract = new
            {
                maximum_power_reward_gate = "power>=99",
                minimum_power_reward_gate = "power<2",
                rewarded_star_tokens = 1,
                middle_power_reward = 0,
                selected_execution_target = "maximum_power_only"
            },
            execution_strategy = "native_predictive_single_click_max_power",
            direct_menu_entry = true,
            dialogue_key = string.Empty,
            repeatable_without_fee = true,
            available_after_grange_judging = true,
            required_player_tile_x = 29,
            interaction_tiles = interactionTiles,
            shop_id = FairTokenShopId,
            shop_currency = "star_tokens",
            shop_rows = shopRows,
            active_menu = activeState,
            native_contract = FairStrengthNativeContract
        };
    }

    private static object[] ReadFairStrengthInteractionTiles(GameLocation location)
    {
        if (location.Map?.Layers.Count is not > 0)
            return Array.Empty<object>();
        var layer = location.Map.Layers[0];
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
            if (tileIndex != 540)
                continue;
            foreach (var stand in new[]
            {
                new Microsoft.Xna.Framework.Point(x + 1, y),
                new Microsoft.Xna.Framework.Point(x - 1, y),
                new Microsoft.Xna.Framework.Point(x, y + 1),
                new Microsoft.Xna.Framework.Point(x, y - 1)
            })
            {
                if (stand.X == 29)
                {
                    result.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        tile_index = tileIndex,
                        stand_tile_x = stand.X,
                        stand_tile_y = stand.Y,
                        required_player_tile_x = 29
                    });
                }
            }
        }
        return result.ToArray();
    }

    private static FieldInfo? StrengthField(string name) =>
        typeof(StrengthGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static float? ReadStrengthFloat(object source, FieldInfo? field) =>
        field?.GetValue(source) is float value ? value : null;

    private static bool? ReadStrengthBool(object source, FieldInfo? field) =>
        field?.GetValue(source) is bool value ? value : null;
}
