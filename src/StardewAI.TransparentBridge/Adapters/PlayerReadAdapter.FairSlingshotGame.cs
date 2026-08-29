using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Minigames;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FairSlingshotNativeContract =
        "Event.checkAction(festival_fall16_buildings_501_502)->DialogueBox(slingshotGame:Play).receiveLeftClick->Event.answerDialogue(slingshotGame,0)->Money-50->globalFadeToBlack(TargetGame.startMe)->native_50000ms_TargetGame_input_session->accuracy_multiplier_score_reward->festivalScore";

    private static readonly FieldInfo? FairSlingshotLocationField = PrivateField<TargetGame>("location");
    private static readonly FieldInfo? FairSlingshotTimerToStartField = PrivateField<TargetGame>("timerToStart");
    private static readonly FieldInfo? FairSlingshotGameEndTimerField = PrivateField<TargetGame>("gameEndTimer");
    private static readonly FieldInfo? FairSlingshotShowResultsTimerField = PrivateField<TargetGame>("showResultsTimer");
    private static readonly FieldInfo? FairSlingshotGameDoneField = PrivateField<TargetGame>("gameDone");
    private static readonly FieldInfo? FairSlingshotExitField = PrivateField<TargetGame>("exit");
    private static readonly FieldInfo? FairSlingshotModifierBonusField = PrivateField<TargetGame>("modifierBonus");
    private static readonly FieldInfo? FairTargetTypeField = PrivateField<TargetGame.Target>("targetType");
    private static readonly FieldInfo? FairTargetCountdownField = PrivateField<TargetGame.Target>("countdownBeforeSpawn");
    private static readonly FieldInfo? FairTargetPausePositionField = PrivateField<TargetGame.Target>("xPausePosition");
    private static readonly FieldInfo? FairTargetPauseTimeField = PrivateField<TargetGame.Target>("xPauseTime");
    private static readonly FieldInfo? FairTargetSpeedField = PrivateField<TargetGame.Target>("speed");
    private static readonly FieldInfo? FairTargetSpawnedField = PrivateField<TargetGame.Target>("spawned");
    private static readonly FieldInfo? FairTargetAtPauseField = PrivateField<TargetGame.Target>("atPausePosition");

    private static object ReadFairSlingshotGameContext(Farmer? player)
    {
        if (player?.currentLocation is null)
            return new { projection_status = "unavailable_world_player_or_location", interaction_tiles = Array.Empty<object>() };

        var activeGame = Game1.currentMinigame as TargetGame;
        var festivalLocation = player.currentLocation;
        var festival = festivalLocation.currentEvent;
        var activeFair = festival is not null && festival.isFestival &&
            string.Equals(festival.id, "festival_fall16", StringComparison.Ordinal);
        if (!activeFair)
        {
            return new
            {
                schema_version = "fair_slingshot_game.v1",
                projection_status = "contextual_inactive_festival_fall16",
                native_contract = FairSlingshotNativeContract,
                interaction_tiles = Array.Empty<object>(),
                target_sequence = FairSlingshotTargetSequence(),
                shop_rows = Array.Empty<object>()
            };
        }

        var interactionTiles = ReadFairSlingshotInteractionTiles(festivalLocation);
        var shopRows = ReadFairStarTokenShopRows();
        var (stardropAcquired, projectedGrangeTokens, remainingDemand) =
            ReadFairStardropDemand(player, festival!);
        var activeMenu = Game1.activeClickableMenu?.GetType().FullName ?? "none";
        var activeMinigame = Game1.currentMinigame?.minigameId() ?? "none";
        var gateStatus = activeGame is not null
            ? "active_native_slingshot_game"
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
                                    ? "blocked_slingshot_game_interaction_tile_unavailable"
                                    : "ready";

        var slingshot = player.CurrentTool as Slingshot;
        var activeState = activeGame is null
            ? null
            : new
            {
                minigame_id = activeGame.minigameId(),
                score = TargetGame.score,
                shots_fired = TargetGame.shotsFired,
                successful_shots = TargetGame.successShots,
                accuracy_percent = TargetGame.accuracy,
                star_tokens_won = TargetGame.starTokensWon,
                timer_to_start_ms = ReadPrivateInt(activeGame, FairSlingshotTimerToStartField),
                game_end_timer_ms = ReadPrivateInt(activeGame, FairSlingshotGameEndTimerField),
                show_results_timer_ms = ReadPrivateInt(activeGame, FairSlingshotShowResultsTimerField),
                game_done = ReadPrivateBool(activeGame, FairSlingshotGameDoneField),
                exit = ReadPrivateBool(activeGame, FairSlingshotExitField),
                modifier_bonus = ReadPrivateFloat(activeGame, FairSlingshotModifierBonusField),
                temporary_location_id = (FairSlingshotLocationField?.GetValue(activeGame) as GameLocation)?.NameOrUniqueName ?? string.Empty,
                slingshot_qualified_item_id = slingshot?.QualifiedItemId ?? string.Empty,
                ammo_qualified_item_id = slingshot?.attachments.ElementAtOrDefault(0)?.QualifiedItemId ?? string.Empty,
                ammo_stack = slingshot?.attachments.ElementAtOrDefault(0)?.Stack ?? 0,
                projectile_count = (FairSlingshotLocationField?.GetValue(activeGame) as GameLocation)?.projectiles.Count ?? 0,
                targets = activeGame.targets.Select(ReadFairSlingshotLiveTarget).ToArray()
            };
        var returnTile = Game1.year % 2 == 0
            ? new { tile_x = 24, tile_y = 70 }
            : new { tile_x = 24, tile_y = 63 };
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "fair_slingshot_game.v1",
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
            schema_version = "fair_slingshot_game.v1",
            projection_status = activeGame is null
                ? "complete_current_festival_fall16_slingshot_game_context"
                : "complete_active_native_slingshot_game_context",
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
            prestart_duration_ms = 1000,
            game_duration_ms = 50000,
            post_game_delay_ms = 1000,
            results_duration_ms = 16100,
            scoring_contract = new
            {
                target_points = new { basic = 1, bonus = 2, deluxe = 5 },
                shot_success_gate = "first target hit by a positive-damage BasicProjectile",
                accuracy_formula = "max(0,round(successShots/(shotsFired-1),2)*100)",
                accuracy_multipliers = new[]
                {
                    new { minimum_percent = 75, multiplier = 1.5 },
                    new { minimum_percent = 85, multiplier = 2.0 },
                    new { minimum_percent = 90, multiplier = 2.5 },
                    new { minimum_percent = 95, multiplier = 3.0 },
                    new { minimum_percent = 100, multiplier = 4.0 }
                },
                reward_minimum_score = 40,
                star_token_formula = "score>=40 ? int(((score*2-30)/10)*2.5)*2 : 0; reward>280 => 500"
            },
            native_loadout = new
            {
                slingshot = "(W)32",
                ammo = "(O)390",
                ammo_stack = 999,
                projectile_speed_pixels_per_tick = new { minimum = 19, maximum = 20 }
            },
            execution_strategy = "native_predictive_intercept_legal_input",
            dialogue_key = "slingshotGame",
            play_response_key = "Play",
            repeatable_while_money_available = true,
            available_after_grange_judging = true,
            return_tile = returnTile,
            interaction_tiles = interactionTiles,
            target_sequence = FairSlingshotTargetSequence(),
            shop_id = FairTokenShopId,
            shop_currency = "star_tokens",
            shop_rows = shopRows,
            active_minigame = activeState,
            native_contract = FairSlingshotNativeContract
        };
    }

    private static object ReadFairSlingshotLiveTarget(TargetGame.Target target)
    {
        var targetType = ReadPrivateInt(target, FairTargetTypeField);
        return new
        {
            runtime_identity = RuntimeHelpers.GetHashCode(target).ToString("X8"),
            target_type = targetType,
            target_type_name = FairSlingshotTargetTypeName(targetType ?? -1),
            point_value = FairSlingshotTargetPointValue(targetType ?? -1),
            position_x = target.Position.X,
            position_y = target.Position.Y,
            width = target.Position.Width,
            height = target.Position.Height,
            countdown_before_spawn_ms = ReadPrivateInt(target, FairTargetCountdownField),
            speed_x_pixels_per_tick = ReadPrivateInt(target, FairTargetSpeedField),
            spawned = ReadPrivateBool(target, FairTargetSpawnedField),
            at_pause_position = ReadPrivateBool(target, FairTargetAtPauseField),
            pause_position_x = ReadPrivateInt(target, FairTargetPausePositionField),
            pause_time_remaining_ms = ReadPrivateInt(target, FairTargetPauseTimeField)
        };
    }

    private static object[] ReadFairSlingshotInteractionTiles(GameLocation location)
    {
        if (location.Map?.Layers.Count is not > 0)
            return Array.Empty<object>();
        var layer = location.Map.Layers[0];
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var tileIndex = location.getTileIndexAt(x, y, "Buildings", "untitled tile sheet");
            if (tileIndex is 501 or 502)
                result.Add(new { tile_x = x, tile_y = y, tile_index = tileIndex });
        }
        return result.ToArray();
    }

    private static object[] FairSlingshotTargetSequence()
    {
        var result = new List<object>();
        AddFairTargetRow(result, 0, 320, 1500, 5, 4, false, 0);
        AddFairTargetRow(result, 4000, 448, 1000, 5, 4, true, 0);
        AddFairTargetRow(result, 8000, 128, 2000, 5, 4, false, 1);
        AddFairTwinPausers(result, 8000, 576, 384, 576, 5, 2000, 1);
        AddFairTwinPausers(result, 15000, 576, 128, 832, 4, 4000, 1);
        AddFairTargetRow(result, 18000, 320, 1500, 5, 4, false, 0);
        AddFairTargetRow(result, 21000, 448, 1000, 5, 4, true, 0);
        AddFairTwinPausers(result, 25000, 832, 128, 832, 5, 1500, 2);
        AddFairTargetRow(result, 27000, 576, 500, 8, 2, true, 0);
        AddFairTargetRow(result, 28000, 448, 500, 8, 2, true, 0);
        AddFairTargetRow(result, 29000, 320, 500, 8, 2, true, 0);
        AddFairTargetRow(result, 30000, 128, 500, 8, 2, true, 0);
        AddFairTwinPausers(result, 36000, 832, 128, 832, 5, 2000, 2);
        AddFairTargetRow(result, 41000, 320, 1500, 5, 4, false, 0);
        AddFairTargetRow(result, 42000, 448, 1000, 5, 4, true, 0);
        AddFairTargetRow(result, 43000, 128, 1000, 4, 4, false, 0);
        return result.ToArray();
    }

    private static void AddFairTargetRow(List<object> rows, int initialDelay, int laneY, int spacing,
        int count, int speed, bool spawnFromRight, int targetType)
    {
        for (var index = 0; index < count; index++)
            rows.Add(FairTargetSequenceRow(initialDelay + index * spacing, laneY, speed, spawnFromRight,
                targetType, null, null));
    }

    private static void AddFairTwinPausers(List<object> rows, int delay, int laneY, int leftPauseX,
        int rightPauseX, int speed, int pauseMs, int targetType)
    {
        rows.Add(FairTargetSequenceRow(delay, laneY, speed, false, targetType, leftPauseX, pauseMs));
        rows.Add(FairTargetSequenceRow(delay, laneY, speed, true, targetType, rightPauseX, pauseMs));
    }

    private static object FairTargetSequenceRow(int delay, int laneY, int speed, bool spawnFromRight,
        int targetType, int? pauseX, int? pauseMs) => new
    {
        spawn_delay_ms = delay,
        lane_y = laneY,
        speed_pixels_per_tick = speed,
        spawn_from_right = spawnFromRight,
        target_type = targetType,
        target_type_name = FairSlingshotTargetTypeName(targetType),
        point_value = FairSlingshotTargetPointValue(targetType),
        pause_x = pauseX,
        pause_duration_ms = pauseMs
    };

    private static string FairSlingshotTargetTypeName(int type) => type switch
    {
        0 => "basic",
        1 => "bonus",
        2 => "deluxe",
        _ => "unknown"
    };

    private static int FairSlingshotTargetPointValue(int type) => type switch
    {
        0 => 1,
        1 => 2,
        2 => 5,
        _ => 0
    };

    private static FieldInfo? PrivateField<T>(string name) =>
        typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private static int? ReadPrivateInt(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is int value ? value : null;

    private static bool? ReadPrivateBool(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is bool value ? value : null;

    private static float? ReadPrivateFloat(object instance, FieldInfo? field) =>
        field?.GetValue(instance) is float value ? value : null;
}
