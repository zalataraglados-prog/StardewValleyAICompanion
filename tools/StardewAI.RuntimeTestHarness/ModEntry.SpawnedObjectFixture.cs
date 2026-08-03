using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSpawnedObjectFixture(
        TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var profile = string.IsNullOrWhiteSpace(request.FixtureSpawnedObjectProfile)
            ? "ordinary"
            : request.FixtureSpawnedObjectProfile;
        if (profile is not (
            "ordinary" or
            "botanist" or
            "gatherer_duplicate" or
            "special_724519" or
            "farm_interior"))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.objects[target].spawned_object_pickup_status=ready",
                "fixture_spawned_object_profile=" + profile,
                "fixture_spawned_object_profile_unknown");
        }

        var location = ResolveSpawnedObjectFixtureLocation(request, profile);
        if (location is null || location.Map?.Layers.Count is null or 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.objects[target].spawned_object_pickup_status=ready",
                "location=missing",
                "spawned_object_fixture_location_unavailable");
        }

        ConfigureSpawnedObjectFixtureProfessions(profile);
        EnsureFixtureInventoryCapacity(Game1.player);
        if (FirstEmptyInventorySlot(Game1.player) < 0)
        {
            Game1.player.Items[Math.Max(0, Game1.player.MaxItems - 1)] = null;
        }

        var requestedTarget = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        if (!TryFindSpawnedObjectFixtureTarget(
                location,
                requestedTarget,
                profile == "gatherer_duplicate",
                out var target,
                out var stand))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.objects[target].spawned_object_pickup_status=ready",
                "location=" + location.NameOrUniqueName,
                "spawned_object_fixture_no_legal_target");
        }

        ClearForageFixtureArea(location, target, 1, 1);
        var item = ItemRegistry.Create<StardewValley.Object>("(O)16");
        item.IsSpawnedObject = true;
        item.Stack = 1;
        if (profile == "special_724519")
        {
            item.SpecialVariable = 724519;
        }
        location.objects[target.ToVector2()] = item;
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        Game1.player.Position = new Vector2(stand.X * Game1.tileSize, stand.Y * Game1.tileSize);
        Game1.player.faceDirection(DirectionTo(stand, target));

        var random = Utility.CreateDaySaveRandom(target.X, target.Y * 777f);
        var expectedQuality = location.GetHarvestSpawnedObjectQuality(
            Game1.player,
            true,
            target.ToVector2(),
            random);
        var farmInterior = location.isFarmBuildingInterior();
        var gathererDuplicate = Game1.player.professions.Contains(13) &&
            random.NextDouble() < 0.2 &&
            !farmInterior;
        var expectedQuantity = gathererDuplicate ? 2 : 1;
        var expectedForagingExperience = farmInterior
            ? 0
            : profile == "special_724519"
                ? 2
                : 7;
        if (gathererDuplicate)
        {
            expectedForagingExperience += 7;
        }
        var expectedFarmingExperience = farmInterior
            ? 5
            : profile == "special_724519"
                ? 3
                : 0;
        var verified = location.objects.TryGetValue(target.ToVector2(), out var observed) &&
            ReferenceEquals(observed, item) &&
            observed.IsSpawnedObject &&
            AreAdjacent(Game1.player.TilePoint, target);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_forage_source_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = new[]
            {
                "spawned_object_profile=" + profile,
                "stand_tile=" + stand.X + "," + stand.Y,
                "expected_quantity=" + expectedQuantity,
                "expected_quality=" + expectedQuality,
                "expected_foraging_experience=" + expectedForagingExperience,
                "expected_farming_experience=" + expectedFarmingExperience
            },
            RequestedEffect = "current_location.objects[" + target.X + "," + target.Y + "].spawned_object_pickup_status=ready",
            ObservedEffect = "location=" + location.NameOrUniqueName +
                ";target=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";qualified_item_id=" + item.QualifiedItemId +
                ";spawned=" + item.IsSpawnedObject.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "spawned_object_fixture_install_mismatch" },
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "]",
                        Before = "fixture_cleared",
                        After = item.QualifiedItemId
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static GameLocation? ResolveSpawnedObjectFixtureLocation(
        TrainingExecutionRequest request,
        string profile)
    {
        if (profile != "farm_interior")
        {
            var locationId = string.IsNullOrWhiteSpace(request.LocationId)
                ? "Forest"
                : request.LocationId;
            return Game1.getLocationFromName(locationId);
        }

        return Game1.getFarm().buildings
            .Select(building => building.GetIndoors())
            .OfType<AnimalHouse>()
            .FirstOrDefault()
            ?? new AnimalHouse("Maps\\Coop", "SpawnedObjectFixtureAnimalHouse");
    }

    private static void ConfigureSpawnedObjectFixtureProfessions(string profile)
    {
        Game1.player.professions.Remove(13);
        Game1.player.professions.Remove(16);
        if (profile == "botanist")
        {
            Game1.player.professions.Add(16);
        }
        else if (profile == "gatherer_duplicate")
        {
            Game1.player.professions.Add(13);
        }
    }

    private static bool TryFindSpawnedObjectFixtureTarget(
        GameLocation location,
        Point requested,
        bool requireGathererDuplicate,
        out Point target,
        out Point stand)
    {
        var layer = location.Map.Layers[0];
        var candidates = new[] { requested }
            .Concat(Enumerable.Range(1, Math.Max(0, layer.LayerWidth - 2))
                .SelectMany(x => Enumerable.Range(1, Math.Max(0, layer.LayerHeight - 2))
                    .Select(y => new Point(x, y))))
            .Distinct();
        foreach (var candidate in candidates)
        {
            if (!IsTileOnMap(location, candidate))
            {
                continue;
            }
            var adjacent = Neighbors(candidate)
                .FirstOrDefault(tile =>
                    IsTileOnMap(location, tile) &&
                    IsTileWalkable(location, tile) &&
                    !IsTileOccupiedByCharacter(location, tile));
            if (adjacent == Point.Zero)
            {
                continue;
            }
            if (requireGathererDuplicate)
            {
                var random = Utility.CreateDaySaveRandom(candidate.X, candidate.Y * 777f);
                location.GetHarvestSpawnedObjectQuality(
                    Game1.player,
                    true,
                    candidate.ToVector2(),
                    random);
                if (random.NextDouble() >= 0.2)
                {
                    continue;
                }
            }
            target = candidate;
            stand = adjacent;
            return true;
        }

        target = Point.Zero;
        stand = Point.Zero;
        return false;
    }
}
