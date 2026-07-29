using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupClearObstacle(
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
                "debug_setup_clear_obstacle",
                "current_location.obstacle[target]=native_fixture",
                ClearObstacleObservedEffect(null),
                "target_tile_required");
        }

        var fixtureKind = string.IsNullOrWhiteSpace(request.RuleKey)
            ? "grass"
            : request.RuleKey;
        if (fixtureKind is not (
            "grass" or "twig" or "seed_spot" or "artifact_spot"))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_clear_obstacle",
                "current_location.obstacle[target]=native_fixture",
                "rule_key=" + fixtureKind,
                "setup_clear_obstacle_rule_key_invalid");
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        if (!CanClearRouteObstacles(Game1.currentLocation) ||
            ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            MoveFixtureFarmerToFarmAdjacent(target);
        }

        var location = Game1.currentLocation;
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_clear_obstacle",
                "current_location.obstacle[" +
                    target.X + "," + target.Y + "]=native_fixture",
                ClearObstacleObservedEffect(target),
                "setup_clear_obstacle_location_not_whitelisted");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var tile = new Vector2(target.X, target.Y);
        var before = ObstacleLabel(location, target);
        location.terrainFeatures.Remove(tile);
        location.objects.Remove(tile);
        if (fixtureKind == "grass")
        {
            location.terrainFeatures[tile] =
                new Grass(Grass.springGrass, 4);
        }
        else
        {
            var qualifiedItemId = fixtureKind switch
            {
                "twig" => "(O)294",
                "seed_spot" => "(O)SeedSpot",
                _ => "(O)590"
            };
            var createdObstacle =
                ItemRegistry.Create<StardewValley.Object>(
                    qualifiedItemId);
            createdObstacle.TileLocation = tile;
            createdObstacle.Location = location;
            location.objects[tile] = createdObstacle;
            EnsureClearObstacleFixtureTool(
                fixtureKind == "twig" ? "axe" : "hoe");
            if (fixtureKind == "artifact_spot")
            {
                var activeHoe = Game1.player.Items
                    .OfType<Hoe>()
                    .First();
                Game1.player.CurrentToolIndex =
                    Game1.player.Items.IndexOf(activeHoe);
            }
        }

        var after = ObstacleLabel(location, target);
        var verified = fixtureKind == "grass"
            ? location.terrainFeatures.TryGetValue(
                tile,
                out var feature) &&
                feature is Grass
            : location.objects.TryGetValue(
                tile,
                out var observedObstacle) &&
                observedObstacle.GetType() ==
                    typeof(StardewValley.Object) &&
                fixtureKind switch
                {
                    "twig" => observedObstacle.IsTwig(),
                    "seed_spot" =>
                        observedObstacle.QualifiedItemId ==
                        "(O)SeedSpot",
                    _ => observedObstacle.QualifiedItemId == "(O)590"
                };
        var fixtureReason =
            "isolated_runtime_fixture_" +
            fixtureKind +
            "_obstacle";
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
            PrimitiveKind = "debug_setup_clear_obstacle",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { fixtureReason }
                : new[]
                {
                    "fixture_" +
                    fixtureKind +
                    "_obstacle_not_visible"
                },
            RequestedEffect =
                "current_location.obstacle[" +
                target.X + "," + target.Y + "]=" +
                fixtureKind,
            ObservedEffect =
                "before=" + before +
                ";after=" + after +
                ";fixture_kind=" + fixtureKind,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "fixture_" +
                    fixtureKind +
                    "_obstacle_not_visible"
                },
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path =
                        "current_location.obstacle[" +
                        target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                }
            }
        };
    }

    private static void EnsureClearObstacleFixtureTool(
        string requiredToolKind)
    {
        var hasTool = requiredToolKind == "axe"
            ? Game1.player.Items.OfType<Axe>().Any()
            : Game1.player.Items.OfType<Hoe>().Any();
        if (hasTool)
        {
            return;
        }

        EnsureFixtureInventoryCapacity(Game1.player);
        InstallFixtureItem(
            Game1.player,
            requiredToolKind == "axe"
                ? new Axe()
                : new Hoe());
    }
}
