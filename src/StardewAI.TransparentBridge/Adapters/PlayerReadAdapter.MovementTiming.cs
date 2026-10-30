using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object? ReadMovementTimingContext(Farmer? player)
    {
        if (player is null)
            return null;

        var permanentBookSpeed =
            (player.stats.Get("Book_Speed") != 0 ? 0.25f : 0f) +
            (player.stats.Get("Book_Speed2") != 0 ? 0.25f : 0f);
        var buffSpeed = player.buffs.Speed;
        var temporarySpeed = player.temporarySpeedBuff;
        const float minimumVanillaTerrainTemporarySpeedBuff = -3f;
        var currentNativeOnFootSpeedScalar = Math.Max(
            1f,
            player.Speed + player.addedSpeed + temporarySpeed);
        var conservativeOnFootSpeedScalar = Math.Max(
            1f,
            player.Speed + player.addedSpeed +
                minimumVanillaTerrainTemporarySpeedBuff);
        var immobilized = player.hasBuff("19");
        var eventControlsMovement = Game1.CurrentEvent is not null || Game1.eventUp;
        var nativeControlGateOpen =
            !Game1.globalFade &&
            !Game1.freezeControls &&
            !Game1.isWarping &&
            !Game1.paused &&
            !Game1.dialogueUp &&
            !eventControlsMovement &&
            player.freezePause <= 0;
        var formulaApplicable =
            !immobilized &&
            !eventControlsMovement &&
            conservativeOnFootSpeedScalar > 0f;
        var theoreticalUpperBound = formulaApplicable
            ? Game1.tileSize /
                (conservativeOnFootSpeedScalar * 0.066d) /
                Game1.realMilliSecondsPerGameMinute
            : (double?)null;

        return new
        {
            projection_status = "exact_current_player_native_cardinal_movement_context",
            base_speed_now = player.Speed,
            added_speed_now = player.addedSpeed,
            buff_speed_now = buffSpeed,
            temporary_speed_buff_now = temporarySpeed,
            current_native_on_foot_speed_scalar =
                currentNativeOnFootSpeedScalar,
            minimum_vanilla_terrain_temporary_speed_buff =
                minimumVanillaTerrainTemporarySpeedBuff,
            permanent_book_speed = permanentBookSpeed,
            riding_horse_now = player.isRidingHorse(),
            immobilizing_buff_19_active = immobilized,
            current_event_active = Game1.CurrentEvent is not null,
            event_up = Game1.eventUp,
            game_paused = Game1.paused,
            dialogue_up = Game1.dialogueUp,
            global_fade = Game1.globalFade,
            freeze_controls = Game1.freezeControls,
            is_warping = Game1.isWarping,
            player_freeze_pause = player.freezePause,
            native_control_gate_open = nativeControlGateOpen,
            player_can_move_now = player.CanMove,
            player_using_tool_now = player.UsingTool,
            tile_size_pixels = Game1.tileSize,
            real_milliseconds_per_game_minute = Game1.realMilliSecondsPerGameMinute,
            native_cardinal_movement_multiplier = 0.066d,
            conservative_on_foot_speed_scalar = conservativeOnFootSpeedScalar,
            theoretical_upper_bound_game_minutes_per_tile = theoreticalUpperBound,
            runtime_calibration_compatible =
                formulaApplicable &&
                theoreticalUpperBound.HasValue &&
                theoreticalUpperBound.Value <= 1d,
            route_timing_ready_now =
                formulaApplicable &&
                nativeControlGateOpen &&
                player.CanMove &&
                !player.UsingTool,
            scope = "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
            source = "Farmer.getMovementSpeed cardinal non-event branch; Farmer.addedSpeed; HoeDirt.doCollisionAction; Grass.doCollisionAction; Game1.realMilliSecondsPerGameMinute"
        };
    }
}
