using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupHousePlantFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_house_plant_rotation", "isolated_house_plant_frame=ready",
                HousePlantFixtureObserved(), reasons.ToArray());
        }
        var requestedSpriteIndex = request.HousePlantCurrentSpriteIndex.GetValueOrDefault(-1);
        if (requestedSpriteIndex is < 0 or > 7)
        {
            return BlockedWithPrimitive(request, "debug_setup_house_plant_rotation", "isolated_house_plant_frame=ready",
                HousePlantFixtureObserved(), "house_plant_fixture_sprite_index_0_7_required");
        }

        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => pair.Value.GetType() == typeof(StardewObject) &&
                pair.Value.bigCraftable.Value &&
                string.Equals(pair.Value.Name, "House Plant", StringComparison.Ordinal) &&
                IsCanonicalHousePlantQualifiedItemId(pair.Value.QualifiedItemId))
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        var target = FindHousePlantFixtureTile(farm);
        if (target is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_house_plant_rotation", "isolated_house_plant_frame=ready",
                HousePlantFixtureObserved(), "house_plant_fixture_tile_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_house_plant_rotation", "isolated_house_plant_frame=ready",
                HousePlantFixtureObserved(), "house_plant_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var plant = new StardewObject(target.Value.ToVector2(), "0");
        plant.ParentSheetIndex = requestedSpriteIndex;
        farm.objects[target.Value.ToVector2()] = plant;
        var stand = Neighbors(target.Value)
            .FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) && !IsTileOccupiedByCharacter(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_house_plant_rotation", "isolated_house_plant_frame=ready",
                HousePlantFixtureObserved(), "house_plant_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;

        var verified = farm.objects.TryGetValue(target.Value.ToVector2(), out var current) &&
            ReferenceEquals(current, plant) &&
            current.ParentSheetIndex == request.HousePlantCurrentSpriteIndex &&
            current.ItemId == "0" && current.QualifiedItemId == "(BC)0" &&
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
            PrimitiveKind = "debug_setup_house_plant_rotation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_canonical_house_plant_visual_frame_and_empty_toolbar_slot_installed" }
                : new[] { "house_plant_fixture_setup_mismatch" },
            RequestedEffect = "house_plant_item_id=0;qualified_item_id=(BC)0;parent_sheet_index=" + request.HousePlantCurrentSpriteIndex,
            ObservedEffect = HousePlantFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "house_plant_fixture_setup_mismatch" }
        };
    }

    private static Point? FindHousePlantFixtureTile(GameLocation location)
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

    private static string HousePlantFixtureObserved()
    {
        var row = Game1.currentLocation?.objects.Pairs
            .Where(pair => pair.Value.GetType() == typeof(StardewObject) &&
                pair.Value.bigCraftable.Value &&
                string.Equals(pair.Value.Name, "House Plant", StringComparison.Ordinal) &&
                IsCanonicalHousePlantQualifiedItemId(pair.Value.QualifiedItemId))
            .Select(pair => new
            {
                pair.Key,
                pair.Value
            })
            .FirstOrDefault();
        return row is not null
            ? "tile=" + (int)row.Key.X + "," + (int)row.Key.Y +
                ";parent_sheet_index=" + row.Value.ParentSheetIndex +
                ";item_id=" + row.Value.ItemId +
                ";qualified_item_id=" + row.Value.QualifiedItemId
            : "house_plant=missing";
    }
}
