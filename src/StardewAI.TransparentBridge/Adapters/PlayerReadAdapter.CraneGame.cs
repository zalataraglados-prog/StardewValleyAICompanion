using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Minigames;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string CraneGameNativeContract =
        "MovieTheater_CraneGame_checkAction_then_yes_500g_then_native_CraneGame_directional_input_then_native_ItemGrabMenu_rewards";

    private static readonly FieldInfo? CranePrizeItemField = PrivateField<CraneGame.Prize>("_item");
    private static readonly FieldInfo? CranePrizeVelocityField = PrivateField<CraneGame.Prize>("_velocity");
    private static readonly FieldInfo? CranePrizeConveyorMoveField = PrivateField<CraneGame.Prize>("_conveyerBeltMove");
    private static readonly FieldInfo? CranePrizeCollectingField = PrivateField<CraneGame.Prize>("_isBeingCollected");
    private static readonly FieldInfo? CraneLogicClawField = PrivateField<CraneGame.GameLogic>("_claw");
    private static readonly FieldInfo? CraneLogicStateTimerField = PrivateField<CraneGame.GameLogic>("_stateTimer");
    private static readonly FieldInfo? CraneClawOffsetField = PrivateField<CraneGame.Claw>("_prizePositionOffset");
    private static readonly FieldInfo? CraneClawDropChecksField = PrivateField<CraneGame.Claw>("_dropChances");

    private static object ReadCraneGameContext(Farmer? player)
    {
        if (player is null)
            return new { projection_status = "unavailable_world_or_player", interaction_tiles = Array.Empty<object>() };

        var theater = Game1.getLocationFromName("MovieTheater") as MovieTheater;
        var active = Game1.currentMinigame as CraneGame;
        var interactions = theater is null ? Array.Empty<object>() : ReadCraneGameInteractionTiles(theater);
        var occupied = theater?.Map?.GetLayer("Buildings")?.Tiles[2, 9] is not null;
        var currentMovie = theater is null ? null : MovieTheater.GetMovieToday();
        var emptySlots = player.Items.Count(item => item is null);
        var inTheater = player.currentLocation is MovieTheater;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var gateStatus = active is not null
            ? "active_native_crane_game"
            : theater is null
                ? "blocked_movie_theater_unavailable"
                : occupied
                    ? "blocked_crane_game_occupied"
                    : player.Money < 500
                        ? "blocked_crane_game_fee_required"
                        : emptySlots < 3
                            ? "blocked_three_reward_slots_required"
                            : !inTheater
                                ? "route_to_movie_theater_required"
                                : !menuClear || Game1.eventUp || player.UsingTool || !player.CanMove
                                    ? "blocked_player_busy"
                                    : interactions.Length == 0
                                        ? "blocked_crane_game_interaction_tile_unavailable"
                                        : "ready";
        var activeState = active is null ? null : ReadActiveCraneGame(active);
        var movieRules = currentMovie is null ? null : new
        {
            movie_id = currentMovie.Id,
            clear_default_crane_prize_groups = currentMovie.ClearDefaultCranePrizeGroups,
            crane_prizes = currentMovie.CranePrizes
        };
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "crane_game.v1",
            theater_available = theater is not null,
            player.Money,
            emptySlots,
            occupied,
            interactions,
            movieRules,
            activeState
        }));

        return new
        {
            schema_version = "crane_game.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            invocation_policy = "player_command_only",
            location_id = "MovieTheater",
            is_current_location = inTheater,
            machine_occupied = occupied,
            fee_gold = 500,
            money = player.Money,
            inventory_empty_slots = emptySlots,
            attempts_per_session = 3,
            timer_ticks_per_attempt = 900,
            horizontal_startup_ticks = 15,
            vertical_startup_ticks = 11,
            claw_speed_pixels_per_tick = 0.5,
            drop_check_interval_min_ticks = 50,
            drop_check_interval_max_exclusive_ticks = 100,
            drop_chances = 3,
            selection_policy = "best_reachable_live_prize_nonlarge_stationary_then_distance;refresh_each_attempt",
            interaction_tiles = interactions,
            base_prize_groups = CraneBasePrizeGroups(),
            current_movie_rules = movieRules,
            active_session = activeState,
            native_contract = CraneGameNativeContract
        };
    }

    private static object[] ReadCraneGameInteractionTiles(MovieTheater theater)
    {
        var layer = theater.Map?.GetLayer("Buildings");
        if (layer is null)
            return Array.Empty<object>();
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = theater.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action, "CraneGame", StringComparison.Ordinal))
                continue;
            result.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = "CraneGame" });
        }
        return result.ToArray();
    }

    private static object CraneBasePrizeGroups() => new
    {
        rarity_1 = new[] { "(F)1760", "(F)1761", "(F)1762", "(F)1763", "(F)1764", "(F)1365" },
        rarity_2 = new[] { "(F)1669", "seasonal:(F)1960|(F)1961|(F)1294|(F)1918", "(F)FancyHousePlant5", "(F)FancyHousePlant4", "(BC)2" },
        rarity_3 = new[]
        {
            "spring:(BC)107|(BC)36|(BC)48|(BC)184|(BC)188|(BC)192|(BC)204",
            "winter:(F)1440|(BC)44|(BC)40|(BC)41|(BC)43|(BC)42",
            "summer:(F)985|(F)984",
            "fall:(F)1917|(F)1307|(BC)47|(F)1471|(F)1375"
        },
        independent_random = new[] { "10%:(O)107|(O)749x5|(O)688x5|(O)288x5", "18%:(O)809", "25%:(F)986x2|75%:(F)989x2" }
    };

    private static object ReadActiveCraneGame(CraneGame game)
    {
        var logic = game.GetObjectOfType<CraneGame.GameLogic>();
        var claw = logic is null ? null : CraneLogicClawField?.GetValue(logic) as CraneGame.Claw;
        var prizes = game.GetObjectsOfType<CraneGame.Prize>()
            .Select((prize, index) =>
            {
                var item = CranePrizeItemField?.GetValue(prize) as Item;
                return new
                {
                    index,
                    runtime_identity = RuntimeHelpers.GetHashCode(prize).ToString("X8"),
                    qualified_item_id = item?.QualifiedItemId ?? string.Empty,
                    stack = item?.Stack ?? 0,
                    position_x = prize.position.X,
                    position_y = prize.position.Y,
                    z_position = prize.zPosition,
                    resting_z_position = prize.GetRestingZPosition(),
                    can_be_grabbed = prize.CanBeGrabbed(),
                    grabbed = prize.grabbed,
                    is_large_item = prize.isLargeItem,
                    velocity = CranePrizeVelocityField?.GetValue(prize),
                    conveyor_move = CranePrizeConveyorMoveField?.GetValue(prize),
                    is_being_collected = CranePrizeCollectingField?.GetValue(prize) as bool? ?? false
                };
            })
            .ToArray();
        return new
        {
            runtime_identity = RuntimeHelpers.GetHashCode(game).ToString("X8"),
            state = logic?.GetCurrentState().ToString() ?? "unavailable",
            state_timer = logic is null ? 0 : ReadPrivateInt(logic, CraneLogicStateTimerField),
            timer_ticks = logic?.currentTimer ?? 0,
            lives = logic?.lives ?? 0,
            max_lives = logic?.maxLives ?? 0,
            collected_items = logic?.collectedItems.Select(item => new { qualified_item_id = item.QualifiedItemId, stack = item.Stack }).ToArray() ?? Array.Empty<object>(),
            claw = claw is null ? null : new
            {
                position_x = claw.position.X,
                position_y = claw.position.Y,
                z_position = claw.zPosition,
                open_angle = claw.openAngle,
                grabbed_prize_identity = claw.GetGrabbedPrize() is { } grabbed ? RuntimeHelpers.GetHashCode(grabbed).ToString("X8") : string.Empty,
                prize_position_offset = CraneClawOffsetField?.GetValue(claw),
                remaining_drop_chances = CraneClawDropChecksField?.GetValue(claw) as int? ?? 0
            },
            prizes
        };
    }
}
