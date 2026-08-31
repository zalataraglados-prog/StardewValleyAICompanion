using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class MovieTheaterProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "movie_theater.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("theater_unlocked")]
    public bool TheaterUnlocked { get; set; }

    [JsonPropertyName("festival_day")]
    public bool FestivalDay { get; set; }

    [JsonPropertyName("time_of_day")]
    public int TimeOfDay { get; set; }

    [JsonPropertyName("total_week")]
    public int TotalWeek { get; set; }

    [JsonPropertyName("player_last_seen_movie_week")]
    public int PlayerLastSeenMovieWeek { get; set; }

    [JsonPropertyName("player_watched_this_week")]
    public bool PlayerWatchedThisWeek { get; set; }

    [JsonPropertyName("movie_ticket_count")]
    public int MovieTicketCount { get; set; }

    [JsonPropertyName("movie_id")]
    public string MovieId { get; set; } = string.Empty;

    [JsonPropertyName("movie_title")]
    public string MovieTitle { get; set; } = string.Empty;

    [JsonPropertyName("movie_tags")]
    public string[] MovieTags { get; set; } = Array.Empty<string>();

    [JsonPropertyName("current_location_id")]
    public string CurrentLocationId { get; set; } = string.Empty;

    [JsonPropertyName("theater_state")]
    public int TheaterState { get; set; }

    [JsonPropertyName("showing_id")]
    public int ShowingId { get; set; }

    [JsonPropertyName("movie_mutex_locked")]
    public bool MovieMutexLocked { get; set; }

    [JsonPropertyName("movie_mutex_held_by_local_player")]
    public bool MovieMutexHeldByLocalPlayer { get; set; }

    [JsonPropertyName("screening_event_active")]
    public bool ScreeningEventActive { get; set; }

    [JsonPropertyName("active_event_id")]
    public string ActiveEventId { get; set; } = string.Empty;

    [JsonPropertyName("current_invitation")]
    public MovieInvitationRef? CurrentInvitation { get; set; }

    [JsonPropertyName("guest_options")]
    public MovieGuestOptionRef[] GuestOptions { get; set; } = Array.Empty<MovieGuestOptionRef>();

    [JsonPropertyName("entrance_action_tiles")]
    public MovieActionTileRef[] EntranceActionTiles { get; set; } = Array.Empty<MovieActionTileRef>();

    [JsonPropertyName("concession_action_tiles")]
    public MovieActionTileRef[] ConcessionActionTiles { get; set; } = Array.Empty<MovieActionTileRef>();

    [JsonPropertyName("screening_door_action_tiles")]
    public MovieActionTileRef[] ScreeningDoorActionTiles { get; set; } = Array.Empty<MovieActionTileRef>();

    [JsonPropertyName("service_status")]
    public string ServiceStatus { get; set; } = "unavailable";

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = Array.Empty<string>();
}

public sealed class MovieInvitationRef
{
    [JsonPropertyName("farmer_id")]
    public long FarmerId { get; set; }

    [JsonPropertyName("guest_name")]
    public string GuestName { get; set; } = string.Empty;

    [JsonPropertyName("fulfilled")]
    public bool Fulfilled { get; set; }

    [JsonPropertyName("purchased_concession_id")]
    public string PurchasedConcessionId { get; set; } = string.Empty;
}

public sealed class MovieGuestOptionRef
{
    [JsonPropertyName("guest_name")]
    public string GuestName { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("movie_response")]
    public string MovieResponse { get; set; } = string.Empty;

    [JsonPropertyName("movie_friendship_base")]
    public int MovieFriendshipBase { get; set; }

    [JsonPropertyName("movie_friendship_effective")]
    public int MovieFriendshipEffective { get; set; }

    [JsonPropertyName("friendship_points_before")]
    public int FriendshipPointsBefore { get; set; }

    [JsonPropertyName("last_seen_movie_week")]
    public int LastSeenMovieWeek { get; set; }

    [JsonPropertyName("can_invite_now")]
    public bool CanInviteNow { get; set; }

    [JsonPropertyName("option_fingerprint")]
    public string OptionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("blocked_reasons")]
    public string[] BlockedReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("concessions")]
    public MovieConcessionOptionRef[] Concessions { get; set; } = Array.Empty<MovieConcessionOptionRef>();
}

public sealed class MovieConcessionOptionRef
{
    [JsonPropertyName("concession_id")]
    public string ConcessionId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public int Price { get; set; }

    [JsonPropertyName("taste")]
    public string Taste { get; set; } = string.Empty;

    [JsonPropertyName("friendship_base")]
    public int FriendshipBase { get; set; }

    [JsonPropertyName("friendship_effective")]
    public int FriendshipEffective { get; set; }

    [JsonPropertyName("option_fingerprint")]
    public string OptionFingerprint { get; set; } = string.Empty;
}

public sealed class MovieActionTileRef
{
    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("action_raw")]
    public string ActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("action_token")]
    public string ActionToken { get; set; } = string.Empty;

    [JsonPropertyName("stand_tiles")]
    public MovieStandTileRef[] StandTiles { get; set; } = Array.Empty<MovieStandTileRef>();
}

public sealed class MovieStandTileRef
{
    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("map_passable")]
    public bool MapPassable { get; set; }

    [JsonPropertyName("occupied")]
    public bool Occupied { get; set; }

    [JsonPropertyName("path_reachable")]
    public bool? PathReachable { get; set; }

    [JsonPropertyName("path_length")]
    public int? PathLength { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }
}
