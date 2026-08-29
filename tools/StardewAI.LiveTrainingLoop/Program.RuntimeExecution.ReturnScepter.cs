using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyReturnScepterRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_return_scepter", StringComparison.Ordinal))
            return;
        request.LocationId = ReadQueueParameterString(item, "source_location_id");
        request.ReturnScepterProjectionFingerprint = ReadQueueParameterString(item, "return_scepter_projection_fingerprint");
        request.ReturnScepterHomeLocationId = ReadQueueParameterString(item, "home_location_id");
        request.ReturnScepterHomeRuntimeType = ReadQueueParameterString(item, "home_runtime_type");
        request.ReturnScepterDestinationLocationId = ReadQueueParameterString(item, "destination_location_id");
        request.ReturnScepterFrontDoorTileX = ReadQueueParameterInt(item, "front_door_tile_x");
        request.ReturnScepterFrontDoorTileY = ReadQueueParameterInt(item, "front_door_tile_y");
        request.ReturnScepterHomeIsCabin = ReadQueueParameterBool(item, "home_is_cabin");
        request.ReturnScepterAlreadyAtDestination = ReadQueueParameterBool(item, "already_at_destination");
        request.ReturnScepterInstantUse = ReadQueueParameterBool(item, "native_instant_use");
        request.ReturnScepterFacingDirection = ReadQueueParameterInt(item, "native_facing_direction");
        request.ReturnScepterCallbackDelayMs = ReadQueueParameterInt(item, "native_callback_delay_ms");
        request.ReturnScepterFreezePauseMs = ReadQueueParameterInt(item, "native_freeze_pause_ms");
        request.ReturnScepterPoofSpriteCount = ReadQueueParameterInt(item, "native_poof_sprite_count");
        request.ReturnScepterTrailSpriteCount = ReadQueueParameterInt(item, "native_trail_sprite_count");
        request.ReturnScepterTrailDelayStepMs = ReadQueueParameterInt(item, "native_trail_delay_step_ms");
        request.ReturnScepterTrailMaxDelayMs = ReadQueueParameterInt(item, "native_trail_max_delay_ms");
        request.ReturnScepterSound = ReadQueueParameterString(item, "native_sound");
    }
}
