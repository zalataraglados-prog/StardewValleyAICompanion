using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPotOfGoldFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_pot_of_gold", "Forest.objects[52,98]=(O)PotOfGold", PotOfGoldFixtureObserved(), reasons.ToArray());
        }
        var forest = Game1.getLocationFromName("Forest");
        if (forest is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_pot_of_gold", "Forest.objects[52,98]=(O)PotOfGold", PotOfGoldFixtureObserved(), "pot_of_gold_fixture_forest_unavailable");
        }

        Game1.exitActiveMenu();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 17;
        var target = new Point(52, 98);
        forest.objects.Remove(target.ToVector2());
        for (var index = forest.debris.Count - 1; index >= 0; index--)
        {
            var debris = forest.debris[index];
            var qualifiedItemId = debris.item?.QualifiedItemId ??
                ItemRegistry.QualifyItemId(debris.itemId.Value) ??
                debris.itemId.Value;
            if (qualifiedItemId is PotOfGoldCoinQualifiedItemId or PotOfGoldHatQualifiedItemId)
            {
                forest.debris.RemoveAt(index);
            }
        }
        var pot = ItemRegistry.Create<StardewObject>(PotOfGoldQualifiedItemId);
        pot.Stack = 1;
        forest.objects[target.ToVector2()] = pot;
        if (request.DebugFillInventory)
        {
            FillInventoryWithBlockingItems("GoldCoin");
        }

        var stand = PotOfGoldFixtureStand(forest, target);
        if (stand is null)
        {
            forest.objects.Remove(target.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_pot_of_gold", "Forest.objects[52,98]=(O)PotOfGold", PotOfGoldFixtureObserved(), "pot_of_gold_fixture_no_walkable_stand");
        }
        Game1.warpFarmer("Forest", stand.Value.X, stand.Value.Y, false);
        Game1.player.Position = stand.Value.ToVector2() * Game1.tileSize;

        // warpFarmer commits the location change on a later game tick. Verify the
        // fixture mutations now; the smoke test then requires a fresh Forest snapshot.
        var verified = Game1.IsSpring && Game1.dayOfMonth == 17 &&
            forest.objects.TryGetValue(target.ToVector2(), out var current) &&
            ReferenceEquals(current, pot) &&
            Game1.player.TilePoint == stand.Value;
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
            PrimitiveKind = "debug_setup_pot_of_gold",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_spring_17_forest_exact_registry_pot_installed" } : new[] { "pot_of_gold_fixture_setup_mismatch" },
            RequestedEffect = "Forest.objects[52,98]=(O)PotOfGold;date=spring:17",
            ObservedEffect = PotOfGoldFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "pot_of_gold_fixture_setup_mismatch" }
        };
    }

    private static Point? PotOfGoldFixtureStand(GameLocation location, Point target)
    {
        foreach (var stand in new[]
        {
            new Point(target.X, target.Y - 1),
            new Point(target.X, target.Y + 1),
            new Point(target.X - 1, target.Y),
            new Point(target.X + 1, target.Y)
        })
        {
            if (IsTileOnMap(location, stand) && IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand))
            {
                return stand;
            }
        }
        return null;
    }

    private static string PotOfGoldFixtureObserved()
    {
        var location = Game1.currentLocation;
        var forest = Game1.getLocationFromName("Forest");
        var present = forest is not null && forest.objects.TryGetValue(new Vector2(52f, 98f), out var item) && item.QualifiedItemId == PotOfGoldQualifiedItemId;
        return "current_location=" + (location?.NameOrUniqueName ?? "none") + ";requested_location=Forest;date=" + Game1.currentSeason + ":" + Game1.dayOfMonth + ";tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";forest_pot_present=" + present.ToString().ToLowerInvariant();
    }
}
