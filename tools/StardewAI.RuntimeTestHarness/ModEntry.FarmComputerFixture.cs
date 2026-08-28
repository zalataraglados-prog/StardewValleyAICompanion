using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFarmComputerFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_farm_computer",
                "isolated_farm_computer=ready", FarmComputerFixtureObserved(), reasons.ToArray());
        }

        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => pair.Value.GetType() == typeof(StardewObject) &&
                string.Equals(pair.Value.QualifiedItemId, FarmComputerQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        var target = FindHousePlantFixtureTile(farm);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_farm_computer",
                "isolated_farm_computer=ready", FarmComputerFixtureObserved(),
                "farm_computer_fixture_tile_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_farm_computer",
                "isolated_farm_computer=ready", FarmComputerFixtureObserved(),
                "farm_computer_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var computer = new StardewObject(target.Value.ToVector2(), "239") { shakeTimer = 0 };
        farm.objects[target.Value.ToVector2()] = computer;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) &&
                !IsTileOccupiedByCharacter(farm, tile) && !IsDestructiveObjectTrap(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_farm_computer",
                "isolated_farm_computer=ready", FarmComputerFixtureObserved(),
                "farm_computer_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = farm.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, computer) && current.GetType() == typeof(StardewObject) &&
            current.bigCraftable.Value && current.ItemId == "239" &&
            current.QualifiedItemId == FarmComputerQualifiedItemId && current.shakeTimer == 0 &&
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
            PrimitiveKind = "debug_setup_farm_computer",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_farm_computer_and_empty_toolbar_slot_installed" }
                : new[] { "farm_computer_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(BC)239;shake_timer=0",
            ObservedEffect = FarmComputerFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "farm_computer_fixture_setup_mismatch" }
        };
    }

    private static string FarmComputerFixtureObserved()
    {
        var row = Game1.currentLocation?.objects.Pairs.FirstOrDefault(pair =>
            string.Equals(pair.Value.QualifiedItemId, FarmComputerQualifiedItemId, StringComparison.Ordinal));
        return row.HasValue && row.Value.Value is not null
            ? "tile=" + (int)row.Value.Key.X + "," + (int)row.Value.Key.Y +
                ";shake_timer=" + row.Value.Value.shakeTimer +
                ";item_id=" + row.Value.Value.ItemId +
                ";qualified_item_id=" + row.Value.Value.QualifiedItemId
            : "farm_computer=missing";
    }
}
