using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyJukeboxSelectionRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.choose_jukebox_track" or "player.choose_jukebox_track" or
            "debug.setup_jukebox_selection"))
            return;
        request.JukeboxTrackId = ReadQueueParameterString(item, "jukebox_track_id");
        request.JukeboxReason = ReadQueueParameterString(item, "jukebox_reason");
        request.ConfirmJukeboxTrack = ReadQueueParameterBool(item, "confirm_jukebox_track");
        request.JukeboxProjectionFingerprint = ReadQueueParameterString(item, "jukebox_projection_fingerprint");
        request.JukeboxTrackIndex = ReadQueueParameterInt(item, "jukebox_track_index");
        request.JukeboxUnlockedTrackCount = ReadQueueParameterInt(item, "jukebox_unlocked_track_count");
        request.JukeboxDefaultTrackBefore = ReadQueueParameterString(item, "jukebox_default_track_before");
        request.JukeboxRequestedTrackBefore = ReadQueueParameterString(item, "jukebox_requested_track_before");
        request.JukeboxCurrentSongBefore = ReadQueueParameterString(item, "jukebox_current_song_before");
        request.JukeboxGreenRainOverride = ReadQueueParameterBool(item, "jukebox_green_rain_override");
        request.JukeboxActionRaw = ReadQueueParameterString(item, "jukebox_action_raw");
    }
}
