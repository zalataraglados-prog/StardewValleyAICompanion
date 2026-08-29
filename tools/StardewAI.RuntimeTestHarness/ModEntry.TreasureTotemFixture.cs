using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupTreasureTotemFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getLocationFromName("Farm");
        if (farm is null)
            return BlockedWithPrimitive(request, "debug_setup_treasure_totem", "Farm",
                "location=unavailable", "treasure_totem_fixture_farm_unavailable");
        Game1.warpFarmer("Farm", Math.Max(1, Game1.player.TilePoint.X), Math.Max(1, Game1.player.TilePoint.Y), false);
        farm = Game1.currentLocation;
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        var center = FindTreasureTotemFixtureCenter(farm);
        if (!center.HasValue)
            return BlockedWithPrimitive(request, "debug_setup_treasure_totem", "Farm",
                "spawnable_center=unavailable", "treasure_totem_fixture_spawnable_center_unavailable");
        Game1.player.Position = new Vector2(center.Value.X * Game1.tileSize, center.Value.Y * Game1.tileSize);
        var slot = EnsureInventoryItem("(O)TreasureTotem", 2);
        var totem = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        if (totem is not null)
            totem.Stack = 2;
        var spawnCount = ReadTreasureTotemSpawnTiles(farm, Game1.player.Tile).Length;
        var verified = totem?.GetType() == typeof(StardewValley.Object) &&
            totem.QualifiedItemId == "(O)TreasureTotem" && totem.Stack == 2 && farm.IsOutdoors &&
            spawnCount > 0 && Game1.player.canMove;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_treasure_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_treasure_totem_ready", "outdoor_spawnable_native_ring_center_ready" }
                : new[] { "slot=" + slot, "item=" + (totem?.QualifiedItemId ?? "missing"), "spawn_count=" + spawnCount },
            RequestedEffect = "player.treasure_totem.ready=true",
            ObservedEffect = "location=Farm;center_tile=" + center.Value.X + "," + center.Value.Y +
                ";slot=" + slot + ";stack=" + (totem?.Stack ?? 0) + ";spawn_count=" + spawnCount,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "treasure_totem_fixture_post_state_mismatch" }
        };
    }

    private static Point? FindTreasureTotemFixtureCenter(GameLocation location)
    {
        var back = location.Map?.GetLayer("Back");
        if (back is null)
            return null;
        Point? best = null;
        var bestCount = 0;
        for (var y = 4; y < back.LayerHeight - 4; y++)
        for (var x = 4; x < back.LayerWidth - 4; x++)
        {
            var rectangle = new Rectangle(x * Game1.tileSize, y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
            if (location.isCollidingPosition(rectangle, Game1.viewport, isFarmer: true, 0, glider: false, Game1.player))
                continue;
            var count = ReadTreasureTotemSpawnTiles(location, new Vector2(x, y)).Length;
            if (count <= bestCount)
                continue;
            best = new Point(x, y);
            bestCount = count;
            if (bestCount == 16)
                return best;
        }
        return best;
    }
}
