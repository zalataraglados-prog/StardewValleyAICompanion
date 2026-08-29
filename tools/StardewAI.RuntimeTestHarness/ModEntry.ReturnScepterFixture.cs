using StardewAI.Contracts.Training;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupReturnScepterFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var started = DateTimeOffset.UtcNow.ToString("O");
        var source = Game1.currentLocation;
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        var slot = EnsureInventoryItem("(T)ReturnScepter", 1);
        var wand = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as Wand : null;
        FarmHouse? home = null;
        try
        {
            home = Utility.getHomeOfFarmer(Game1.player);
        }
        catch
        {
            // Verified below.
        }
        var door = home?.getFrontDoorSpot();
        if (door.HasValue && string.Equals(source.NameOrUniqueName, "Farm", StringComparison.Ordinal) &&
            Game1.player.TilePoint == door.Value)
        {
            var sourceTile = FindReturnScepterFixtureSourceTile(source);
            if (!sourceTile.HasValue)
                return BlockedWithPrimitive(request, "debug_setup_return_scepter", "Farm",
                    "source_tile=unavailable", "return_scepter_fixture_source_tile_unavailable");
            Game1.player.Position = new Vector2(sourceTile.Value.X * Game1.tileSize, sourceTile.Value.Y * Game1.tileSize);
        }
        var notAtDestination = !door.HasValue || !string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.Ordinal) ||
            Game1.player.TilePoint != door.Value;
        var verified = wand?.GetType() == typeof(Wand) && wand.QualifiedItemId == "(T)ReturnScepter" &&
            wand.Stack == 1 && wand.InstantUse && home is not null && door.HasValue && Game1.player.canMove &&
            notAtDestination;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_return_scepter",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_return_scepter_ready", "own_home_front_door_available", "non_destination_source_ready" }
                : new[] { "slot=" + slot, "wand=" + (wand?.QualifiedItemId ?? "missing"), "home=" + (home?.NameOrUniqueName ?? "missing"), "door=" + (door?.ToString() ?? "missing") },
            RequestedEffect = "player.return_scepter.ready=true",
            ObservedEffect = "location=" + Game1.currentLocation.NameOrUniqueName + ";tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";slot=" + slot +
                ";home=" + (home?.NameOrUniqueName ?? "missing") + ";door=" + (door?.X.ToString() ?? "missing") + "," + (door?.Y.ToString() ?? "missing"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "return_scepter_fixture_post_state_mismatch" }
        };
    }

    private static Point? FindReturnScepterFixtureSourceTile(GameLocation location)
    {
        var back = location.Map?.GetLayer("Back");
        if (back is null)
            return null;
        for (var y = 2; y < back.LayerHeight - 2; y++)
        for (var x = 2; x < back.LayerWidth - 2; x++)
        {
            if (location.warps.Any(warp => warp.X == x && warp.Y == y))
                continue;
            var rectangle = new Rectangle(x * Game1.tileSize, y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
            if (!location.isCollidingPosition(rectangle, Game1.viewport, isFarmer: true, 0, glider: false, Game1.player))
                return new Point(x, y);
        }
        return null;
    }
}
