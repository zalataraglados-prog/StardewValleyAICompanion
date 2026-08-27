using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupStatueBlessingFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_statue_blessing", "farming_mastery_and_statue_blessing=ready", StatueBlessingFixtureObserved(), reasons.ToArray());
        }
        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => pair.Value.QualifiedItemId == StatueOfBlessingsQualifiedItemId)
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        var target = FindStatueBlessingFixtureTile(farm);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_statue_blessing", "farming_mastery_and_statue_blessing=ready", StatueBlessingFixtureObserved(), "statue_blessing_fixture_tile_unavailable");
        }

        Game1.exitActiveMenu();
        foreach (var id in StatueBlessingActiveBuffIds())
        {
            Game1.player.buffs.Remove(id);
        }
        Game1.player.hasBeenBlessedByStatueToday = false;
        Game1.player.stats.Set(StatKeys.Mastery(0), 1u);
        var statue = ItemRegistry.Create<StardewObject>(StatueOfBlessingsQualifiedItemId);
        farm.objects[target.Value.ToVector2()] = statue;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) && !IsTileOccupiedByCharacter(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_statue_blessing", "farming_mastery_and_statue_blessing=ready", StatueBlessingFixtureObserved(), "statue_blessing_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = Game1.player.stats.Get(StatKeys.Mastery(0)) >= 1 &&
            !Game1.player.hasBeenBlessedByStatueToday &&
            StatueBlessingActiveBuffIds().Length == 0 &&
            farm.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, statue) && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_statue_blessing",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_exact_statue_of_blessings_and_farming_mastery_installed" } : new[] { "statue_blessing_fixture_setup_mismatch" },
            RequestedEffect = "farming_mastery=1;has_been_blessed_today=false;active_blessing_buff=none;current_location_has_exact_statue=true",
            ObservedEffect = StatueBlessingFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "statue_blessing_fixture_setup_mismatch" }
        };
    }

    private static Point? FindStatueBlessingFixtureTile(GameLocation location)
    {
        for (var y = 8; y < 40; y++)
        for (var x = 8; x < 70; x++)
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

    private static string StatueBlessingFixtureObserved()
    {
        var location = Game1.currentLocation;
        var statueCount = location?.objects.Values.Count(item => item.QualifiedItemId == StatueOfBlessingsQualifiedItemId) ?? 0;
        return "current_location=" + (location?.NameOrUniqueName ?? "none") +
            ";farming_mastery=" + Game1.player.stats.Get(StatKeys.Mastery(0)) +
            ";has_been_blessed_today=" + Game1.player.hasBeenBlessedByStatueToday.ToString().ToLowerInvariant() +
            ";active_statue_blessing_buffs=" + string.Join(",", StatueBlessingActiveBuffIds()) +
            ";current_location_statue_count=" + statueCount;
    }
}
