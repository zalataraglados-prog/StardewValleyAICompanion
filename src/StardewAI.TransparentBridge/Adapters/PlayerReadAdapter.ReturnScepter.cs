using System.Text.Json;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string ReturnScepterNativeContract =
        "Farmer.BeginUsingTool->Tool.beginUsing(InstantUse)->Game1.toolAnimationDone->Wand.DoFunction->1000ms_wandWarpForReal->Utility.getHomeOfFarmer(player).getFrontDoorSpot->Game1.warpFarmer(Farm)";

    private static object ReadReturnScepterContext(Farmer? player)
    {
        if (player?.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        FarmHouse? home = null;
        try
        {
            home = Utility.getHomeOfFarmer(player);
        }
        catch
        {
            // A stale or mod-removed home location must be projected as unavailable, not crash the bridge.
        }

        var frontDoor = home?.getFrontDoorSpot();
        var alreadyAtDestination = frontDoor.HasValue &&
            string.Equals(location.NameOrUniqueName, "Farm", StringComparison.Ordinal) &&
            player.TilePoint == frontDoor.Value;
        var rows = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(Wand) &&
                string.Equals(entry.item.QualifiedItemId, "(T)ReturnScepter", StringComparison.Ordinal))
            .Select(entry => new
            {
                inventory_slot_index = entry.slot,
                item_id = entry.item!.ItemId,
                qualified_item_id = entry.item.QualifiedItemId,
                display_name = entry.item.DisplayName,
                inventory_runtime_type = entry.item.GetType().FullName,
                stack_before = entry.item.Stack,
                stack_after = entry.item.Stack,
                reusable_tool = true,
                instant_use = ((Wand)entry.item).InstantUse,
                play_use_sounds = ((Wand)entry.item).PlayUseSounds
            })
            .ToArray();
        var exactScepterAvailable = rows.Any(row => row.stack_before == 1 && row.instant_use);
        var nativeWandGate = !player.bathingClothes.Value && !player.onBridge.Value;
        var executorBaseGate = player.canMove && !player.UsingTool && Game1.activeClickableMenu is null &&
            !Game1.dialogueUp && Game1.currentMinigame is null && !Game1.eventUp && !Game1.fadeToBlack &&
            !player.swimming.Value && !player.IsSitting() && !player.isRidingHorse() && !player.canOnlyWalk;
        var gateStatus = rows.Length == 0 ? "blocked_no_inventory_return_scepter" :
            !exactScepterAvailable ? "blocked_noncanonical_return_scepter" :
            home is null || !frontDoor.HasValue ? "blocked_home_unavailable" :
            player.bathingClothes.Value ? "blocked_bathing_clothes" :
            player.onBridge.Value ? "blocked_on_bridge" :
            !executorBaseGate ? "blocked_executor_base_gate" :
            alreadyAtDestination ? "blocked_already_at_destination" :
            "ready";
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "return_scepter.v1",
            current_location_id = location.NameOrUniqueName,
            current_tile = player.TilePoint,
            home_location_id = player.homeLocation.Value,
            home_runtime_type = home?.GetType().Name,
            front_door = frontDoor,
            home_is_cabin = home is Cabin,
            alreadyAtDestination,
            nativeWandGate,
            executorBaseGate,
            rows
        }));

        return new
        {
            schema_version = "return_scepter.v1",
            projection_status = home is null
                ? "partial_home_location_unavailable"
                : "complete_current_native_return_scepter_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = gateStatus,
            native_wand_gate = new
            {
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                passed = nativeWandGate
            },
            executor_base_gate = new
            {
                can_move = player.canMove,
                using_tool = player.UsingTool,
                active_menu_clear = Game1.activeClickableMenu is null,
                dialogue_clear = !Game1.dialogueUp,
                minigame_clear = Game1.currentMinigame is null,
                event_up = Game1.eventUp,
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                sitting = player.IsSitting(),
                riding_horse = player.isRidingHorse(),
                can_only_walk = player.canOnlyWalk,
                passed = executorBaseGate
            },
            destination = new
            {
                home_location_id = player.homeLocation.Value,
                home_runtime_type = home?.GetType().Name ?? string.Empty,
                destination_location_id = "Farm",
                front_door_tile_x = frontDoor?.X,
                front_door_tile_y = frontDoor?.Y,
                home_is_cabin = home is Cabin,
                already_at_destination = alreadyAtDestination
            },
            animation_contract = new
            {
                instant_use = true,
                facing_direction = 2,
                callback_delay_ms = 1000,
                freeze_pause_ms = 2000,
                poof_sprite_count = 12,
                poof_delay_domain_ms = "random_int[25,74]",
                poof_position_domain_pixels = "player_xy+[-256,191]",
                trail_sprite_count = 17,
                trail_delay_step_ms = 25,
                trail_max_delay_ms = 400,
                sound = "wand",
                sound_condition = "selected_wand.PlayUseSounds"
            },
            state_transition_contract = new
            {
                display_farmer_during_warp = false,
                temporarily_invincible_during_warp = true,
                temporary_invincibility_timer_ms = -2000,
                wand_sets_can_move_false_before_return = true,
                instant_tool_base_restores_can_move_after_do_function = true,
                effective_input_hold_uses_freeze_pause_ms = 2000,
                callback_fade_to_black_alpha = 0.99f,
                callback_restores_display_farmer = true,
                callback_clears_temporary_invincibility = true,
                callback_sets_can_move_true = true
            },
            native_contract = ReturnScepterNativeContract,
            rows
        };
    }
}
