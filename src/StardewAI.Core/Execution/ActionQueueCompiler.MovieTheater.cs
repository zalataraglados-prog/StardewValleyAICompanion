using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string CompilerMovieTheaterNativeContract =
        "NPC_ticket_native_invite_then_Town_Theater_Entrance_yes_then_optional_MovieTheater_Concessions_ShopMenu_then_Theater_Doors_mutex_ready_native_MovieTheaterScreening_event_and_week_friendship_receipt";

    private static CompiledActionStep[] CompileWatchMovieStep(SmallModelAction action, SnapshotEnvelope _)
    {
        var stage = ReadParameter(action, "movie_stage");
        if (stage is not ("watch_movie_invite_guest" or "watch_movie_enter" or
            "watch_movie_concession" or "watch_movie_screening"))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "watch_movie",
                "MovieTheater:stage=" + stage + ":movie=" + ReadParameter(action, "continuation.movie_id") +
                ":guest=" + ReadParameter(action, "continuation.movie_guest_name") +
                ":concession=" + (ReadParameter(action, "continuation.movie_concession_id") is { Length: > 0 } concession ? concession : "none"),
                stage == "watch_movie_screening"
                    ? "native_movie_screening_completed=true;player_last_seen_movie_week_updated=true;friendship_receipt_verified=true"
                    : "movie_stage_completed=true;fresh_snapshot_replan_required=true",
                stage == "watch_movie_screening" ? 7200 : 900)
        };
    }

    private static string[] ValidateWatchMoviePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.watch_movie")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var projection = ReadStateFieldValue(snapshot, "player", "movie_theater");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "movie_theater_projection_unavailable" };
        var row = projection.Value;
        var stage = ReadParameter(action, "movie_stage");
        var movieId = ReadParameter(action, "continuation.movie_id") ?? string.Empty;
        var guest = ReadParameter(action, "continuation.movie_guest_name") ?? string.Empty;
        var concession = ReadParameter(action, "continuation.movie_concession_id") ?? string.Empty;
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");

        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "projection_fingerprint") != ReadParameter(action, "movie_projection_fingerprint") ||
            ReadString(row, "native_contract") != CompilerMovieTheaterNativeContract ||
            ReadParameter(action, "native_contract") != CompilerMovieTheaterNativeContract ||
            ReadString(row, "movie_id") != movieId || string.IsNullOrWhiteSpace(guest) ||
            ReadBool(row, "festival_day") == true || ReadBool(row, "player_watched_this_week") == true ||
            ReadInt(row, "time_of_day") is < 900 or > 2100)
            reasons.Add("movie_theater_projection_drifted_or_closed");
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("movie_theater_menu_must_be_clear_before_stage");
        if (!x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(x.Value - standX.Value) + Math.Abs(y.Value - standY.Value) != 1)
            reasons.Add("movie_theater_exact_adjacent_endpoint_required");

        if (stage == "watch_movie_invite_guest")
        {
            if (guest == "__alone__" || ReadStateFieldString(snapshot, "player", "location_id") != ReadParameter(action, "target_location") ||
                !MovieGuestMatches(row, guest, x, y) || ReadIntParameter(action, "movie_ticket_slot_index") is not { } slot ||
                !MovieTicketSlotMatches(row, slot, ReadIntParameter(action, "movie_ticket_stack_before")))
                reasons.Add("movie_guest_or_ticket_projection_drifted");
        }
        else if (stage == "watch_movie_enter")
        {
            if (ReadStateFieldString(snapshot, "player", "location_id") != "Town" ||
                !MovieActionTileMatches(row, "entrance_action_tiles", x, y, "Theater_Entrance") ||
                !MovieActionStandMatches(row, "entrance_action_tiles", x, y, standX, standY) ||
                ReadInt(row, "movie_ticket_count") < 1)
                reasons.Add("movie_theater_entrance_or_ticket_projection_drifted");
        }
        else if (stage == "watch_movie_concession")
        {
            if (guest == "__alone__" || string.IsNullOrWhiteSpace(concession) ||
                ReadStateFieldString(snapshot, "player", "location_id") != "MovieTheater" ||
                !MovieActionTileMatches(row, "concession_action_tiles", x, y, "Concessions") ||
                !MovieActionStandMatches(row, "concession_action_tiles", x, y, standX, standY) ||
                !MovieConcessionMatches(row, guest, concession))
                reasons.Add("movie_concession_projection_drifted");
        }
        else if (stage == "watch_movie_screening")
        {
            if (ReadStateFieldString(snapshot, "player", "location_id") != "MovieTheater" ||
                !MovieActionTileMatches(row, "screening_door_action_tiles", x, y, "Theater_Doors") ||
                !MovieActionStandMatches(row, "screening_door_action_tiles", x, y, standX, standY) ||
                ReadBool(row, "movie_mutex_locked") == true && ReadBool(row, "movie_mutex_held_by_local_player") != true ||
                !MovieInvitationReady(row, guest, concession))
                reasons.Add("movie_screening_projection_not_ready");
        }
        else
        {
            reasons.Add("movie_stage_not_supported");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool MovieGuestMatches(JsonElement projection, string guest, int? x, int? y) =>
        projection.TryGetProperty("guest_options", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "guest_name") == guest && ReadBool(row, "can_invite_now") == true &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y);

    private static bool MovieTicketSlotMatches(JsonElement projection, int slot, int? stack) =>
        stack is > 0 && projection.TryGetProperty("movie_ticket_slots", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "slot_index") == slot && ReadInt(row, "stack") == stack);

    private static bool MovieActionTileMatches(JsonElement projection, string property, int? x, int? y, string token) =>
        projection.TryGetProperty(property, out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
            ReadInt(row, "tile_x") == x && ReadInt(row, "tile_y") == y &&
            ReadString(row, "action_token") == token && ReadString(row, "action_raw").StartsWith(token, StringComparison.Ordinal));

    private static bool MovieActionStandMatches(
        JsonElement projection,
        string property,
        int? endpointX,
        int? endpointY,
        int? standX,
        int? standY) =>
        projection.TryGetProperty(property, out var endpoints) && endpoints.ValueKind == JsonValueKind.Array &&
        endpoints.EnumerateArray().Any(endpoint => endpoint.ValueKind == JsonValueKind.Object &&
            ReadInt(endpoint, "tile_x") == endpointX && ReadInt(endpoint, "tile_y") == endpointY &&
            endpoint.TryGetProperty("stand_tiles", out var stands) && stands.ValueKind == JsonValueKind.Array &&
            stands.EnumerateArray().Any(stand => stand.ValueKind == JsonValueKind.Object &&
                ReadInt(stand, "tile_x") == standX && ReadInt(stand, "tile_y") == standY &&
                ReadBool(stand, "available") == true && ReadBool(stand, "path_reachable") == true));

    private static bool MovieConcessionMatches(JsonElement projection, string guest, string concession) =>
        projection.TryGetProperty("guest_options", out var rows) && rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object && ReadString(row, "guest_name") == guest &&
            row.TryGetProperty("concessions", out var concessions) && concessions.ValueKind == JsonValueKind.Array &&
            concessions.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object &&
                ReadString(item, "concession_id") == concession));

    private static bool MovieInvitationReady(JsonElement projection, string guest, string concession)
    {
        if (guest == "__alone__")
            return string.IsNullOrWhiteSpace(concession);
        if (!projection.TryGetProperty("current_invitation", out var invitation) || invitation.ValueKind != JsonValueKind.Object ||
            ReadString(invitation, "guest_name") != guest || ReadBool(invitation, "fulfilled") != true)
            return false;
        return string.IsNullOrWhiteSpace(concession) ||
            ReadString(invitation, "purchased_concession_id") == concession;
    }
}
