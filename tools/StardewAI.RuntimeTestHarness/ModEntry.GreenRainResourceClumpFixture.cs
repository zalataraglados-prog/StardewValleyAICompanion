using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupGreenRainResourceClumpFixture(
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
                "debug_setup_green_rain_resource_clump",
                "current_location.resource_clumps[target].clear_kind=green_rain_bush",
                "target_tile=missing",
                "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var anchor = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var bounds = new Rectangle(
            anchor.X * Game1.tileSize,
            anchor.Y * Game1.tileSize,
            2 * Game1.tileSize,
            2 * Game1.tileSize);

        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        for (var x = anchor.X; x < anchor.X + 2; x++)
        {
            for (var y = anchor.Y; y < anchor.Y + 2; y++)
            {
                var tile = new Vector2(x, y);
                farm.objects.Remove(tile);
                farm.terrainFeatures.Remove(tile);
            }
        }

        foreach (var existing in farm.resourceClumps
            .Where(clump => clump.getBoundingBox().Intersects(bounds))
            .ToList())
        {
            farm.resourceClumps.Remove(existing);
        }

        EnsureFixtureInventoryCapacity(Game1.player);
        var axe = Game1.player.Items
            .OfType<Axe>()
            .OrderByDescending(tool => tool.UpgradeLevel)
            .FirstOrDefault();
        if (axe is null)
        {
            axe = new Axe();
            InstallFixtureItem(Game1.player, axe);
        }

        var before = GreenRainResourceClumpObservedEffect(farm, anchor);
        var clump = new ResourceClump(
            ResourceClump.greenRainBush1Index,
            2,
            2,
            new Vector2(anchor.X, anchor.Y),
            4,
            "TileSheets\\Objects_2");
        farm.resourceClumps.Add(clump);
        var moved = MoveFixtureFarmerToLocationAdjacent(
            farm,
            anchor,
            out var stand,
            out var moveReason);
        var after = GreenRainResourceClumpObservedEffect(farm, anchor);
        var verified = moved &&
            farm.resourceClumps.Any(candidate =>
                ReferenceEquals(candidate, clump) &&
                candidate.GetType() == typeof(ResourceClump) &&
                candidate.IsGreenRainBush() &&
                candidate.width.Value == 2 &&
                candidate.height.Value == 2) &&
            Game1.player.Items.OfType<Axe>().Any() &&
            !ResourceClumpContainsTile(clump, stand) &&
            AreAdjacent(stand, anchor);

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
            PrimitiveKind = "debug_setup_green_rain_resource_clump",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_runtime_fixture_green_rain_resource_clump_present",
                    "fixture_farmer_adjacent_to_resource_clump",
                    "fixture_axe_available"
                }
                : new[] { moved ? "fixture_green_rain_resource_clump_invalid" : moveReason },
            RequestedEffect =
                "current_location.resource_clumps[" + anchor.X + "," + anchor.Y +
                "].clear_kind=green_rain_bush;player.adjacent_to_target=true;player.has_axe=true",
            ObservedEffect = after,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { moved ? "fixture_green_rain_resource_clump_invalid" : moveReason },
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = anchor.X,
            TargetTileY = anchor.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.resource_clumps[" + anchor.X + "," + anchor.Y + "]",
                        Before = before,
                        After = after
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.tile",
                        Before = "unknown",
                        After = stand.X + "," + stand.Y
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string GreenRainResourceClumpObservedEffect(
        GameLocation location,
        Point anchor)
    {
        var clump = location.resourceClumps.FirstOrDefault(candidate =>
            (int)candidate.Tile.X == anchor.X &&
            (int)candidate.Tile.Y == anchor.Y);
        return clump is null
            ? "present=false;location=" + location.NameOrUniqueName
            : "present=true;location=" + location.NameOrUniqueName +
              ";runtime_type=" + clump.GetType().FullName +
              ";parent_sheet_index=" + clump.parentSheetIndex.Value +
              ";width=" + clump.width.Value +
              ";height=" + clump.height.Value +
              ";health=" + clump.health.Value;
    }
}
