using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupDartsGameFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var dropped = request.DartsLimitedNutDroppedBefore ?? 0;
        if (dropped is < 0 or > 2)
            reasons.Add("darts_game_fixture_drop_count_out_of_range");
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.currentMinigame = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.dayOfMonth = 2;
        Game1.timeOfDay = 2000;
        Game1.isRaining = false;
        var cave = Game1.getLocationFromName("IslandSouthEastCave") as IslandSouthEastCave;
        if (cave is null)
            return BlockedWithPrimitive(request, "debug_setup_darts_game",
                "location=IslandSouthEastCave;pirate_night=true", "location=missing",
                "darts_game_fixture_cave_unavailable");

        cave.updateMap();
        cave.MakeMapModifications(force: true);
        cave.wasPirateCaveOnLoad = true;
        cave.setTileProperty(30, 8, "Buildings", "Action", "DartsGame");
        if (Game1.player.team.limitedNutDrops.ContainsKey("Darts"))
            Game1.player.team.limitedNutDrops["Darts"] = dropped;
        else
            Game1.player.team.limitedNutDrops.Add("Darts", dropped);
        Game1.currentLocation = cave;
        Game1.player.currentLocation = cave;
        var interaction = new Point(30, 8);
        var stand = Neighbors(interaction).First(candidate =>
            IsTileOnMap(cave, candidate) && IsTileWalkable(cave, candidate) && !IsTileOccupiedByCharacter(cave, candidate));
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();

        var verified = ReferenceEquals(Game1.currentLocation, cave) &&
            ReferenceEquals(Game1.player.currentLocation, cave) && Game1.player.TilePoint == stand &&
            IslandSouthEastCave.isPirateNight() &&
            cave.doesTileHaveProperty(30, 8, "Action", "Buildings") == "DartsGame" &&
            Game1.player.team.GetDroppedLimitedNutCount("Darts") == dropped;
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
            PrimitiveKind = "debug_setup_darts_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "fixture_only_even_day_2000_nonrain_pirate_night", "native_cave_DartsGame_action_present", "fixture_only_limited_drop_count_ready" }
                : new[] { "darts_game_fixture_post_state_mismatch" },
            RequestedEffect = "location=IslandSouthEastCave;pirate_night=true;darts_limited_nut_dropped=" + dropped,
            ObservedEffect = "location=" + Game1.currentLocation.NameOrUniqueName +
                ";stand=" + stand.X + "," + stand.Y +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";current_location_ref=" + ReferenceEquals(Game1.currentLocation, cave).ToString().ToLowerInvariant() +
                ";player_location_ref=" + ReferenceEquals(Game1.player.currentLocation, cave).ToString().ToLowerInvariant() +
                ";action=" + (cave.doesTileHaveProperty(30, 8, "Action", "Buildings") ?? "missing") +
                ";day=" + Game1.dayOfMonth + ";time=" + Game1.timeOfDay +
                ";raining=" + Game1.IsRainingHere().ToString().ToLowerInvariant() +
                ";pirate_night=" + IslandSouthEastCave.isPirateNight().ToString().ToLowerInvariant() +
                ";limited_nut_dropped=" + Game1.player.team.GetDroppedLimitedNutCount("Darts").ToString(CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "darts_game_fixture_post_state_mismatch" }
        };
    }
}
