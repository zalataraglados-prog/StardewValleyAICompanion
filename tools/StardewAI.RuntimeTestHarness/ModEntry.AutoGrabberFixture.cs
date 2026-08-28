using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupAutoGrabberFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), reasons.ToArray());
        }

        var house = Game1.getFarm().buildings
            .Select(building => building.GetIndoors())
            .OfType<AnimalHouse>()
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (house is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), "auto_grabber_fixture_requires_existing_animal_house");
        }
        foreach (var tile in house.objects.Pairs
            .Where(pair => string.Equals(pair.Value.QualifiedItemId, AutoGrabberQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            house.objects.Remove(tile);
        }
        var target = FindHousePlantFixtureTile(house);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), "auto_grabber_fixture_tile_unavailable");
        }

        var milk = ItemRegistry.Create("(O)184", 2);
        var egg = ItemRegistry.Create("(O)176", 3);
        if (!Game1.player.couldInventoryAcceptThisItem(milk) || !Game1.player.couldInventoryAcceptThisItem(egg))
        {
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), "auto_grabber_fixture_inventory_capacity_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), "auto_grabber_fixture_safe_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var autoGrabber = new StardewObject(target.Value.ToVector2(), "165");
        var chest = new Chest();
        chest.Items.Add(milk);
        chest.Items.Add(egg);
        autoGrabber.heldObject.Value = chest;
        autoGrabber.showNextIndex.Value = true;
        house.objects[target.Value.ToVector2()] = autoGrabber;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(house, tile) && IsTileWalkable(house, tile) &&
                !IsTileOccupiedByCharacter(house, tile) && !IsDestructiveObjectTrap(house, tile));
        if (stand == default)
        {
            house.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_auto_grabber", "isolated_auto_grabber=ready",
                AutoGrabberFixtureObserved(), "auto_grabber_fixture_stand_unavailable");
        }
        Game1.warpFarmer(house.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.currentLocation = house;
        Game1.player.currentLocation = house;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = house.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, autoGrabber) && ReferenceEquals(current.heldObject.Value, chest) &&
            chest.Items.Count(item => item is not null) == 2 && chest.Items.Sum(item => item?.Stack ?? 0) == 5 &&
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
            PrimitiveKind = "debug_setup_auto_grabber",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_auto_grabber_two_stack_held_chest_and_safe_toolbar_slot_installed" }
                : new[] { "auto_grabber_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(BC)165;held_chest_stacks=2;held_chest_quantity=5",
            ObservedEffect = AutoGrabberFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "auto_grabber_fixture_setup_mismatch" }
        };
    }

    private static string AutoGrabberFixtureObserved()
    {
        var row = Game1.currentLocation?.objects.Pairs
            .Where(pair => string.Equals(pair.Value.QualifiedItemId, AutoGrabberQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => new { pair.Key, pair.Value })
            .FirstOrDefault();
        if (row is null || row.Value.heldObject.Value is not Chest chest)
            return "auto_grabber=missing";
        return "tile=" + (int)row.Key.X + "," + (int)row.Key.Y +
            ";item_id=" + row.Value.ItemId +
            ";qualified_item_id=" + row.Value.QualifiedItemId +
            ";held_chest_stacks=" + chest.Items.Count(item => item is not null) +
            ";held_chest_quantity=" + chest.Items.Sum(item => item?.Stack ?? 0);
    }
}
