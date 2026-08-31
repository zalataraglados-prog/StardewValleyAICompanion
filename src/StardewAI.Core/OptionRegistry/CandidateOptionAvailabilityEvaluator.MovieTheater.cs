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
    private const string MovieAloneGuest = "__alone__";

    private EventCandidate[] MovieTheaterCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "movie_theater");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();
        var row = projection.Value;
        if (ReadString(row, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(row, "invocation_policy") != "autonomous_social_value_with_explicit_alone_variant" ||
            ReadBool(row, "theater_unlocked") != true || ReadBool(row, "festival_day") == true ||
            ReadBool(row, "player_watched_this_week") == true || string.IsNullOrWhiteSpace(ReadString(row, "movie_id")) ||
            ReadInt(row, "time_of_day") is < 900 or > 2100)
            return Array.Empty<EventCandidate>();

        var objectives = MovieObjectives(row, boundParameters);
        if (objectives.Length == 0)
            return Array.Empty<EventCandidate>();

        return objectives
            .SelectMany(objective => MovieObjectiveStageCandidates(snapshot, row, objective))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private EventCandidate[] MovieObjectiveStageCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        MovieObjective objective)
    {
        var invitation = projection.TryGetProperty("current_invitation", out var invitationRow) &&
            invitationRow.ValueKind == JsonValueKind.Object
                ? invitationRow
                : (JsonElement?)null;
        var invitationGuest = invitation.HasValue ? ReadString(invitation.Value, "guest_name") : string.Empty;
        if (invitation.HasValue &&
            (objective.GuestName == MovieAloneGuest ||
             !string.Equals(invitationGuest, objective.GuestName, StringComparison.Ordinal)))
            return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var alreadyInsideTheater = string.Equals(currentLocation, "MovieTheater", StringComparison.OrdinalIgnoreCase);
        var neededTickets = alreadyInsideTheater
            ? 0
            : invitation.HasValue || objective.GuestName == MovieAloneGuest ? 1 : 2;
        if (ReadInt(projection, "movie_ticket_count") < neededTickets)
            return MovieTicketPurchaseCandidates(snapshot, objective);

        if (!invitation.HasValue && objective.GuestName != MovieAloneGuest)
        {
            if (!objective.GuestCanInvite)
                return Array.Empty<EventCandidate>();
            if (!string.Equals(currentLocation, objective.GuestLocation, StringComparison.OrdinalIgnoreCase))
                return MovieRouteCandidates(snapshot, currentLocation, objective.GuestLocation, objective, "guest");
            var stand = FindBestStandTile(snapshot, objective.GuestTileX, objective.GuestTileY);
            if (stand is null)
                return Array.Empty<EventCandidate>();
            var ticketSlot = ReadMovieTicketSlots(projection).FirstOrDefault();
            if (ticketSlot is null)
                return Array.Empty<EventCandidate>();
            return new[]
            {
                MovieStageCandidate(
                    objective,
                    "watch_movie_invite_guest",
                    currentLocation,
                    objective.GuestTileX,
                    objective.GuestTileY,
                    stand,
                    600,
                    "native_movie_ticket_invitation_created=true;fresh_snapshot_replan_required=true",
                    new[]
                    {
                        Parameter("movie_ticket_slot_index", ticketSlot.SlotIndex.ToString(CultureInfo.InvariantCulture)),
                        Parameter("movie_ticket_stack_before", ticketSlot.Stack.ToString(CultureInfo.InvariantCulture))
                    })
            };
        }

        if (!string.Equals(currentLocation, "Town", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentLocation, "MovieTheater", StringComparison.OrdinalIgnoreCase))
            return MovieRouteCandidates(snapshot, currentLocation, "Town", objective, "theater");

        if (string.Equals(currentLocation, "Town", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = ReadMovieActionTiles(projection, "entrance_action_tiles").FirstOrDefault();
            if (endpoint is null)
                return Array.Empty<EventCandidate>();
            var stand = FindMovieEndpointStandTile(snapshot, endpoint);
            if (stand is null)
                return Array.Empty<EventCandidate>();
            return new[]
            {
                MovieStageCandidate(objective, "watch_movie_enter", "Town", endpoint.X, endpoint.Y,
                    stand, 900, "native_movie_ticket_consumed=true;location=MovieTheater;fresh_snapshot_replan_required=true",
                    new[] { Parameter("movie_action_raw", endpoint.ActionRaw), Parameter("movie_action_token", endpoint.ActionToken) })
            };
        }

        if (objective.GuestName != MovieAloneGuest && invitation.HasValue &&
            ReadBool(invitation.Value, "fulfilled") != true)
        {
            return new[]
            {
                MovieStageCandidate(objective, "watch_movie_wait_guest", "MovieTheater", null, null, null, 120,
                    "native_invited_guest_spawn_and_fulfillment_polled=true;fresh_snapshot_replan_required=true",
                    new[] { Parameter("retry_wait_ticks", "120") })
            };
        }

        var purchasedConcession = invitation.HasValue
            ? ReadString(invitation.Value, "purchased_concession_id")
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(objective.ConcessionId) &&
            !string.Equals(purchasedConcession, objective.ConcessionId, StringComparison.Ordinal))
        {
            var endpoint = ReadMovieActionTiles(projection, "concession_action_tiles").FirstOrDefault();
            if (endpoint is null)
                return Array.Empty<EventCandidate>();
            var stand = FindMovieEndpointStandTile(snapshot, endpoint);
            if (stand is null)
                return Array.Empty<EventCandidate>();
            return new[]
            {
                MovieStageCandidate(objective, "watch_movie_concession", "MovieTheater", endpoint.X, endpoint.Y,
                    stand, 600, "native_movie_concession_purchased=true;fresh_snapshot_replan_required=true",
                    new[] { Parameter("movie_action_raw", endpoint.ActionRaw), Parameter("movie_action_token", endpoint.ActionToken) })
            };
        }

        var doors = ReadMovieActionTiles(projection, "screening_door_action_tiles").FirstOrDefault();
        if (doors is null || ReadBool(projection, "movie_mutex_locked") == true && ReadBool(projection, "movie_mutex_held_by_local_player") != true)
            return Array.Empty<EventCandidate>();
        var doorStand = FindMovieEndpointStandTile(snapshot, doors);
        if (doorStand is null)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            MovieStageCandidate(objective, "watch_movie_screening", "MovieTheater", doors.X, doors.Y,
                doorStand, 7200, "native_movie_screening_completed=true;player_last_seen_movie_week_updated=true;friendship_receipt_verified=true",
                new[] { Parameter("movie_action_raw", doors.ActionRaw), Parameter("movie_action_token", doors.ActionToken) })
        };
    }

    private EventCandidate[] MovieTicketPurchaseCandidates(SnapshotEnvelope snapshot, MovieObjective objective)
    {
        var purchase = BuySupplyStageCandidates(snapshot, new[]
        {
            Parameter("continuation.shop_id", "BoxOffice"),
            Parameter("continuation.qualified_item_id", "(O)809"),
            Parameter("continuation.max_unit_price", "1000")
        });
        var continuation = MovieContinuationParameters(objective);
        return purchase.Select(candidate => CloneCandidate(
                candidate,
                candidateId: "movie:" + objective.ObjectiveKey + ":ticket:" + candidate.CandidateId,
                expectedEffect: candidate.ExpectedEffect + ";movie_ticket_acquired_for_bound_movie_objective=true",
                parameters: candidate.Parameters
                    .Where(parameter => !parameter.Name.StartsWith("continuation.", StringComparison.Ordinal))
                    .Concat(continuation)
                    .Concat(new[]
                    {
                        Parameter("continuation.shop_id", "BoxOffice"),
                        Parameter("continuation.qualified_item_id", "(O)809"),
                        Parameter("continuation.max_unit_price", "1000"),
                        Parameter("continuation.quantity", "1")
                    })
                    .ToArray(),
                availabilityClass: "movie_ticket_" + candidate.AvailabilityClass))
            .ToArray();
    }

    private EventCandidate[] MovieRouteCandidates(
        SnapshotEnvelope snapshot,
        string currentLocation,
        string targetLocation,
        MovieObjective objective,
        string routePurpose)
    {
        var plan = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (plan?.FirstConnectorCandidate is not { } connector)
            return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(connector,
                candidateId: "movie:" + objective.ObjectiveKey + ":route-" + routePurpose + ":" + connector.CandidateId,
                expectedEffect: connector.ExpectedEffect + ";movie_objective_retained=true;fresh_snapshot_replan_required=true",
                parameters: connector.Parameters.Concat(MovieContinuationParameters(objective)).ToArray(),
                availabilityClass: "movie_" + routePurpose + "_rolling_route")
        };
    }

    private static EventCandidate MovieStageCandidate(
        MovieObjective objective,
        string kind,
        string location,
        int? targetX,
        int? targetY,
        CandidateTile? stand,
        int ticks,
        string effect,
        IEnumerable<SmallModelActionParameter> stageParameters)
    {
        var parameters = new List<SmallModelActionParameter>(MovieContinuationParameters(objective))
        {
            Parameter("movie_stage", kind),
            Parameter("movie_projection_fingerprint", objective.ProjectionFingerprint),
            Parameter("native_contract", objective.NativeContract),
            Parameter("max_movement_tiles", "512")
        };
        if (stand is not null)
        {
            parameters.Add(Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)));
            parameters.Add(Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)));
        }
        parameters.AddRange(stageParameters);
        return new EventCandidate
        {
            CandidateId = "movie:" + objective.ObjectiveKey + ":" + kind,
            Kind = kind,
            Available = true,
            AllowedNow = true,
            AllowedToday = true,
            LocationId = location,
            TileX = targetX,
            TileY = targetY,
            EstimatedTicks = ticks,
            EnergyCost = 0,
            AvailabilityClass = "native_movie_objective_stage",
            ExpectedEffect = effect,
            Parameters = parameters.ToArray()
        };
    }

    private static MovieObjective[] MovieObjectives(JsonElement projection, SmallModelActionParameter[] boundParameters)
    {
        var movieId = ReadString(projection, "movie_id");
        var boundMovie = ReadParameter(boundParameters, "continuation.movie_id");
        var boundGuest = ReadParameter(boundParameters, "continuation.movie_guest_name");
        var boundConcession = ReadParameter(boundParameters, "continuation.movie_concession_id");
        if (!string.IsNullOrWhiteSpace(boundMovie) && !string.Equals(boundMovie, movieId, StringComparison.Ordinal))
            return Array.Empty<MovieObjective>();

        var common = new
        {
            ProjectionFingerprint = ReadString(projection, "projection_fingerprint"),
            NativeContract = ReadString(projection, "native_contract")
        };
        var results = new List<MovieObjective>
        {
            new(movieId, MovieAloneGuest, string.Empty, 0, 0, true, string.Empty, 0, 0,
                common.ProjectionFingerprint, common.NativeContract)
        };
        if (projection.TryGetProperty("guest_options", out var guests) && guests.ValueKind == JsonValueKind.Array)
        {
            foreach (var guest in guests.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.Object))
            {
                var blocked = MovieReadStringArray(guest, "blocked_reasons")
                    .Where(reason => reason != "two_movie_tickets_required_for_new_guest_objective")
                    .ToArray();
                if (blocked.Length > 0)
                    continue;
                var guestName = ReadString(guest, "guest_name");
                results.Add(new MovieObjective(movieId, guestName, ReadString(guest, "location_id"),
                    ReadInt(guest, "tile_x"), ReadInt(guest, "tile_y"), true, string.Empty,
                    ReadInt(guest, "movie_friendship_effective"), 0, common.ProjectionFingerprint, common.NativeContract));
                if (!guest.TryGetProperty("concessions", out var concessions) || concessions.ValueKind != JsonValueKind.Array)
                    continue;
                results.AddRange(concessions.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.Object)
                    .Select(concession => new MovieObjective(movieId, guestName, ReadString(guest, "location_id"),
                        ReadInt(guest, "tile_x"), ReadInt(guest, "tile_y"), true,
                        ReadString(concession, "concession_id"), ReadInt(guest, "movie_friendship_effective"),
                        ReadInt(concession, "friendship_effective"), common.ProjectionFingerprint, common.NativeContract)));
            }
        }

        return results
            .Where(objective => string.IsNullOrWhiteSpace(boundGuest) ||
                string.Equals(objective.GuestName, boundGuest, StringComparison.Ordinal))
            .Where(objective => string.IsNullOrWhiteSpace(boundGuest) ||
                string.Equals(objective.ConcessionId, boundConcession, StringComparison.Ordinal))
            .GroupBy(objective => objective.ObjectiveKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static SmallModelActionParameter[] MovieContinuationParameters(MovieObjective objective) =>
        new[]
        {
            Parameter("continuation.option_id", "social.watch_movie"),
            Parameter("continuation.movie_id", objective.MovieId),
            Parameter("continuation.movie_guest_name", objective.GuestName),
            Parameter("continuation.movie_concession_id", objective.ConcessionId),
            Parameter("continuation.movie_objective_key", objective.ObjectiveKey),
            Parameter("continuation.movie_friendship_effective", objective.MovieFriendship.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.movie_concession_friendship_effective", objective.ConcessionFriendship.ToString(CultureInfo.InvariantCulture))
        };

    private static MovieActionTile?[] ReadMovieActionTiles(JsonElement projection, string propertyName)
    {
        if (!projection.TryGetProperty(propertyName, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<MovieActionTile?>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => (MovieActionTile?)new MovieActionTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"),
                ReadString(row, "action_raw"), ReadString(row, "action_token"), ReadMovieStandTiles(row)))
            .ToArray();
    }

    private static MovieStandTile[] ReadMovieStandTiles(JsonElement endpoint) =>
        endpoint.TryGetProperty("stand_tiles", out var rows) && rows.ValueKind == JsonValueKind.Array
            ? rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => new MovieStandTile(
                    ReadInt(row, "tile_x"),
                    ReadInt(row, "tile_y"),
                    ReadBool(row, "available") == true,
                    ReadBool(row, "path_reachable") == true,
                    ReadInt(row, "path_length", int.MaxValue)))
                .ToArray()
            : Array.Empty<MovieStandTile>();

    private static CandidateTile? FindMovieEndpointStandTile(SnapshotEnvelope snapshot, MovieActionTile endpoint) =>
        endpoint.StandTiles
            .Where(tile => tile.Available && tile.PathReachable &&
                Math.Abs(endpoint.X - tile.X) + Math.Abs(endpoint.Y - tile.Y) == 1 &&
                !CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
            .OrderBy(tile => tile.PathLength)
            .ThenBy(tile => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - tile.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - tile.Y))
            .Select(tile => new CandidateTile(tile.X, tile.Y))
            .FirstOrDefault();

    private static MovieTicketSlot?[] ReadMovieTicketSlots(JsonElement projection)
    {
        if (!projection.TryGetProperty("movie_ticket_slots", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<MovieTicketSlot?>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => (MovieTicketSlot?)new MovieTicketSlot(ReadInt(row, "slot_index"), ReadInt(row, "stack")))
            .ToArray();
    }

    private static string[] MovieReadStringArray(JsonElement row, string propertyName) =>
        row.TryGetProperty(propertyName, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

    private sealed record MovieObjective(
        string MovieId,
        string GuestName,
        string GuestLocation,
        int GuestTileX,
        int GuestTileY,
        bool GuestCanInvite,
        string ConcessionId,
        int MovieFriendship,
        int ConcessionFriendship,
        string ProjectionFingerprint,
        string NativeContract)
    {
        public string ObjectiveKey => MovieId + ":" + GuestName + ":" +
            (string.IsNullOrWhiteSpace(ConcessionId) ? "none" : ConcessionId);
    }

    private sealed record MovieActionTile(
        int X,
        int Y,
        string ActionRaw,
        string ActionToken,
        MovieStandTile[] StandTiles);
    private sealed record MovieStandTile(int X, int Y, bool Available, bool PathReachable, int PathLength);
    private sealed record MovieTicketSlot(int SlotIndex, int Stack);
}
