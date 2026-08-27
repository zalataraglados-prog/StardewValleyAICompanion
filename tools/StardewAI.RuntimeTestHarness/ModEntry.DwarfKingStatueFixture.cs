using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupDwarfKingStatueFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_dwarf_king_statue", "mining_mastery_and_dwarf_king_statue=ready", DwarfKingStatueFixtureObserved(), reasons.ToArray());
        }
        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => pair.Value.QualifiedItemId == DwarfKingStatueQualifiedItemId)
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        var target = FindDwarfKingStatueFixtureTile(farm);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_dwarf_king_statue", "mining_mastery_and_dwarf_king_statue=ready", DwarfKingStatueFixtureObserved(), "dwarf_king_statue_fixture_tile_unavailable");
        }

        Game1.exitActiveMenu();
        foreach (var id in DwarfKingActiveBuffIds())
        {
            Game1.player.buffs.Remove(id);
        }
        Game1.player.stats.Set(StatKeys.Mastery(3), 1u);
        var statue = ItemRegistry.Create<StardewObject>(DwarfKingStatueQualifiedItemId);
        farm.objects[target.Value.ToVector2()] = statue;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) && !IsTileOccupiedByCharacter(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_dwarf_king_statue", "mining_mastery_and_dwarf_king_statue=ready", DwarfKingStatueFixtureObserved(), "dwarf_king_statue_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = Game1.player.stats.Get(StatKeys.Mastery(3)) >= 1 &&
            DwarfKingActiveBuffIds().Length == 0 &&
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
            PrimitiveKind = "debug_setup_dwarf_king_statue",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_exact_dwarf_king_statue_and_mining_mastery_installed" } : new[] { "dwarf_king_statue_fixture_setup_mismatch" },
            RequestedEffect = "mining_mastery=1;active_dwarf_statue_buff=none;current_location_has_exact_statue=true",
            ObservedEffect = DwarfKingStatueFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "dwarf_king_statue_fixture_setup_mismatch" }
        };
    }

    private static Point? FindDwarfKingStatueFixtureTile(GameLocation location)
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

    private static string DwarfKingStatueFixtureObserved()
    {
        var location = Game1.currentLocation;
        var statueCount = location?.objects.Values.Count(item => item.QualifiedItemId == DwarfKingStatueQualifiedItemId) ?? 0;
        return "current_location=" + (location?.NameOrUniqueName ?? "none") +
            ";mining_mastery=" + Game1.player.stats.Get(StatKeys.Mastery(3)) +
            ";active_dwarf_statue_buffs=" + string.Join(",", DwarfKingActiveBuffIds()) +
            ";current_location_statue_count=" + statueCount;
    }
}
