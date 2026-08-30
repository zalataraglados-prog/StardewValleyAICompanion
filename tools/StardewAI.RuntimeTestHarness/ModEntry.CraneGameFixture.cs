using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCraneGameFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.currentMinigame = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        var theater = Game1.getLocationFromName("MovieTheater") as MovieTheater;
        if (theater is null || !TryFindCraneGameFixtureEndpoint(theater, out var interaction, out var stand))
        {
            return BlockedWithPrimitive(request, "debug_setup_crane_game",
                "location=MovieTheater;machine_occupied=false;money=10000;empty_slots>=3",
                "location=" + (theater?.NameOrUniqueName ?? "missing"),
                "crane_game_fixture_endpoint_unavailable");
        }

        var buildings = theater.Map?.GetLayer("Buildings");
        if (buildings is null || buildings.LayerWidth <= 2 || buildings.LayerHeight <= 9)
        {
            return BlockedWithPrimitive(request, "debug_setup_crane_game",
                "location=MovieTheater;machine_occupied=false", "buildings_layer=missing_or_small",
                "crane_game_fixture_occupancy_tile_unavailable");
        }
        buildings.Tiles[2, 9] = null;
        Game1.currentLocation = theater;
        Game1.player.currentLocation = theater;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        Game1.player.Money = Math.Max(request.CraneMoneyBefore ?? 10000, 500);
        var cleared = 0;
        for (var slot = Game1.player.Items.Count - 1; slot >= 0 && cleared < 3; slot--)
        {
            if (Game1.player.Items[slot] is not null)
                Game1.player.Items[slot] = null;
            cleared++;
        }

        var verified = ReferenceEquals(Game1.currentLocation, theater) &&
            ReferenceEquals(Game1.player.currentLocation, theater) &&
            Game1.player.TilePoint == stand && Game1.player.Money >= 500 &&
            Game1.player.Items.Count(item => item is null) >= 3 && buildings.Tiles[2, 9] is null;
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
            PrimitiveKind = "debug_setup_crane_game",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_MovieTheater_and_CraneGame_action_present",
                    "fixture_only_occupancy_tile_cleared",
                    "fixture_only_fee_and_three_reward_slots_ready"
                }
                : new[] { "crane_game_fixture_post_state_mismatch" },
            RequestedEffect = "location=MovieTheater;machine_occupied=false;money>=500;empty_slots>=3",
            ObservedEffect = "location=" + Game1.currentLocation.NameOrUniqueName +
                ";interaction=" + interaction.X + "," + interaction.Y +
                ";stand=" + stand.X + "," + stand.Y +
                ";money=" + Game1.player.Money.ToString(CultureInfo.InvariantCulture) +
                ";empty_slots=" + Game1.player.Items.Count(item => item is null).ToString(CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "crane_game_fixture_post_state_mismatch" }
        };
    }

    private static bool TryFindCraneGameFixtureEndpoint(
        MovieTheater theater,
        out Point interaction,
        out Point stand)
    {
        var layer = theater.Map?.GetLayer("Buildings");
        if (layer is not null)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (theater.doesTileHaveProperty(x, y, "Action", "Buildings") != "CraneGame")
                    continue;
                var target = new Point(x, y);
                foreach (var candidate in Neighbors(target))
                {
                    if (!IsTileOnMap(theater, candidate) || !IsTileWalkable(theater, candidate) ||
                        IsTileOccupiedByCharacter(theater, candidate))
                        continue;
                    interaction = target;
                    stand = candidate;
                    return true;
                }
            }
        }
        interaction = Point.Zero;
        stand = Point.Zero;
        return false;
    }
}
