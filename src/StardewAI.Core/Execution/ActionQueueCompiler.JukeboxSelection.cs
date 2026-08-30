using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string JukeboxSelectionCompilerNativeContract =
        "Saloon_Jukebox_checkAction->ChooseFromListMenu(default_index_0)->receiveLeftClick_forward_exact_index->receiveLeftClick_ok->Game1_default_music_request_receipt->receiveLeftClick_cancel";

    private static readonly string[] JukeboxSelectionBoundParameterNames =
    {
        "jukebox_projection_fingerprint", "jukebox_track_index", "jukebox_unlocked_track_count",
        "jukebox_default_track_before", "jukebox_requested_track_before", "jukebox_current_song_before",
        "jukebox_green_rain_override", "target_location", "target_tile_x", "target_tile_y",
        "stand_tile_x", "stand_tile_y", "jukebox_action_raw", "expected_menu_type_after",
        "expected_menu_kind", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildJukeboxSelectionParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !JukeboxSelectionBoundParameterNames.Contains(parameter.Name, StringComparer.Ordinal))
            .ToList();
        var trackId = ReadParameter(action, "jukebox_track_id");
        var projection = ReadStateFieldValue(snapshot, "player", "jukebox_selection");
        if (string.IsNullOrWhiteSpace(trackId) || !projection.HasValue ||
            projection.Value.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var track = JukeboxCompilerTrack(projection.Value, trackId);
        var target = ResolveJukeboxCompilerTarget(projection.Value, action, snapshot);
        if (!track.HasValue || ReadBool(track.Value, "selectable_now") != true || target is null)
            return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("jukebox_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("jukebox_track_index", ReadInt(track.Value, "track_index").ToString(CultureInfo.InvariantCulture)),
            Parameter("jukebox_unlocked_track_count", ReadInt(projection.Value, "unlocked_track_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("jukebox_default_track_before", ReadString(projection.Value, "default_music_track")),
            Parameter("jukebox_requested_track_before", ReadString(projection.Value, "requested_music_track")),
            Parameter("jukebox_current_song_before", ReadString(projection.Value, "current_song_name")),
            Parameter("jukebox_green_rain_override", (ReadBool(projection.Value, "green_rain_native_override_active") == true).ToString().ToLowerInvariant()),
            Parameter("target_location", ReadString(projection.Value, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("jukebox_action_raw", target.ActionRaw),
            Parameter("expected_menu_type_after", "ChooseFromListMenu"),
            Parameter("expected_menu_kind", "jukebox"),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileJukeboxSelectionStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundJukeboxSelectionAction(action, snapshot);
        var trackId = ReadParameter(bound, "jukebox_track_id");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (string.IsNullOrWhiteSpace(trackId) || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("choose_jukebox_track",
                ReadParameter(bound, "target_location") + "(" + x + "," + y + "):track=" + trackId,
                "default_music_track=" + trackId + ";native_menu_receipt_verified=true",
                420 + Math.Max(0, ReadIntParameter(bound, "jukebox_track_index") ?? 0) * 2)
        };
    }

    private static string[] ValidateJukeboxSelectionPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("player.choose_jukebox_track" or "executor.choose_jukebox_track"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        var trackId = ReadParameter(action, "jukebox_track_id");
        if (string.IsNullOrWhiteSpace(trackId) || string.IsNullOrWhiteSpace(ReadParameter(action, "jukebox_reason")) ||
            ReadParameter(action, "confirm_jukebox_track") != "true")
            reasons.Add("jukebox_selection_exact_track_reason_and_confirmation_required");
        var projection = ReadStateFieldValue(snapshot, "player", "jukebox_selection");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("jukebox_selection_projection_unavailable").ToArray();
        var jukebox = projection.Value;
        var track = string.IsNullOrWhiteSpace(trackId) ? null : JukeboxCompilerTrack(jukebox, trackId);
        if (ReadString(jukebox, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(jukebox, "invocation_policy") != "player_command_only" ||
            ReadString(jukebox, "service_status") != "ready" ||
            !string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "Saloon", StringComparison.OrdinalIgnoreCase))
            reasons.Add("jukebox_selection_native_service_not_ready");
        if (!track.HasValue || ReadBool(track.Value, "selectable_now") != true)
            reasons.Add("jukebox_selection_track_locked_unknown_or_native_weather_blocked");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("jukebox_selection_menu_must_be_clear");

        var bound = BoundJukeboxSelectionAction(action, snapshot);
        var target = ResolveJukeboxCompilerTarget(jukebox, action, snapshot);
        if (target is null || !track.HasValue ||
            ReadParameter(bound, "jukebox_projection_fingerprint") != ReadString(jukebox, "projection_fingerprint") ||
            ReadIntParameter(bound, "jukebox_track_index") != ReadInt(track.Value, "track_index") ||
            ReadIntParameter(bound, "jukebox_unlocked_track_count") != ReadInt(jukebox, "unlocked_track_count") ||
            ReadParameter(bound, "jukebox_default_track_before") != ReadString(jukebox, "default_music_track") ||
            ReadParameter(bound, "jukebox_requested_track_before") != ReadString(jukebox, "requested_music_track") ||
            ReadParameter(bound, "jukebox_current_song_before") != ReadString(jukebox, "current_song_name") ||
            ReadParameter(bound, "jukebox_green_rain_override") !=
                (ReadBool(jukebox, "green_rain_native_override_active") == true).ToString().ToLowerInvariant() ||
            ReadIntParameter(bound, "target_tile_x") != target?.TargetX ||
            ReadIntParameter(bound, "target_tile_y") != target?.TargetY ||
            ReadIntParameter(bound, "stand_tile_x") != target?.StandX ||
            ReadIntParameter(bound, "stand_tile_y") != target?.StandY ||
            ReadParameter(bound, "jukebox_action_raw") != "Jukebox" ||
            ReadParameter(bound, "native_contract") != JukeboxSelectionCompilerNativeContract)
            reasons.Add("jukebox_selection_complete_fresh_typed_projection_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundJukeboxSelectionAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildJukeboxSelectionParameters(action, snapshot)
    };

    private static JsonElement? JukeboxCompilerTrack(JsonElement projection, string trackId)
    {
        if (!projection.TryGetProperty("tracks", out var tracks) || tracks.ValueKind != JsonValueKind.Array)
            return null;
        var row = tracks.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(value, "track_id"), trackId, StringComparison.Ordinal));
        return row.ValueKind == JsonValueKind.Object ? row : null;
    }

    private static JukeboxCompilerTarget? ResolveJukeboxCompilerTarget(
        JsonElement projection,
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
                ReadString(row, "action_raw") == "Jukebox")
            .Select(row =>
            {
                var x = ReadInt(row, "tile_x");
                var y = ReadInt(row, "tile_y");
                var requestedX = ReadIntParameter(action, "stand_tile_x");
                var requestedY = ReadIntParameter(action, "stand_tile_y");
                var stand = requestedX.HasValue && requestedY.HasValue &&
                    Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                    SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                        ? new SleepStandTile(requestedX.Value, requestedY.Value)
                        : FindBestSleepStandTile(snapshot, x, y);
                return stand is null ? null : new JukeboxCompilerTarget(x, y, stand.X, stand.Y,
                    ReadString(row, "action_raw"), Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y));
            })
            .Where(target => target is not null)
            .OrderBy(target => target!.Distance).ThenBy(target => target!.TargetY).ThenBy(target => target!.TargetX)
            .FirstOrDefault();
    }

    private sealed record JukeboxCompilerTarget(int TargetX, int TargetY, int StandX, int StandY, string ActionRaw, int Distance);
}
