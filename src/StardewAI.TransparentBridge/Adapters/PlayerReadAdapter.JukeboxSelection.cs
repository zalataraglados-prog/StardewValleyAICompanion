using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string JukeboxSelectionNativeContract =
        "Saloon_Jukebox_checkAction->ChooseFromListMenu(default_index_0)->receiveLeftClick_forward_exact_index->receiveLeftClick_ok->Game1_default_music_request_receipt->receiveLeftClick_cancel";

    private static object ReadJukeboxSelection(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new
            {
                schema_version = "jukebox_selection.v1",
                projection_status = "unavailable_world_or_player",
                tracks = Array.Empty<object>(),
                action_tiles = Array.Empty<object>()
            };
        }

        var saloon = Game1.getLocationFromName("Saloon");
        var actionTiles = ReadJukeboxActionTiles(saloon);
        var trackIds = Utility.GetJukeboxTracks(player, saloon ?? player.currentLocation).ToArray();
        var greenRainOverride = Game1.IsGreenRainingHere() &&
            !Game1.currentLocation.InIslandContext() && Game1.IsRainingHere(Game1.currentLocation);
        var tracks = trackIds.Select((trackId, index) => new
        {
            track_id = trackId,
            track_index = index,
            display_name = Utility.getSongTitleFromCueName(trackId),
            unlock_source = Game1.jukeboxTrackData.TryGetValue(trackId, out var data) && data.Available == true
                ? "data_always_available"
                : "player_songs_heard_or_canonical_alternative",
            selectable_now = !greenRainOverride || string.Equals(trackId, "rain", StringComparison.Ordinal),
            gate_status = !greenRainOverride || string.Equals(trackId, "rain", StringComparison.Ordinal)
                ? "ready"
                : "blocked_green_rain_native_changeMusicTrack_guard"
        }).Cast<object>().ToArray();
        var projectionBody = new
        {
            trackIds,
            actionTiles,
            greenRainOverride,
            defaultMusicTrack = Game1.getMusicTrackName()
        };

        return new
        {
            schema_version = "jukebox_selection.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = JukeboxSelectionSha256(JsonSerializer.Serialize(projectionBody)),
            invocation_policy = "player_command_only",
            training_policy = "excluded_from_autonomous_candidates_and_strategy_training",
            location_id = "Saloon",
            is_current_location = ReferenceEquals(Game1.currentLocation, saloon),
            service_status = saloon is null
                ? "blocked_saloon_missing"
                : actionTiles.Length == 0
                    ? "blocked_jukebox_action_missing"
                    : ReferenceEquals(Game1.currentLocation, saloon) ? "ready" : "route_to_saloon_required",
            green_rain_native_override_active = greenRainOverride,
            default_music_track = Game1.getMusicTrackName(),
            requested_music_track = Game1.requestedMusicTrack ?? string.Empty,
            current_song_name = Game1.currentSong?.Name ?? string.Empty,
            unlocked_track_count = trackIds.Length,
            songs_heard_count = player.songsHeard.Count,
            tracks,
            action_tiles = actionTiles,
            active_menu_status = Game1.activeClickableMenu is ChooseFromListMenu
                ? ReferenceEquals(Game1.currentLocation, saloon) ? "choose_from_list_in_saloon" : "other_choose_from_list_menu"
                : "none",
            native_contract = JukeboxSelectionNativeContract,
            native_catalog_policy = "Utility.GetJukeboxTracks_exact_order;Data_JukeboxTracks_available_true_plus_valid_non_disabled_player_songsHeard_with_alternative_track_canonicalization",
            mini_jukebox_boundary = "not_in_scope;no_turn_off_random_or_location_miniJukeboxTrack_mutation",
            direct_mutation_policy = "production_executor_must_not_call_changeMusicTrack_or_write_music_state_directly"
        };
    }

    private static object[] ReadJukeboxActionTiles(GameLocation? saloon)
    {
        var buildings = saloon?.map?.GetLayer("Buildings");
        if (saloon is null || buildings is null)
            return Array.Empty<object>();
        var rows = new List<object>();
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            var action = saloon.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (string.Equals(action, "Jukebox", StringComparison.Ordinal))
                rows.Add(new { tile_x = x, tile_y = y, action_raw = action, action_token = "Jukebox" });
        }
        return rows.ToArray();
    }

    private static string JukeboxSelectionSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
