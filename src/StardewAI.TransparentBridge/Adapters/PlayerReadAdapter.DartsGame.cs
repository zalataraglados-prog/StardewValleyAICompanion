using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Minigames;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string DartsNativeContract =
        "IslandSouthEastCave_DartsGame_checkAction_then_yes_then_native_Darts_mouse_aim_charge_release_then_native_limited_nut_drop";

    private static readonly FieldInfo? DartsCanCancelShotField = PrivateField<Darts>("canCancelShot");
    private static readonly FieldInfo? DartsScreenWidthField = PrivateField<Darts>("screenWidth");
    private static readonly FieldInfo? DartsScreenHeightField = PrivateField<Darts>("screenHeight");

    private static object ReadDartsGameContext(Farmer? player)
    {
        if (player is null)
            return new { projection_status = "unavailable_world_or_player", interaction_tiles = Array.Empty<object>() };

        var cave = Game1.getLocationFromName("IslandSouthEastCave") as IslandSouthEastCave;
        var active = Game1.currentMinigame as Darts;
        var inCave = ReferenceEquals(player.currentLocation, cave);
        var caveRaining = cave?.IsRainingHere();
        var pirateNight = caveRaining is false && Game1.timeOfDay >= 2000 && Game1.dayOfMonth % 2 == 0;
        var interactions = cave is null ? Array.Empty<object>() : ReadDartsInteractionTiles(cave);
        var dropped = player.team.GetDroppedLimitedNutCount("Darts");
        var startingDarts = dropped switch { 1 => 15, 2 => 10, _ => 20 };
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var gateStatus = active is not null
            ? "active_native_darts_game"
            : dropped >= 3
                ? "complete_three_darts_walnuts_dropped"
                : cave is null
                    ? "blocked_pirate_cove_unavailable"
                    : !pirateNight
                        ? "blocked_not_pirate_night"
                        : !inCave
                            ? "route_to_pirate_cove_required"
                            : !menuClear || Game1.eventUp || player.UsingTool || !player.CanMove
                                ? "blocked_player_busy"
                                : interactions.Length == 0
                                    ? "blocked_darts_interaction_tile_unavailable"
                                    : "ready";
        var activeState = active is null ? null : ReadActiveDartsGame(active);
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "darts_game.v1",
            cave_available = cave is not null,
            Game1.dayOfMonth,
            Game1.timeOfDay,
            cave_raining = caveRaining,
            pirateNight,
            dropped,
            startingDarts,
            interactions,
            activeState
        }));

        return new
        {
            schema_version = "darts_game.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            invocation_policy = "autonomous_progression",
            location_id = "IslandSouthEastCave",
            is_current_location = inCave,
            player_can_move = player.CanMove,
            player_using_tool = player.UsingTool,
            player_freeze_pause = player.freezePause,
            dialogue_up = Game1.dialogueUp,
            event_up = Game1.eventUp,
            active_menu_type = Game1.activeClickableMenu?.GetType().Name ?? "none",
            day_of_month = Game1.dayOfMonth,
            time_of_day = Game1.timeOfDay,
            location_context_id = cave?.GetLocationContextId() ?? "Island",
            raining_here = caveRaining,
            pirate_night = pirateNight,
            pirate_night_rule = "not_raining_here_and_time_at_least_2000_and_even_day",
            limited_nut_key = "Darts",
            limited_nut_limit = 3,
            limited_nut_dropped_before = dropped,
            limited_nut_dropped_after = Math.Min(3, dropped + 1),
            starting_dart_count = startingDarts,
            starting_points = 301,
            perfect_victory_max_throws = 6,
            perfect_score_plan = "T20,T20,T20,T20,T17,D5",
            charge_release_threshold = 0.02f,
            declared_interaction_tile = new { tile_x = 30, tile_y = 8, action_token = "DartsGame" },
            interaction_tiles = interactions,
            active_session = activeState,
            native_contract = DartsNativeContract
        };
    }

    private static object[] ReadDartsInteractionTiles(IslandSouthEastCave cave)
    {
        var layer = cave.Map?.GetLayer("Buildings");
        if (layer is null)
            return Array.Empty<object>();
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = cave.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action, "DartsGame", StringComparison.Ordinal))
                continue;
            result.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = "DartsGame" });
        }
        return result.ToArray();
    }

    private static object ReadActiveDartsGame(Darts game) => new
    {
        runtime_identity = RuntimeHelpers.GetHashCode(game).ToString("X8"),
        minigame_id = game.minigameId(),
        state = game.currentGameState.ToString(),
        state_timer = game.stateTimer,
        game_paused = game.gamePaused,
        cursor_position = game.cursorPosition,
        aim_position = game.aimPosition,
        dart_board_center = game.dartBoardCenter,
        charge_time = game.chargeTime,
        charge_direction = game.chargeDirection,
        can_cancel_shot = DartsCanCancelShotField?.GetValue(game) as bool? ?? false,
        hang_time = game.hangTime,
        previous_points = game.previousPoints,
        points = game.points,
        next_point_transfer_time = game.nextPointTransferTime,
        throw_start_position = game.throwStartPosition,
        dart_position = game.dartPosition,
        dart_time = game.dartTime,
        last_hit_string = game.lastHitString,
        last_hit_amount = game.lastHitAmount,
        last_hit_was_double = game.lastHitWasDouble,
        starting_dart_count = game.startingDartCount,
        dart_count = game.dartCount,
        throws_count = game.throwsCount,
        is_perfect_victory = game.IsPerfectVictory(),
        screen_width = DartsScreenWidthField?.GetValue(game) as int? ?? 0,
        screen_height = DartsScreenHeightField?.GetValue(game) as int? ?? 0,
        pixel_scale = game.pixelScale,
        upper_left = game.upperLeft
    };
}
