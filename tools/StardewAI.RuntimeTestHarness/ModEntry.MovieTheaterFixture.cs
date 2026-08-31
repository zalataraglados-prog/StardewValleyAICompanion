using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMovieTheaterFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.currentMinigame = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        StopAllMovement();

        var town = Game1.getLocationFromName("Town");
        var theater = Game1.getLocationFromName("MovieTheater") as MovieTheater;
        var guest = Game1.getCharacterFromName("Abigail");
        if (town is null || theater is null || guest is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_movie_theater",
                "movie_theater_fixture=ready", "town_or_theater_or_guest=missing",
                "movie_theater_fixture_world_identity_unavailable");
        }

        Game1.MasterPlayer.mailReceived.Add("ccMovieTheater");
        town.MakeMapModifications(force: true);
        theater.MakeMapModifications(force: true);
        theater.ResetTheater();
        Game1.player.team.movieInvitations.Clear();
        Game1.timeOfDay = 1000;
        Game1.player.lastSeenMovieWeek.Set(Game1.Date.TotalWeeks - 1);
        guest.lastSeenMovieWeek.Set(Game1.Date.TotalWeeks - 1);
        var friendship = Game1.player.friendshipData.TryGetValue(guest.Name, out var existingFriendship)
            ? existingFriendship
            : Game1.player.friendshipData[guest.Name] = new Friendship();
        friendship.Clear();
        friendship.Points = 1000;
        friendship.Status = FriendshipStatus.Friendly;

        if (!TryFindMovieTheaterFixtureTiles(town, out var entrance, out var stand, out var guestTile))
        {
            return BlockedWithPrimitive(request, "debug_setup_movie_theater",
                "movie_theater_fixture=ready", "entrance_or_adjacent_tiles=missing",
                "movie_theater_fixture_tiles_unavailable");
        }

        guest.currentLocation?.characters.Remove(guest);
        foreach (var duplicate in town.characters
                     .Where(character => character.Name == guest.Name && !ReferenceEquals(character, guest))
                     .ToArray())
            town.characters.Remove(duplicate);
        guest.currentLocation = town;
        guest.setTileLocation(guestTile.ToVector2());
        if (!town.characters.Contains(guest))
            town.characters.Add(guest);

        Game1.currentLocation = town;
        Game1.player.currentLocation = town;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        Game1.player.Money = Math.Max(Game1.player.Money, 10000);
        for (var slot = 0; slot < Game1.player.Items.Count; slot++)
        {
            if (Game1.player.Items[slot]?.QualifiedItemId == "(O)809")
                Game1.player.Items[slot] = null;
        }
        var ticketSlot = -1;
        for (var slot = 0; slot < Game1.player.Items.Count; slot++)
        {
            if (Game1.player.Items[slot] is not null)
                continue;
            ticketSlot = slot;
            break;
        }
        if (ticketSlot < 0)
        {
            ticketSlot = Game1.player.Items.Count - 1;
            Game1.player.Items[ticketSlot] = null;
        }
        Game1.player.Items[ticketSlot] = ItemRegistry.Create("(O)809", 2);
        Game1.player.CurrentToolIndex = ticketSlot;

        var movie = MovieTheater.GetMovieToday();
        var verified = movie is not null && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater") &&
            ReferenceEquals(Game1.currentLocation, town) && Game1.player.TilePoint == stand &&
            ReferenceEquals(guest.currentLocation, town) && guest.TilePoint == guestTile &&
            CountInventoryItems("(O)809") == 2 && Game1.player.Money >= 10000 &&
            MovieTheater.GetResponseForMovie(guest) != "reject";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_movie_theater",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "fixture_only_theater_unlock_week_ticket_money_and_friendship_ready",
                    "fixture_only_Abigail_placed_adjacent_to_native_Theater_Entrance_stand",
                    "native_movie_guest_response_accepts_current_movie"
                }
                : new[] { "movie_theater_fixture_post_state_mismatch" },
            RequestedEffect = "movie_theater_fixture=ready;guest=Abigail;tickets=2;money>=10000",
            ObservedEffect = "movie_id=" + (movie?.Id ?? "none") +
                ";entrance=" + entrance.X + "," + entrance.Y +
                ";stand=" + stand.X + "," + stand.Y +
                ";guest_tile=" + guestTile.X + "," + guestTile.Y +
                ";tickets=" + CountInventoryItems("(O)809").ToString(CultureInfo.InvariantCulture) +
                ";money=" + Game1.player.Money.ToString(CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "movie_theater_fixture_post_state_mismatch" }
        };
    }

    private static bool TryFindMovieTheaterFixtureTiles(
        GameLocation town,
        out Point entrance,
        out Point stand,
        out Point guestTile)
    {
        var layer = town.Map?.GetLayer("Buildings");
        if (layer is not null)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var raw = town.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() != "Theater_Entrance")
                    continue;
                var endpoint = new Point(x, y);
                foreach (var standCandidate in Neighbors(endpoint))
                {
                    if (!IsTileOnMap(town, standCandidate) || !IsTileWalkable(town, standCandidate) ||
                        IsTileOccupiedByCharacter(town, standCandidate))
                        continue;
                    foreach (var guestCandidate in Neighbors(standCandidate))
                    {
                        if (guestCandidate == endpoint || !IsTileOnMap(town, guestCandidate) ||
                            !IsTileWalkable(town, guestCandidate) || IsTileOccupiedByCharacter(town, guestCandidate))
                            continue;
                        entrance = endpoint;
                        stand = standCandidate;
                        guestTile = guestCandidate;
                        return true;
                    }
                }
            }
        }
        entrance = Point.Zero;
        stand = Point.Zero;
        guestTile = Point.Zero;
        return false;
    }
}
