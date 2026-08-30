using System.Runtime.CompilerServices;
using System.Text.Json;
using StardewValley;
using StardewValley.Minigames;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string PrairieKingEquivalentContract =
        "Saloon_Arcade_Prairie_checkAction_optional_CowboyGame_NewGame_then_timed_equivalent_then_AbigailGame_usePowerup_minus3_native_phase1_settlement";

    private static object ReadPrairieKingContext(Farmer? player)
    {
        if (player is null)
            return new { projection_status = "unavailable_world_or_player", interaction_tiles = Array.Empty<object>() };

        var saloon = Game1.getLocationFromName("Saloon");
        var active = Game1.currentMinigame as AbigailGame;
        var inSaloon = ReferenceEquals(player.currentLocation, saloon);
        var interactions = saloon is null
            ? Array.Empty<object>()
            : ReadPrairieKingInteractionTiles(saloon);
        var completed = player.stats.Get("completedPrairieKing");
        var completedWithoutDying = player.stats.Get("completedPrairieKingWithoutDying");
        var progress = player.jotpkProgress.Value;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var gateStatus = active is not null
            ? "active_native_prairie_king"
            : completedWithoutDying > 0
                ? "complete_prairie_king_without_dying"
                : saloon is null
                    ? "blocked_saloon_unavailable"
                    : !inSaloon
                        ? "route_to_saloon_required"
                        : !menuClear || Game1.eventUp || player.UsingTool || !player.CanMove
                            ? "blocked_player_busy"
                            : interactions.Length == 0
                                ? "blocked_prairie_king_interaction_tile_unavailable"
                                : "ready";
        var activeState = active is null ? null : ReadActivePrairieKing(active);
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "prairie_king.v1",
            saloon_available = saloon is not null,
            completed,
            completedWithoutDying,
            has_saved_progress = progress is not null,
            saved_round = progress?.whichRound.Value,
            saved_wave = progress?.whichWave.Value,
            saved_died = progress?.died.Value,
            interactions,
            activeState
        }));

        return new
        {
            schema_version = "prairie_king.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            gate_status = gateStatus,
            invocation_policy = "autonomous_timed_equivalent",
            native_proxy_policy = "post_core_training_player_command_only",
            location_id = "Saloon",
            is_current_location = inSaloon,
            player_can_move = player.CanMove,
            player_using_tool = player.UsingTool,
            dialogue_up = Game1.dialogueUp,
            event_up = Game1.eventUp,
            active_menu_type = Game1.activeClickableMenu?.GetType().Name ?? "none",
            completed_before = completed,
            completed_without_dying_before = completedWithoutDying,
            completion_goal = "complete_without_dying",
            has_saved_progress = progress is not null,
            saved_progress = progress is null
                ? null
                : new
                {
                    round = progress.whichRound.Value,
                    wave = progress.whichWave.Value,
                    world = progress.world.Value,
                    lives = progress.lives.Value,
                    score = progress.score.Value,
                    died = progress.died.Value
                },
            dialogue_key = progress is null ? "none" : "CowboyGame",
            dialogue_response_key = progress is null ? "none" : "NewGame",
            equivalent_duration_ticks = 108000,
            equivalent_acceleration = 60,
            equivalent_time_policy = "conservative_30_minute_session_budget",
            native_completion_trigger = "AbigailGame.usePowerup(-3)",
            interaction_tiles = interactions,
            active_session = activeState,
            equivalent_contract = PrairieKingEquivalentContract
        };
    }

    private static object[] ReadPrairieKingInteractionTiles(GameLocation saloon)
    {
        var layer = saloon.Map?.GetLayer("Buildings");
        if (layer is null)
            return Array.Empty<object>();
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = saloon.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action, "Arcade_Prairie", StringComparison.Ordinal))
                continue;
            result.Add(new
            {
                tile_x = x,
                tile_y = y,
                action_raw = action,
                action_token = "Arcade_Prairie"
            });
        }
        return result.ToArray();
    }

    private static object ReadActivePrairieKing(AbigailGame game) => new
    {
        runtime_identity = RuntimeHelpers.GetHashCode(game).ToString("X8"),
        minigame_id = game.minigameId(),
        playing_with_abigail = AbigailGame.playingWithAbigail,
        on_start_menu = AbigailGame.onStartMenu,
        game_over = AbigailGame.gameOver,
        end_cutscene = AbigailGame.endCutscene,
        end_cutscene_phase = AbigailGame.endCutscenePhase,
        end_cutscene_timer = AbigailGame.endCutsceneTimer,
        round = game.whichRound,
        wave = AbigailGame.whichWave,
        world = AbigailGame.world,
        lives = game.lives,
        coins = game.coins,
        score = game.score,
        died = game.died,
        wave_timer = AbigailGame.waveTimer
    };
}
