using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFeedHopperFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_feed_hopper", "isolated_feed_hopper=ready",
                FeedHopperFixtureObserved(), reasons.ToArray());
        }

        var farm = Game1.getFarm();
        var house = farm.buildings
            .Select(building => building.GetIndoors())
            .OfType<AnimalHouse>()
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (house is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_feed_hopper", "isolated_feed_hopper=ready",
                FeedHopperFixtureObserved(), "feed_hopper_fixture_requires_existing_animal_house");
        }

        foreach (var tile in house.objects.Pairs
            .Where(pair => pair.Value.GetType() == typeof(StardewObject) &&
                string.Equals(pair.Value.QualifiedItemId, FeedHopperQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            house.objects.Remove(tile);
        }
        foreach (var tile in house.objects.Pairs
            .Where(pair => string.Equals(pair.Value.Name, "Hay", StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            house.objects.Remove(tile);
        }
        var target = FindHousePlantFixtureTile(house);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_feed_hopper", "isolated_feed_hopper=ready",
                FeedHopperFixtureObserved(), "feed_hopper_fixture_tile_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_feed_hopper", "isolated_feed_hopper=ready",
                FeedHopperFixtureObserved(), "feed_hopper_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        if (house.animalsThatLiveHere.Count == 0)
            house.animalsThatLiveHere.Add(long.MaxValue);
        house.GetRootLocation().piecesOfHay.Value = 10;
        var hopper = new StardewObject(target.Value.ToVector2(), "99");
        house.objects[target.Value.ToVector2()] = hopper;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(house, tile) && IsTileWalkable(house, tile) &&
                !IsTileOccupiedByCharacter(house, tile) && !IsDestructiveObjectTrap(house, tile));
        if (stand == default)
        {
            house.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_feed_hopper", "isolated_feed_hopper=ready",
                FeedHopperFixtureObserved(), "feed_hopper_fixture_stand_unavailable");
        }
        Game1.warpFarmer(house.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.currentLocation = house;
        Game1.player.currentLocation = house;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var expected = Math.Min(
            Math.Max(1, Math.Min(house.animalsThatLiveHere.Count, house.GetRootLocation().piecesOfHay.Value)),
            house.animalLimit.Value - house.numberOfObjectsWithName("Hay"));
        var verified = house.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, hopper) && current.GetType() == typeof(StardewObject) &&
            current.bigCraftable.Value && current.ItemId == "99" &&
            current.QualifiedItemId == FeedHopperQualifiedItemId && expected > 0 &&
            Game1.player.couldInventoryAcceptThisItem(FeedHopperHayQualifiedItemId, expected, 0) &&
            Game1.currentLocation == house && Game1.player.currentLocation == house &&
            Game1.player.Items[emptySlot] is null && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_feed_hopper",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_feed_hopper_unfed_animal_silo_hay_and_empty_toolbar_slot_installed" }
                : new[] { "feed_hopper_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(BC)99;silo_hay=10;unfed_animal_count>0",
            ObservedEffect = FeedHopperFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "feed_hopper_fixture_setup_mismatch" }
        };
    }

    private static string FeedHopperFixtureObserved()
    {
        if (Game1.currentLocation is not AnimalHouse house)
            return "animal_house=missing";
        var row = house.objects.Pairs
            .Where(pair => string.Equals(
                pair.Value.QualifiedItemId, FeedHopperQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => new { pair.Key, pair.Value })
            .FirstOrDefault();
        return row is not null
            ? "tile=" + (int)row.Key.X + "," + (int)row.Key.Y +
                ";item_id=" + row.Value.ItemId +
                ";qualified_item_id=" + row.Value.QualifiedItemId +
                ";silo_hay=" + house.GetRootLocation().piecesOfHay.Value +
                ";animals=" + house.animalsThatLiveHere.Count +
                ";placed_hay=" + house.numberOfObjectsWithName("Hay")
            : "feed_hopper=missing";
    }
}
