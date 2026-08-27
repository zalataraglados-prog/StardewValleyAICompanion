using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string SlimeBallFixtureLocationName = "StardewAI_SlimeHutchFixture";

    private TrainingExecutionResult ExecuteSetupSlimeBallFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_slime_ball", "isolated_slime_ball=ready",
                SlimeBallFixtureObserved(), reasons.ToArray());
        }

        var hutch = Game1.locations.OfType<SlimeHutch>()
            .FirstOrDefault(location => string.Equals(location.NameOrUniqueName, SlimeBallFixtureLocationName, StringComparison.Ordinal));
        if (hutch is null)
        {
            hutch = new SlimeHutch("Maps\\SlimeHutch", SlimeBallFixtureLocationName);
            Game1.locations.Add(hutch);
        }
        foreach (var tile in hutch.objects.Pairs
            .Where(pair => string.Equals(pair.Value.QualifiedItemId, SlimeBallQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            hutch.objects.Remove(tile);
        }

        var target = FindSlimeBallFixtureTile(hutch);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_slime_ball", "isolated_slime_ball=ready",
                SlimeBallFixtureObserved(), "slime_ball_fixture_tile_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_slime_ball", "isolated_slime_ball=ready",
                SlimeBallFixtureObserved(), "slime_ball_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var ball = ItemRegistry.Create<StardewObject>(SlimeBallQualifiedItemId);
        ball.TileLocation = target.Value.ToVector2();
        ball.Fragility = 2;
        hutch.objects[target.Value.ToVector2()] = ball;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(hutch, tile) && IsTileWalkable(hutch, tile) &&
                !IsTileOccupiedByCharacter(hutch, tile) && !IsDestructiveObjectTrap(hutch, tile));
        if (stand == default)
        {
            hutch.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_slime_ball", "isolated_slime_ball=ready",
                SlimeBallFixtureObserved(), "slime_ball_fixture_stand_unavailable");
        }
        Game1.warpFarmer(hutch.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = hutch.GetType() == typeof(SlimeHutch) &&
            hutch.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, ball) && current.GetType() == typeof(StardewObject) &&
            current.bigCraftable.Value && current.Fragility == 2 &&
            current.QualifiedItemId == SlimeBallQualifiedItemId &&
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
            PrimitiveKind = "debug_setup_slime_ball",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_SlimeHutch_base_fragility_2_slime_ball_and_empty_toolbar_slot_installed" }
                : new[] { "slime_ball_fixture_setup_mismatch" },
            RequestedEffect = "slime_ball_qualified_item_id=(BC)56;fragility=2",
            ObservedEffect = SlimeBallFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "slime_ball_fixture_setup_mismatch" }
        };
    }

    private static Point? FindSlimeBallFixtureTile(GameLocation location)
    {
        for (var y = 2; y < 12; y++)
        for (var x = 2; x < 20; x++)
        {
            var target = new Point(x, y);
            if (location.objects.ContainsKey(target.ToVector2()) || !IsTileOnMap(location, target))
            {
                continue;
            }
            if (Neighbors(target).Any(tile => IsTileOnMap(location, tile) && IsTileWalkable(location, tile)))
            {
                return target;
            }
        }
        return null;
    }

    private static string SlimeBallFixtureObserved()
    {
        var location = Game1.currentLocation;
        if (location is null)
        {
            return "location=unavailable;slime_ball=missing";
        }
        foreach (var row in location.objects.Pairs)
        {
            if (string.Equals(row.Value.QualifiedItemId, SlimeBallQualifiedItemId, StringComparison.Ordinal))
            {
                return "location=" + location.NameOrUniqueName +
                    ";tile=" + (int)row.Key.X + "," + (int)row.Key.Y +
                    ";qualified_item_id=" + row.Value.QualifiedItemId +
                    ";fragility=" + row.Value.Fragility;
            }
        }
        return "location=" + location.NameOrUniqueName + ";slime_ball=missing";
    }
}
