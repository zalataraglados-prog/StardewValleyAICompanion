using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyWarpTotemRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_warp_totem", StringComparison.Ordinal))
            return;
        request.WarpTotemProjectionFingerprint = ReadQueueParameterString(item, "warp_totem_projection_fingerprint");
        request.WarpTotemBaseDestinationLocationId = ReadQueueParameterString(item, "base_destination_location_id");
        request.WarpTotemRequestedDestinationTileX = ReadQueueParameterInt(item, "requested_destination_tile_x");
        request.WarpTotemRequestedDestinationTileY = ReadQueueParameterInt(item, "requested_destination_tile_y");
        request.WarpTotemEffectiveDestinationLocationId = ReadQueueParameterString(item, "effective_destination_location_id");
        request.WarpTotemEffectiveDestinationTileX = ReadQueueParameterInt(item, "effective_destination_tile_x");
        request.WarpTotemEffectiveDestinationTileY = ReadQueueParameterInt(item, "effective_destination_tile_y");
        request.WarpTotemDestinationRouteMode = ReadQueueParameterString(item, "destination_route_mode");
        request.WarpTotemFarmDestinationSource = ReadQueueParameterString(item, "farm_destination_source");
        request.WarpTotemPassiveFestivalRouteJson = ReadQueueParameterString(item, "passive_festival_route_json");
        request.WarpTotemActiveFestivalId = ReadQueueParameterString(item, "active_festival_id");
        request.WarpTotemActiveFestivalStartTime = ReadQueueParameterInt(item, "active_festival_start_time");
        request.WarpTotemActiveFestivalEndTime = ReadQueueParameterInt(item, "active_festival_end_time");
        request.WarpTotemActiveFestivalEntryTileX = ReadQueueParameterInt(item, "active_festival_entry_tile_x");
        request.WarpTotemActiveFestivalEntryTileY = ReadQueueParameterInt(item, "active_festival_entry_tile_y");
        request.WarpTotemActiveFestivalEntryFacing = ReadQueueParameterInt(item, "active_festival_entry_facing");
        request.WarpTotemFestivalPrestartWarpCancelled = ReadQueueParameterBool(item, "festival_prestart_warp_cancelled");
        request.WarpTotemFestivalReadyCheckRequired = ReadQueueParameterBool(item, "festival_ready_check_required");
        request.WarpTotemFacingDirection = ReadQueueParameterInt(item, "native_facing_direction");
        request.WarpTotemAnimationDurationMs = ReadQueueParameterInt(item, "native_animation_duration_ms");
        request.WarpTotemCallbackDelayMs = ReadQueueParameterInt(item, "native_totem_callback_delay_ms");
        request.WarpTotemInitialItemSpriteCount = ReadQueueParameterInt(item, "native_initial_item_sprite_count");
        request.WarpTotemSprinkleSpriteCount = ReadQueueParameterInt(item, "native_sprinkle_sprite_count");
        request.WarpTotemPoofSpriteCount = ReadQueueParameterInt(item, "native_poof_sprite_count");
        request.WarpTotemTrailSpriteCount = ReadQueueParameterInt(item, "native_trail_sprite_count");
        request.WarpTotemInitialSound = ReadQueueParameterString(item, "native_initial_sound");
        request.WarpTotemWarpSound = ReadQueueParameterString(item, "native_warp_sound");
        request.WarpTotemGlowColorRgba = ReadQueueParameterString(item, "native_glow_color_rgba");
    }
}
