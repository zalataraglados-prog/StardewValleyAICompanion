using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSingingStoneFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_singing_stone", "isolated_singing_stone=ready",
                SingingStoneFixtureObserved(), reasons.ToArray());
        }

        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => pair.Value.GetType() == typeof(StardewObject) &&
                string.Equals(pair.Value.QualifiedItemId, SingingStoneQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        var target = FindHousePlantFixtureTile(farm);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_singing_stone", "isolated_singing_stone=ready",
                SingingStoneFixtureObserved(), "singing_stone_fixture_tile_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_singing_stone", "isolated_singing_stone=ready",
                SingingStoneFixtureObserved(), "singing_stone_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var stone = new StardewObject(target.Value.ToVector2(), "94") { shakeTimer = 0 };
        farm.objects[target.Value.ToVector2()] = stone;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) &&
                !IsTileOccupiedByCharacter(farm, tile) && !IsDestructiveObjectTrap(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_singing_stone", "isolated_singing_stone=ready",
                SingingStoneFixtureObserved(), "singing_stone_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = farm.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, stone) && current.GetType() == typeof(StardewObject) &&
            current.bigCraftable.Value && current.ItemId == "94" && current.QualifiedItemId == SingingStoneQualifiedItemId &&
            current.shakeTimer == 0 && Game1.player.Items[emptySlot] is null && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_singing_stone",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_singing_stone_and_empty_toolbar_slot_installed" }
                : new[] { "singing_stone_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(BC)94;shake_timer=0",
            ObservedEffect = SingingStoneFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "singing_stone_fixture_setup_mismatch" }
        };
    }

    private static string SingingStoneFixtureObserved()
    {
        var row = Game1.currentLocation?.objects.Pairs
            .FirstOrDefault(pair => string.Equals(
                pair.Value.QualifiedItemId, SingingStoneQualifiedItemId, StringComparison.Ordinal));
        return row.HasValue
            ? "tile=" + (int)row.Value.Key.X + "," + (int)row.Value.Key.Y +
                ";shake_timer=" + row.Value.Value.shakeTimer +
                ";item_id=" + row.Value.Value.ItemId +
                ";qualified_item_id=" + row.Value.Value.QualifiedItemId
            : "singing_stone=missing";
    }
}
