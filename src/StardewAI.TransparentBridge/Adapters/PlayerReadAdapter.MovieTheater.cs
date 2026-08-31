using System.Reflection;
using System.Text.Json;
using Netcode;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly FieldInfo? MovieTheaterCurrentStateField = PrivateField<MovieTheater>("currentState");
    private static readonly FieldInfo? MovieTheaterShowingIdField = PrivateField<MovieTheater>("showingId");

    private const string MovieTheaterNativeContract =
        "NPC_ticket_native_invite_then_Town_Theater_Entrance_yes_then_optional_MovieTheater_Concessions_ShopMenu_then_Theater_Doors_mutex_ready_native_MovieTheaterScreening_event_and_week_friendship_receipt";

    private static object ReadMovieTheaterContext(Farmer? player)
    {
        if (player is null)
            return new { schema_version = "movie_theater.v1", projection_status = "unavailable_world_or_player" };

        var theater = Game1.getLocationFromName("MovieTheater") as MovieTheater;
        var town = Game1.getLocationFromName("Town");
        var unlocked = Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater");
        var movie = theater is null ? null : MovieTheater.GetMovieToday();
        var totalWeek = Game1.Date.TotalWeeks;
        var ticketSlots = player.Items
            .Select((item, index) => new { item, index })
            .Where(row => string.Equals(row.item?.QualifiedItemId, "(O)809", StringComparison.Ordinal))
            .Select(row => new { slot_index = row.index, stack = row.item!.Stack })
            .ToArray();
        var ticketCount = ticketSlots.Sum(row => row.stack);
        var invitations = player.team.movieInvitations
            .Where(invitation => invitation?.farmer is not null && invitation.invitedNPC is not null)
            .Select(invitation => new
            {
                farmer_id = invitation.farmer.UniqueMultiplayerID,
                farmer_name = invitation.farmer.Name,
                guest_name = invitation.invitedNPC.Name,
                fulfilled = invitation.fulfilled
            })
            .OrderBy(row => row.farmer_id)
            .ThenBy(row => row.guest_name, StringComparer.Ordinal)
            .ToArray();
        var ownInvitation = player.team.movieInvitations.FirstOrDefault(invitation =>
            invitation?.farmer == player && invitation.invitedNPC is not null);
        var purchasedConcessionId = string.Empty;
        if (theater is not null && ownInvitation?.invitedNPC is { } invitedNpc)
        {
            purchasedConcessionId = theater.GetConcessionsDictionary()
                .FirstOrDefault(pair => string.Equals(pair.Key.Name, invitedNpc.Name, StringComparison.Ordinal))
                .Value?.Id ?? string.Empty;
        }

        var invitedNpcsByName = player.team.movieInvitations
            .Select(invitation => invitation?.invitedNPC)
            .Where(npc => npc is not null && !string.IsNullOrWhiteSpace(npc.Name))
            .GroupBy(npc => npc!.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First()!, StringComparer.Ordinal);
        var guestOptions = movie is null
            ? Array.Empty<object>()
            : NpcReadAdapter.CollectAllLoadedNpcs()
                .Where(npc => npc is not null)
                .GroupBy(npc => npc.Name, StringComparer.Ordinal)
                .Select(group => invitedNpcsByName.TryGetValue(group.Key, out var invitedNpc)
                    ? invitedNpc
                    : Game1.getCharacterFromName(group.Key) ?? group.First())
                .Select(npc => ReadMovieGuestOption(player, npc, movie.Id, unlocked, totalWeek, ticketCount))
                .OrderBy(row => (string)(row.GetType().GetProperty("guest_name")?.GetValue(row) ?? string.Empty), StringComparer.Ordinal)
                .Cast<object>()
                .ToArray();
        var entranceTiles = ReadMovieActionTiles(player, town, "Theater_Entrance");
        var concessionTiles = ReadMovieActionTiles(player, theater, "Concessions");
        var doorTiles = ReadMovieActionTiles(player, theater, "Theater_Doors");
        var activeEventId = Game1.CurrentEvent?.id ?? string.Empty;
        var screeningActive = string.Equals(activeEventId, "MovieTheaterScreening", StringComparison.Ordinal);
        var blocked = new List<string>();
        if (!unlocked) blocked.Add("movie_theater_not_unlocked");
        if (movie is null) blocked.Add("movie_today_unavailable");
        if (theater is null || town is null) blocked.Add("movie_theater_locations_unavailable");
        if (Game1.isFestival()) blocked.Add("movie_theater_closed_for_festival");
        if (Game1.timeOfDay < 900 || Game1.timeOfDay > 2100) blocked.Add("movie_theater_closed_time_range_0900_2100");
        if (player.lastSeenMovieWeek.Value >= totalWeek) blocked.Add("player_already_watched_movie_this_week");
        if (entranceTiles.Length == 0) blocked.Add("movie_theater_entrance_endpoint_unavailable");
        if (doorTiles.Length == 0) blocked.Add("movie_theater_screening_door_endpoint_unavailable");

        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "movie_theater.v1",
            unlocked,
            date = Game1.Date.TotalDays,
            totalWeek,
            Game1.timeOfDay,
            festival = Game1.isFestival(),
            player_id = player.UniqueMultiplayerID,
            player_location = player.currentLocation?.NameOrUniqueName,
            player_last_seen = player.lastSeenMovieWeek.Value,
            ticketSlots,
            movie = movie is null ? null : new { movie.Id, movie.Tags, movie.YearModulus, movie.YearRemainder },
            invitations,
            own_invitation = ownInvitation is null ? null : new { guest = ownInvitation.invitedNPC?.Name, ownInvitation.fulfilled, purchasedConcessionId },
            theater_state = ReadMovieTheaterNetInt(theater, MovieTheaterCurrentStateField),
            showing_id = ReadMovieTheaterNetInt(theater, MovieTheaterShowingIdField),
            mutex_locked = player.team.movieMutex.IsLocked(),
            mutex_held = player.team.movieMutex.IsLockHeld(),
            activeEventId,
            guestOptions,
            entranceTiles,
            concessionTiles,
            doorTiles
        }));

        return new
        {
            schema_version = "movie_theater.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            invocation_policy = "autonomous_social_value_with_explicit_alone_variant",
            native_contract = MovieTheaterNativeContract,
            theater_unlocked = unlocked,
            festival_day = Game1.isFestival(),
            time_of_day = Game1.timeOfDay,
            open_time = 900,
            close_time = 2100,
            total_week = totalWeek,
            player_last_seen_movie_week = player.lastSeenMovieWeek.Value,
            player_watched_this_week = player.lastSeenMovieWeek.Value >= totalWeek,
            movie_ticket_qualified_item_id = "(O)809",
            movie_ticket_unit_price = 1000,
            movie_ticket_count = ticketCount,
            movie_ticket_slots = ticketSlots,
            movie_id = movie?.Id ?? string.Empty,
            movie_title = movie is null ? string.Empty : StardewValley.TokenizableStrings.TokenParser.ParseText(movie.Title),
            movie_tags = movie?.Tags?.OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
            current_location_id = player.currentLocation?.NameOrUniqueName ?? string.Empty,
            theater_state = ReadMovieTheaterNetInt(theater, MovieTheaterCurrentStateField),
            showing_id = ReadMovieTheaterNetInt(theater, MovieTheaterShowingIdField),
            movie_mutex_locked = player.team.movieMutex.IsLocked(),
            movie_mutex_held_by_local_player = player.team.movieMutex.IsLockHeld(),
            screening_event_active = screeningActive,
            active_event_id = activeEventId,
            current_invitation = ownInvitation is null ? null : new
            {
                farmer_id = ownInvitation.farmer.UniqueMultiplayerID,
                guest_name = ownInvitation.invitedNPC?.Name ?? string.Empty,
                fulfilled = ownInvitation.fulfilled,
                purchased_concession_id = purchasedConcessionId
            },
            all_invitations = invitations,
            guest_options = guestOptions,
            entrance_action_tiles = entranceTiles,
            concession_action_tiles = concessionTiles,
            screening_door_action_tiles = doorTiles,
            service_status = blocked.Count == 0 ? "ready" : "blocked",
            blocked_diagnostics = blocked.Distinct(StringComparer.Ordinal).ToArray(),
            ticket_acquisition_policy = "reuse_economy.buy_supplies_BoxOffice_(O)809_one_ticket_per_fresh_snapshot",
            screening_policy = "native_event_must_run_to_requestMovieEnd;skipEvent_does_not_apply_friendship_commands"
        };
    }

    private static object ReadMovieGuestOption(
        Farmer player,
        NPC npc,
        string movieId,
        bool theaterUnlocked,
        int totalWeek,
        int ticketCount)
    {
        var reasons = new List<string>();
        var friendship = player.friendshipData.TryGetValue(npc.Name, out var row) ? row : null;
        if (!theaterUnlocked) reasons.Add("movie_theater_not_unlocked");
        if (!NpcReadAdapter.SupportsVanillaSocialQueries(npc)) reasons.Add("movie_guest_runtime_type_or_override_not_locked_base");
        if (npc.SpeaksDwarvish() && !player.canUnderstandDwarves) reasons.Add("movie_guest_language_gate");
        if (string.Equals(npc.Name, "Leo", StringComparison.Ordinal) && !Game1.MasterPlayer.mailReceived.Contains("leoMoved")) reasons.Add("movie_guest_leo_not_moved");
        if (string.Equals(npc.Name, "Krobus", StringComparison.Ordinal) && Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Fri") reasons.Add("movie_guest_krobus_friday");
        if (!npc.IsVillager || !npc.CanSocialize) reasons.Add("movie_guest_not_socializable_villager");
        if (friendship is null) reasons.Add("movie_guest_friendship_row_missing");
        else if (friendship.IsDivorced()) reasons.Add("movie_guest_divorced");
        if (player.lastSeenMovieWeek.Value >= totalWeek) reasons.Add("player_already_watched_movie_this_week");
        if (Game1.isFestival()) reasons.Add("movie_theater_closed_for_festival");
        if (Game1.timeOfDay > 2100) reasons.Add("movie_theater_closed_after_2100");
        if (ticketCount < 2) reasons.Add("two_movie_tickets_required_for_new_guest_objective");

        foreach (var invitation in player.team.movieInvitations)
        {
            if (invitation.farmer == player) reasons.Add("player_already_invited_movie_guest");
            if (invitation.invitedNPC == npc) reasons.Add("movie_guest_already_invited_by_someone");
        }
        if (npc.lastSeenMovieWeek.Value >= totalWeek) reasons.Add("movie_guest_already_watched_this_week");
        var response = MovieTheater.GetResponseForMovie(npc) ?? string.Empty;
        if (response == "reject") reasons.Add("movie_guest_rejects_current_movie");
        var movieBase = response switch { "love" => 200, "like" => 100, _ => 0 };
        var movieEffective = EffectiveMovieFriendshipDelta(player, npc, movieBase, friendship?.Points ?? 0);
        var concessions = MovieTheater.GetConcessionsForGuest(npc.Name)
            .Select(item =>
            {
                var taste = MovieTheater.GetConcessionTasteForCharacter(npc, item);
                var baseDelta = taste switch { "love" => 50, "like" => 25, _ => 0 };
                var effective = EffectiveMovieFriendshipDelta(
                    player,
                    npc,
                    baseDelta,
                    (friendship?.Points ?? 0) + movieEffective);
                var optionFingerprint = Sha256(JsonSerializer.Serialize(new
                {
                    movieId,
                    guest = npc.Name,
                    concession = item.Id,
                    price = item.salePrice(),
                    taste,
                    baseDelta,
                    effective
                }));
                return new
                {
                    concession_id = item.Id,
                    qualified_item_id = item.QualifiedItemId,
                    display_name = item.DisplayName,
                    price = item.salePrice(),
                    taste,
                    friendship_base = baseDelta,
                    friendship_effective = effective,
                    option_fingerprint = optionFingerprint
                };
            })
            .OrderByDescending(item => item.friendship_effective)
            .ThenBy(item => item.price)
            .ThenBy(item => item.concession_id, StringComparer.Ordinal)
            .ToArray();
        var optionFingerprint = Sha256(JsonSerializer.Serialize(new
        {
            movieId,
            guest = npc.Name,
            location = npc.currentLocation?.NameOrUniqueName,
            tile = npc.TilePoint,
            response,
            movieBase,
            movieEffective,
            friendship_before = friendship?.Points,
            npc_last_seen = npc.lastSeenMovieWeek.Value,
            reasons,
            concessions
        }));
        return new
        {
            guest_name = npc.Name,
            display_name = npc.displayName,
            runtime_type = npc.GetType().FullName,
            current_instance_loaded = npc.currentLocation?.characters.Any(candidate => ReferenceEquals(candidate, npc)) == true,
            location_id = npc.currentLocation?.NameOrUniqueName ?? string.Empty,
            tile_x = npc.TilePoint.X,
            tile_y = npc.TilePoint.Y,
            movie_response = response,
            movie_friendship_base = movieBase,
            movie_friendship_effective = movieEffective,
            friendship_points_before = friendship?.Points ?? 0,
            last_seen_movie_week = npc.lastSeenMovieWeek.Value,
            can_invite_now = reasons.Count == 0,
            option_fingerprint = optionFingerprint,
            blocked_reasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            concessions
        };
    }

    private static int ReadMovieTheaterNetInt(MovieTheater? theater, FieldInfo? field) =>
        theater is not null && field?.GetValue(theater) is NetInt value ? value.Value : -1;

    private static int EffectiveMovieFriendshipDelta(Farmer player, NPC npc, int amount, int before)
    {
        if (amount <= 0 || npc.isDivorcedFrom(player) || (npc.SpeaksDwarvish() && !player.canUnderstandDwarves))
            return 0;
        if (player.stats.Get("Book_Friendship") != 0) amount = (int)(amount * 1.1f);
        if (npc.Equals(player.getSpouse())) amount = (int)(amount * 0.66f);
        var cap = (Utility.GetMaximumHeartsForCharacter(npc) + 1) * NPC.friendshipPointsPerHeartLevel - 1;
        return Math.Max(0, Math.Min(before + amount, cap) - before);
    }

    private static object[] ReadMovieActionTiles(Farmer player, GameLocation? location, string actionToken)
    {
        var layer = location?.Map?.GetLayer("Buildings");
        if (location is null || layer is null) return Array.Empty<object>();
        var reachableDistances = ReadMovieReachableTileDistances(
            player, location, layer.LayerWidth, layer.LayerHeight);
        var result = new List<object>();
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var raw = location.doesTileHaveProperty(x, y, "Action", "Buildings");
            var token = raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.Equals(token, actionToken, StringComparison.Ordinal)) continue;
            var standTiles = new[]
                {
                    new { x = x, y = y - 1 },
                    new { x = x + 1, y },
                    new { x = x, y = y + 1 },
                    new { x = x - 1, y }
                }
                .Where(tile => tile.x >= 0 && tile.y >= 0 && tile.x < layer.LayerWidth && tile.y < layer.LayerHeight)
                .Select(tile =>
                {
                    var mapPassable = location.isTilePassable(
                        new xTile.Dimensions.Location(tile.x, tile.y), Game1.viewport);
                    var playerAlreadyStandingHere = ReferenceEquals(player.currentLocation, location) &&
                        player.TilePoint.X == tile.x && player.TilePoint.Y == tile.y;
                    var occupied = !playerAlreadyStandingHere &&
                        (location.IsTileOccupiedBy(new Microsoft.Xna.Framework.Vector2(tile.x, tile.y)) ||
                         location.characters.Any(character => character.TilePoint.X == tile.x && character.TilePoint.Y == tile.y));
                    int? pathLength = reachableDistances is not null &&
                        reachableDistances.TryGetValue(tile.y * layer.LayerWidth + tile.x, out var distance)
                            ? distance
                            : null;
                    bool? pathReachable = reachableDistances is null ? null : pathLength.HasValue;
                    return new
                    {
                        tile_x = tile.x,
                        tile_y = tile.y,
                        map_passable = mapPassable,
                        occupied,
                        path_reachable = pathReachable,
                        path_length = pathLength,
                        available = mapPassable && !occupied && pathReachable == true
                    };
                })
                .ToArray();
            result.Add(new
            {
                location_id = location.NameOrUniqueName,
                tile_x = x,
                tile_y = y,
                action_raw = raw ?? string.Empty,
                action_token = token,
                stand_tiles = standTiles
            });
        }
        return result.OrderBy(row => (int)(row.GetType().GetProperty("tile_y")?.GetValue(row) ?? 0))
            .ThenBy(row => (int)(row.GetType().GetProperty("tile_x")?.GetValue(row) ?? 0))
            .ToArray();
    }

    private static Dictionary<int, int>? ReadMovieReachableTileDistances(
        Farmer player,
        GameLocation location,
        int width,
        int height)
    {
        if (!ReferenceEquals(player.currentLocation, location) || width <= 0 || height <= 0)
            return null;

        var start = player.TilePoint;
        var startKey = start.Y * width + start.X;
        var distances = new Dictionary<int, int> { [startKey] = 0 };
        var queue = new Queue<Microsoft.Xna.Framework.Point>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentDistance = distances[current.Y * width + current.X];
            if (currentDistance >= 512) continue;
            foreach (var next in new[]
                     {
                         new Microsoft.Xna.Framework.Point(current.X + 1, current.Y),
                         new Microsoft.Xna.Framework.Point(current.X - 1, current.Y),
                         new Microsoft.Xna.Framework.Point(current.X, current.Y + 1),
                         new Microsoft.Xna.Framework.Point(current.X, current.Y - 1)
                     })
            {
                if (next.X < 0 || next.Y < 0 || next.X >= width || next.Y >= height) continue;
                var key = next.Y * width + next.X;
                if (distances.ContainsKey(key)) continue;
                var rectangle = new Microsoft.Xna.Framework.Rectangle(
                    next.X * Game1.tileSize + 1,
                    next.Y * Game1.tileSize + 1,
                    Game1.tileSize - 2,
                    Game1.tileSize - 2);
                if (location.isCollidingPosition(
                        rectangle, Game1.viewport, isFarmer: true, 0, glider: false, player, pathfinding: true) ||
                    location.characters.Any(character => character.GetBoundingBox().Intersects(rectangle)))
                    continue;
                distances[key] = currentDistance + 1;
                queue.Enqueue(next);
            }
        }
        return distances;
    }
}
