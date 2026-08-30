using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("jukebox_track_id")]
    public string JukeboxTrackId { get; set; } = string.Empty;

    [JsonPropertyName("jukebox_reason")]
    public string JukeboxReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_jukebox_track")]
    public bool? ConfirmJukeboxTrack { get; set; }

    [JsonPropertyName("jukebox_projection_fingerprint")]
    public string JukeboxProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("jukebox_track_index")]
    public int? JukeboxTrackIndex { get; set; }

    [JsonPropertyName("jukebox_unlocked_track_count")]
    public int? JukeboxUnlockedTrackCount { get; set; }

    [JsonPropertyName("jukebox_default_track_before")]
    public string JukeboxDefaultTrackBefore { get; set; } = string.Empty;

    [JsonPropertyName("jukebox_requested_track_before")]
    public string JukeboxRequestedTrackBefore { get; set; } = string.Empty;

    [JsonPropertyName("jukebox_current_song_before")]
    public string JukeboxCurrentSongBefore { get; set; } = string.Empty;

    [JsonPropertyName("jukebox_green_rain_override")]
    public bool? JukeboxGreenRainOverride { get; set; }

    [JsonPropertyName("jukebox_action_raw")]
    public string JukeboxActionRaw { get; set; } = string.Empty;
}
