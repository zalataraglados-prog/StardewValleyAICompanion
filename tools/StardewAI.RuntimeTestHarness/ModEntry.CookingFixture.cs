using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCookingFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (request.CookingSourceKind is not ("kitchen" or "cookout_kit"))
        {
            return BlockedWithPrimitive(request, "debug_setup_cooking_fixture",
                "cooking_fixture=ready", "source=missing", "cooking_source_kind_required");
        }

        var player = Game1.player;
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            player.Items[slot] = null;
        }
        player.cookingRecipes["Fried Egg"] = 0;
        player.recipesCooked.Remove("194");
        Game1.activeClickableMenu = null;

        GameLocation location;
        Point target;
        if (request.CookingSourceKind == "kitchen")
        {
            var home = Utility.getHomeOfFarmer(player);
            if (home is null)
            {
                return CookingFixtureBlocked(request, "cooking_fixture_home_missing");
            }
            player.HouseUpgradeLevel = Math.Max(1, player.HouseUpgradeLevel);
            home.setMapForUpgradeLevel(Math.Max(1, home.upgradeLevel));
            var kitchen = FindKitchenActionTile(home);
            var fridge = home.GetFridge();
            if (!kitchen.HasValue || fridge is null)
            {
                return CookingFixtureBlocked(request, "cooking_fixture_kitchen_or_fridge_missing");
            }
            fridge.Items.Clear();
            fridge.Items.Add(ItemRegistry.Create("(O)176", 1));
            player.Items[0] = ItemRegistry.Create("(O)917", 1);
            location = home;
            target = kitchen.Value;
        }
        else
        {
            var farm = Game1.getFarm();
            var tile = FindCookingFixtureTile(farm);
            if (!tile.HasValue)
            {
                return CookingFixtureBlocked(request, "cooking_fixture_cookout_tile_missing");
            }
            farm.objects[tile.Value.ToVector2()] = new Torch("278", bigCraftable: true)
            {
                Fragility = 1,
                destroyOvernight = true
            };
            player.Items[0] = ItemRegistry.Create("(O)176", 1);
            location = farm;
            target = tile.Value;
        }

        var moved = MoveFixtureFarmerToLocationAdjacent(location, target, out var stand, out var moveReason);
        var verified = moved && player.cookingRecipes.ContainsKey("Fried Egg") &&
            (request.CookingSourceKind == "kitchen"
                ? location.GetFridge()?.Items.CountId("(O)176") == 1 && player.Items.CountId("(O)917") == 1
                : location.objects.TryGetValue(target.ToVector2(), out var value) && value.QualifiedItemId == "(BC)278" &&
                  player.Items.CountId("(O)176") == 1);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_cooking_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_save_cooking_fixture_ready", "learned_recipe_and_exact_native_source_materials_ready" }
                : new[] { moveReason, "cooking_fixture_post_state_mismatch" },
            RequestedEffect = "cooking_fixture=ready;source=" + request.CookingSourceKind,
            ObservedEffect = "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y +
                ";stand=" + stand.X + "," + stand.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "cooking_fixture_post_state_mismatch" }
        };
    }

    private static Point? FindKitchenActionTile(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
        {
            return null;
        }
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            if (string.Equals(location.doesTileHaveProperty(x, y, "Action", "Buildings"),
                    "kitchen", StringComparison.OrdinalIgnoreCase))
            {
                return new Point(x, y);
            }
        }
        return null;
    }

    private static Point? FindCookingFixtureTile(Farm farm)
    {
        for (var y = 8; y < 40; y++)
        for (var x = 8; x < 70; x++)
        {
            var target = new Point(x, y);
            if (farm.objects.ContainsKey(target.ToVector2()) || !IsTileOnMap(farm, target))
            {
                continue;
            }
            if (Neighbors(target).Any(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile)))
            {
                return target;
            }
        }
        return null;
    }

    private static TrainingExecutionResult CookingFixtureBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(request, "debug_setup_cooking_fixture",
            "cooking_fixture=ready", "cooking_fixture=blocked", reasons);
}
