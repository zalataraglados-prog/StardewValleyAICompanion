using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] JukeboxSelectionCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var trackId = JukeboxIntent(intent, "jukebox_track_id");
        var reason = JukeboxIntent(intent, "jukebox_reason");
        if (string.IsNullOrWhiteSpace(trackId) || string.IsNullOrWhiteSpace(reason) ||
            JukeboxIntent(intent, "confirm_jukebox_track") != "true")
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "player", "jukebox_selection");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only")
            return Array.Empty<EventCandidate>();
        var track = FindJukeboxTrack(projection.Value, trackId);
        if (!track.HasValue || ReadBool(track.Value, "selectable_now") != true)
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(projection.Value, "location_id");
        if (!string.Equals(currentLocation, targetLocation, StringComparison.OrdinalIgnoreCase))
            return JukeboxRouteCandidates(snapshot, projection.Value, trackId, reason, currentLocation, targetLocation);

        var endpoint = JukeboxActionTiles(projection.Value)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(row => row.stand is not null)
            .OrderBy(row => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - row.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - row.stand!.Y))
            .FirstOrDefault();
        var reasons = new List<string>();
        if (ReadString(projection.Value, "service_status") != "ready")
            reasons.Add("jukebox_selection_service_not_ready:" + ReadString(projection.Value, "service_status"));
        if (endpoint is null)
            reasons.Add("jukebox_selection_has_no_reachable_stand");
        var parameters = endpoint is null
            ? Array.Empty<SmallModelActionParameter>()
            : JukeboxCandidateParameters(projection.Value, track.Value, trackId, reason, endpoint.tile, endpoint.stand!);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "player.choose_jukebox_track",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "jukebox-selection:" + ReadInt(track.Value, "track_index") + ":" +
                    (ReadString(projection.Value, "projection_fingerprint") is { Length: >= 12 } fingerprint
                        ? fingerprint[..12] : "invalid"),
                Kind = "choose_jukebox_track",
                Available = reasons.Count == 0,
                AllowedNow = reasons.Count == 0,
                AllowedToday = reasons.Count == 0,
                LocationId = targetLocation,
                TileX = endpoint?.tile.X,
                TileY = endpoint?.tile.Y,
                EstimatedTicks = 420 + Math.Max(0, ReadInt(track.Value, "track_index")) * 2,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_saloon_jukebox",
                ExpectedEffect = "default_music_track=" + trackId + ";native_menu_receipt_verified=true",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private EventCandidate[] JukeboxRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        string trackId,
        string reason,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(projection, "service_status") != "route_to_saloon_required")
            return Array.Empty<EventCandidate>();
        var track = FindJukeboxTrack(projection, trackId);
        if (!track.HasValue || ReadBool(track.Value, "selectable_now") != true)
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null)
            return Array.Empty<EventCandidate>();
        var continuation = new[]
        {
            Parameter("continuation.option_id", "player.choose_jukebox_track"),
            Parameter("continuation.jukebox_track_id", trackId),
            Parameter("continuation.jukebox_reason", reason),
            Parameter("continuation.confirm_jukebox_track", "true")
        };
        return new[]
        {
            CloneCandidate(route.FirstConnectorCandidate,
                candidateId: "jukebox-selection-route:" + ReadInt(track.Value, "track_index") + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";jukebox_track_continuation=" + trackId,
                parameters: route.FirstConnectorCandidate.Parameters.Concat(continuation).ToArray(),
                availabilityClass: "jukebox_selection_player_command_rolling_route")
        };
    }

    private static SmallModelActionParameter[] JukeboxCandidateParameters(
        JsonElement projection,
        JsonElement track,
        string trackId,
        string reason,
        JukeboxActionTile tile,
        CandidateTile stand) => new[]
    {
        Parameter("jukebox_track_id", trackId),
        Parameter("jukebox_reason", reason),
        Parameter("confirm_jukebox_track", "true"),
        Parameter("jukebox_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
        Parameter("jukebox_track_index", ReadInt(track, "track_index").ToString(CultureInfo.InvariantCulture)),
        Parameter("jukebox_unlocked_track_count", ReadInt(projection, "unlocked_track_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("jukebox_default_track_before", ReadString(projection, "default_music_track")),
        Parameter("jukebox_requested_track_before", ReadString(projection, "requested_music_track")),
        Parameter("jukebox_current_song_before", ReadString(projection, "current_song_name")),
        Parameter("jukebox_green_rain_override", (ReadBool(projection, "green_rain_native_override_active") == true).ToString().ToLowerInvariant()),
        Parameter("target_location", ReadString(projection, "location_id")),
        Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("jukebox_action_raw", tile.ActionRaw),
        Parameter("expected_menu_type_after", "ChooseFromListMenu"),
        Parameter("expected_menu_kind", "jukebox"),
        Parameter("native_contract", ReadString(projection, "native_contract")),
        Parameter("max_movement_tiles", "512")
    };

    private static JsonElement? FindJukeboxTrack(JsonElement projection, string trackId)
    {
        if (!projection.TryGetProperty("tracks", out var tracks) || tracks.ValueKind != JsonValueKind.Array)
            return null;
        var row = tracks.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "track_id"), trackId, StringComparison.Ordinal));
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static JukeboxActionTile[] JukeboxActionTiles(JsonElement projection) =>
        projection.TryGetProperty("action_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => new JukeboxActionTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"),
                    ReadString(row, "action_raw"))).ToArray()
            : Array.Empty<JukeboxActionTile>();

    private static string JukeboxIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    private sealed record JukeboxActionTile(int X, int Y, string ActionRaw);
}
