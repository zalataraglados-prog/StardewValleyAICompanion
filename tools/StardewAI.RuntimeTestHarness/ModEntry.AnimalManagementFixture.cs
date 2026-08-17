using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupAnimalManagement(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var farm = Game1.getFarm();
        Game1.exitActiveMenu();
        Game1.player.forceCanMove();
        Game1.player.Halt();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.timeOfDay = 1000;
        var sourceReady = EnsureAnimalManagementFixtureHome(
            farm, out var sourceHome, out var sourceHouse, out var sourceReason);
        var targetReady = EnsureAnimalManagementFixtureHome(
            farm, out var targetHome, out var targetHouse, out var targetReason);
        if (!sourceReady || !targetReady)
        {
            return BlockedWithPrimitive(request, "debug_setup_animal_management",
                "four_animals_and_two_compatible_homes=ready", "fixture_home=unavailable",
                "animal_management_fixture_home_unavailable:" + sourceReason + ":" + targetReason);
        }
        if (ReferenceEquals(sourceHome, targetHome) ||
            !TryFindAnimalManagementFixtureTiles(farm, 4, out var targetTiles))
        {
            return BlockedWithPrimitive(request, "debug_setup_animal_management",
                "four_animals_and_two_compatible_homes=ready", "fixture_tiles=unavailable",
                "animal_management_fixture_tiles_unavailable");
        }

        var names = new[]
        {
            "AIMRename",
            "AIMToggle",
            "AIMMove",
            "AIMSell"
        };
        var animals = new List<FarmAnimal>();
        for (var index = 0; index < names.Length; index++)
        {
            var animalId = unchecked(Game1.player.UniqueMultiplayerID + DateTime.UtcNow.Ticks + index);
            while (farm.animals.ContainsKey(animalId))
            {
                animalId++;
            }
            var animal = new FarmAnimal("White Cow", animalId, Game1.player.UniqueMultiplayerID);
            animal.Name = names[index];
            animal.displayName = names[index];
            animal.age.Value = animal.GetAnimalData()?.DaysToMature ?? 99;
            animal.wasPet.Value = index != 0;
            animal.allowReproduction.Value = true;
            sourceHouse.adoptAnimal(animal);
            sourceHouse.animals.Remove(animal.myID.Value);
            animal.currentLocation = farm;
            animal.Position = new Vector2(targetTiles[index].X * Game1.tileSize, targetTiles[index].Y * Game1.tileSize);
            animal.Position += new Vector2(
                (targetTiles[index].X - animal.TilePoint.X) * Game1.tileSize,
                (targetTiles[index].Y - animal.TilePoint.Y) * Game1.tileSize);
            animal.pauseTimer = int.MaxValue;
            animal.Halt();
            farm.animals[animal.myID.Value] = animal;
            animals.Add(animal);
        }

        if (!Game1.player.Items.Any(item => item is null or StardewValley.Tool))
        {
            Game1.player.Items[^1] = null!;
        }
        var moved = MoveFixtureFarmerToFarmAdjacent(targetTiles[0], out var stand, out var moveReason);
        var verified = moved && animals.All(animal =>
                farm.animals.TryGetValue(animal.myID.Value, out var current) &&
                ReferenceEquals(current, animal) && ReferenceEquals(animal.home, sourceHome) && animal.isAdult()) &&
            sourceHouse.animalsThatLiveHere.Count >= animals.Count && targetHouse.animalsThatLiveHere.Count == 0 &&
            animals[1].CanHavePregnancy() && animals.All(animal => animal.CanLiveIn(targetHome));
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
            PrimitiveKind = "debug_setup_animal_management",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_fixture_four_native_adult_animals_ready",
                    "isolated_fixture_two_compatible_native_homes_ready",
                    "rename_fixture_requires_initial_native_pet",
                    "stand_tile=" + stand.X + "," + stand.Y
                }
                : new[] { "animal_management_fixture_postcondition_mismatch", moveReason },
            RequestedEffect = "four_animals_and_two_compatible_homes=ready",
            ObservedEffect = "animals=" + string.Join(",", animals.Select(animal => animal.displayName + "@" + animal.TilePoint)) +
                ";source=" + sourceHome.buildingType.Value + "@" + sourceHome.tileX.Value + "," + sourceHome.tileY.Value +
                ";target=" + targetHome.buildingType.Value + "@" + targetHome.tileX.Value + "," + targetHome.tileY.Value,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "animal_management_fixture_postcondition_mismatch:" + moveReason }
        };
    }

    private static bool EnsureAnimalManagementFixtureHome(
        GameLocation farm,
        out Building home,
        out AnimalHouse house,
        out string reason)
    {
        home = new Building("Barn", Vector2.Zero);
        home.FinishConstruction(onGameStart: true);
        home.LoadFromBuildingData(home.GetData(), forUpgrade: false, forConstruction: true);
        if (!TryFindFixtureBuildingTile(farm, home, out var tile))
        {
            house = null!;
            reason = "building_tile_unavailable";
            return false;
        }
        home.tileX.Value = tile.X;
        home.tileY.Value = tile.Y;
        home.load();
        farm.buildings.Add(home);
        house = home.GetIndoors() as AnimalHouse ?? null!;
        reason = house is null ? "animal_house_unavailable" : string.Empty;
        return house is not null;
    }

    private static bool TryFindAnimalManagementFixtureTiles(
        GameLocation farm,
        int count,
        out Point[] tiles)
    {
        var layer = farm.Map?.Layers.FirstOrDefault();
        var width = layer?.LayerWidth ?? 0;
        var height = layer?.LayerHeight ?? 0;
        const int fixtureWidth = 17;
        const int fixtureHeight = 5;
        for (var y = 3; y <= height - fixtureHeight - 3; y++)
        {
            for (var x = 3; x <= width - fixtureWidth - 3; x++)
            {
                var areaTiles = Enumerable.Range(y, fixtureHeight)
                    .SelectMany(tileY => Enumerable.Range(x, fixtureWidth)
                        .Select(tileX => new Point(tileX, tileY)))
                    .ToArray();
                if (areaTiles.Any(tile =>
                    farm.getBuildingAt(new Vector2(tile.X, tile.Y)) is not null ||
                    !farm.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport)))
                {
                    continue;
                }
                foreach (var tile in areaTiles)
                {
                    var vector = new Vector2(tile.X, tile.Y);
                    farm.objects.Remove(vector);
                    farm.terrainFeatures.Remove(vector);
                }
                var pixelArea = new Rectangle(
                    x * Game1.tileSize, y * Game1.tileSize,
                    fixtureWidth * Game1.tileSize, fixtureHeight * Game1.tileSize);
                foreach (var clump in farm.resourceClumps
                    .Where(clump => clump.getBoundingBox().Intersects(pixelArea)).ToList())
                {
                    farm.resourceClumps.Remove(clump);
                }
                if (areaTiles.Any(tile => !IsTileWalkable(farm, tile) || IsTileOccupiedByCharacter(farm, tile)))
                {
                    continue;
                }
                tiles = Enumerable.Range(0, count)
                    .Select(index => new Point(x + 1 + index * 4, y + 2))
                    .ToArray();
                return true;
            }
        }
        tiles = Array.Empty<Point>();
        return false;
    }
}
