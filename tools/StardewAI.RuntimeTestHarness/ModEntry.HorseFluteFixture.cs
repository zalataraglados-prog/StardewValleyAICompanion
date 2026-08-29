using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupHorseFluteFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();

        var stable = farm.buildings.OfType<Stable>().FirstOrDefault(row => row.owner.Value == Game1.player.UniqueMultiplayerID);
        if (stable is null)
        {
            stable = new Stable(Vector2.Zero);
            stable.LoadFromBuildingData(stable.GetData(), forUpgrade: false, forConstruction: true);
            if (!TryFindFixtureBuildingTile(farm, stable, out var stableTile))
                return BlockedWithPrimitive(request, "debug_setup_horse_flute", "player.horse_flute.ready=true", "stable_tile=missing", "horse_flute_fixture_stable_tile_unavailable");
            stable.tileX.Value = stableTile.X;
            stable.tileY.Value = stableTile.Y;
            stable.owner.Value = Game1.player.UniqueMultiplayerID;
            stable.daysOfConstructionLeft.Value = 0;
            stable.load();
            farm.buildings.Add(stable);
        }
        stable.owner.Value = Game1.player.UniqueMultiplayerID;
        stable.daysOfConstructionLeft.Value = 0;
        stable.grabHorse();

        var horse = stable.getStableHorse();
        if (horse is null)
            return BlockedWithPrimitive(request, "debug_setup_horse_flute", "player.horse_flute.ready=true", "horse=missing", "horse_flute_fixture_horse_unavailable");
        horse.ownerId.Value = Game1.player.UniqueMultiplayerID;
        Game1.player.horseName.Value = string.IsNullOrWhiteSpace(Game1.player.horseName.Value) ? "Runtime Fixture Horse" : Game1.player.horseName.Value;
        Game1.warpCharacter(horse, farm, stable.GetDefaultHorseTile().ToVector2());

        var playerTile = FindHorseFluteFixturePlayerTile(farm, horse.TilePoint);
        if (!playerTile.HasValue)
            return BlockedWithPrimitive(request, "debug_setup_horse_flute", "player.horse_flute.ready=true", "player_tile=missing", "horse_flute_fixture_clear_player_tile_unavailable");
        Game1.player.Position = playerTile.Value.ToVector2() * Game1.tileSize;
        Game1.player.Halt();
        Game1.player.forceCanMove();

        var slot = EnsureInventoryItem("(O)911", 1);
        var flute = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        var restrictions = Utility.GetHorseWarpRestrictionsForFarmer(Game1.player);
        var nearby = Math.Abs(Game1.player.TilePoint.X - horse.TilePoint.X) <= 1 &&
            Math.Abs(Game1.player.TilePoint.Y - horse.TilePoint.Y) <= 1;
        var verified = flute?.GetType() == typeof(StardewValley.Object) &&
            string.Equals(flute.QualifiedItemId, "(O)911", StringComparison.Ordinal) &&
            restrictions == Utility.HorseWarpRestrictions.None && !nearby && horse.getOwner() == Game1.player;

        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_horse_flute",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_stable_and_owned_horse_ready", "exact_base_reusable_horse_flute_ready", "clear_outdoor_summon_rectangle_ready", "horse_remote_from_player" }
                : new[] { "restrictions=" + (int)restrictions, "nearby=" + nearby.ToString().ToLowerInvariant(), "slot=" + slot },
            RequestedEffect = "player.horse_flute.ready=true",
            ObservedEffect = "stable_tile=" + stable.tileX.Value + "," + stable.tileY.Value +
                ";horse_id=" + horse.HorseId + ";horse_tile=" + horse.TilePoint.X + "," + horse.TilePoint.Y +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";inventory_slot_index=" + slot + ";restrictions=" + (int)restrictions,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "horse_flute_fixture_post_state_mismatch" }
        };
    }

    private static Point? FindHorseFluteFixturePlayerTile(GameLocation farm, Point horseTile)
    {
        var back = farm.Map?.GetLayer("Back");
        if (back is null)
            return null;
        for (var y = 1; y < back.LayerHeight - 1; y++)
        {
            for (var x = 1; x < back.LayerWidth - 2; x++)
            {
                if (Math.Abs(x - horseTile.X) <= 8 && Math.Abs(y - horseTile.Y) <= 8)
                    continue;
                var rectangle = new Rectangle(x * Game1.tileSize, y * Game1.tileSize, 128, 64);
                if (!farm.isCollidingPosition(rectangle, Game1.viewport, isFarmer: true, 0, glider: false, Game1.player))
                    return new Point(x, y);
            }
        }
        return null;
    }
}
