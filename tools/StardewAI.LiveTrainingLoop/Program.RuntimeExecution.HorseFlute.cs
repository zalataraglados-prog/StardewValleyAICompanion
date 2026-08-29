using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyHorseFluteRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.use_horse_flute", StringComparison.Ordinal))
            return;

        request.HorseWarpRestrictions = ReadQueueParameterInt(item, "horse_warp_restrictions");
        request.HorseWarpRestrictionNames = ReadQueueParameterString(item, "horse_warp_restriction_names");
        request.OwnedHorseId = ReadQueueParameterString(item, "owned_horse_id");
        request.OwnedHorseLocationId = ReadQueueParameterString(item, "owned_horse_location_id");
        request.OwnedHorseTileX = ReadQueueParameterInt(item, "owned_horse_tile_x");
        request.OwnedHorseTileY = ReadQueueParameterInt(item, "owned_horse_tile_y");
        request.OwnedHorseNearby = ReadQueueParameterBool(item, "owned_horse_nearby");
        request.TeamEventStableHorseId = ReadQueueParameterString(item, "team_event_stable_horse_id");
        request.TeamEventStableLocationId = ReadQueueParameterString(item, "team_event_stable_location_id");
        request.TeamEventStableTileX = ReadQueueParameterInt(item, "team_event_stable_tile_x");
        request.TeamEventStableTileY = ReadQueueParameterInt(item, "team_event_stable_tile_y");
        request.TeamEventStableMatchesOwnedHorse = ReadQueueParameterBool(item, "team_event_stable_matches_owned_horse");
        request.HorseFluteExpectedResult = ReadQueueParameterString(item, "expected_result");
        request.HorseFluteUseDelayMs = ReadQueueParameterInt(item, "use_delay_ms");
        request.HorseFluteFreezePauseMs = ReadQueueParameterInt(item, "freeze_pause_ms");
        request.HorseFluteMusicDuckMs = ReadQueueParameterInt(item, "music_duck_ms");
        request.HorseFluteExpectedFacingDirection = ReadQueueParameterInt(item, "facing_direction");
    }
}
