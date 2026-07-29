using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupForageSourceFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.forage_source[target].ready=true",
                "target_tile=missing",
                "target_tile_required");
        }

        return request.RuleKey switch
        {
            "bush" => ExecuteSetupBushSourceFixture(request),
            "ginger" => ExecuteSetupGingerSourceFixture(request),
            _ => BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.forage_source[target].ready=true",
                "rule_key=" + request.RuleKey,
                "forage_source_fixture_rule_key_invalid")
        };
    }

    private TrainingExecutionResult ExecuteSetupBushSourceFixture(
        TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        if (!TryResolveForageFixtureLocation(
                request,
                out var location,
                out var target,
                out var reason))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.large_terrain_features[target].bush_harvest_status=ready",
                "location_or_target=invalid",
                reason);
        }

        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 16;
        ClearForageFixtureArea(location, target, 2, 1);
        var before = ForageFixtureObservedEffect(location, target);
        var bush = new Bush(
            new Vector2(target.X, target.Y),
            1,
            location);
        bush.townBush.Value = false;
        bush.tileSheetOffset.Value = 1;
        bush.shakeTimer = 0f;
        bush.setUpSourceRect();
        location.largeTerrainFeatures.Add(bush);
        var moved = MoveFixtureFarmerToLocationAdjacent(
            location,
            target,
            out var stand,
            out var moveReason);
        var projection = ProjectBushHarvest(location, bush);
        var verified = moved &&
            string.Equals(projection.Status, "ready", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(projection.QualifiedItemId) &&
            AreAdjacent(stand, target) &&
            !bush.getBoundingBox().Contains(
                stand.X * Game1.tileSize + Game1.tileSize / 2,
                stand.Y * Game1.tileSize + Game1.tileSize / 2);

        return ForageFixtureResult(
            request,
            started,
            location,
            target,
            stand,
            before,
            ForageFixtureObservedEffect(location, target),
            "bush",
            projection.QualifiedItemId,
            verified,
            verified ? "isolated_native_bush_ready" : moved ? "fixture_bush_not_ready" : moveReason);
    }

    private TrainingExecutionResult ExecuteSetupGingerSourceFixture(
        TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        if (!TryResolveForageFixtureLocation(
                request,
                out var location,
                out var target,
                out var reason))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_forage_source_fixture",
                "current_location.terrain_features[target].is_ginger=true",
                "location_or_target=invalid",
                reason);
        }

        ClearForageFixtureArea(location, target, 1, 1);
        EnsureFixtureInventoryCapacity(Game1.player);
        var hoe = Game1.player.Items
            .OfType<Hoe>()
            .FirstOrDefault(tool => tool.GetType() == typeof(Hoe));
        if (hoe is null)
        {
            hoe = new Hoe();
            InstallFixtureItem(Game1.player, hoe);
        }
        Game1.player.Stamina = Math.Max(Game1.player.Stamina, 200f);

        var before = ForageFixtureObservedEffect(location, target);
        var tile = new Vector2(target.X, target.Y);
        var dirt = new HoeDirt(0, location)
        {
            crop = new Crop(
                forageCrop: true,
                Crop.forageCrop_gingerID,
                target.X,
                target.Y,
                location)
        };
        location.terrainFeatures[tile] = dirt;
        var moved = MoveFixtureFarmerToLocationAdjacent(
            location,
            target,
            out var stand,
            out var moveReason);
        var verified = moved &&
            IsExactGinger(location, tile, out _) &&
            Game1.player.Items.OfType<Hoe>().Any(tool => tool.GetType() == typeof(Hoe)) &&
            AreAdjacent(stand, target);

        return ForageFixtureResult(
            request,
            started,
            location,
            target,
            stand,
            before,
            ForageFixtureObservedEffect(location, target),
            "ginger",
            GingerQualifiedItemId,
            verified,
            verified ? "isolated_native_ginger_ready" : moved ? "fixture_ginger_not_ready" : moveReason);
    }

    private static bool TryResolveForageFixtureLocation(
        TrainingExecutionRequest request,
        out GameLocation location,
        out Point target,
        out string reason)
    {
        var locationId = string.IsNullOrWhiteSpace(request.LocationId)
            ? "Forest"
            : request.LocationId;
        location = Game1.getLocationFromName(locationId)!;
        target = new Point(
            request.TargetTileX!.Value,
            request.TargetTileY!.Value);
        reason = string.Empty;
        if (location is null)
        {
            reason = "fixture_location_not_found";
            return false;
        }
        if (!IsTileOnMap(location, target))
        {
            reason = "fixture_target_tile_not_on_map";
            return false;
        }
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        return true;
    }

    private static void ClearForageFixtureArea(
        GameLocation location,
        Point target,
        int width,
        int height)
    {
        var bounds = new Rectangle(
            target.X * Game1.tileSize,
            target.Y * Game1.tileSize,
            width * Game1.tileSize,
            height * Game1.tileSize);
        for (var x = target.X; x < target.X + width; x++)
        {
            for (var y = target.Y; y < target.Y + height; y++)
            {
                var tile = new Vector2(x, y);
                location.objects.Remove(tile);
                location.terrainFeatures.Remove(tile);
            }
        }
        foreach (var feature in location.largeTerrainFeatures
            .Where(candidate => candidate.getBoundingBox().Intersects(bounds))
            .ToList())
        {
            location.largeTerrainFeatures.Remove(feature);
        }
    }

    private static TrainingExecutionResult ForageFixtureResult(
        TrainingExecutionRequest request,
        string started,
        GameLocation location,
        Point target,
        Point stand,
        string before,
        string after,
        string kind,
        string qualifiedItemId,
        bool verified,
        string reason)
    {
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
            PrimitiveVerificationReasons = new[] { reason, "source_kind=" + kind, "qualified_item_id=" + qualifiedItemId },
            RequestedEffect = "current_location.forage_source[" + target.X + "," + target.Y + "].ready=true",
            ObservedEffect = after + ";stand_tile=" + stand.X + "," + stand.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { reason },
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.forage_source[" + target.X + "," + target.Y + "]",
                        Before = before,
                        After = after
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string ForageFixtureObservedEffect(
        GameLocation location,
        Point target)
    {
        var tile = new Vector2(target.X, target.Y);
        var bush = location.largeTerrainFeatures
            .OfType<Bush>()
            .FirstOrDefault(candidate =>
                (int)candidate.Tile.X == target.X &&
                (int)candidate.Tile.Y == target.Y);
        var ginger = IsExactGinger(location, tile, out _);
        return "location=" + location.NameOrUniqueName +
            ";target=" + target.X + "," + target.Y +
            ";bush_ready=" + (bush?.readyForHarvest() == true).ToString().ToLowerInvariant() +
            ";ginger_ready=" + ginger.ToString().ToLowerInvariant();
    }
}
