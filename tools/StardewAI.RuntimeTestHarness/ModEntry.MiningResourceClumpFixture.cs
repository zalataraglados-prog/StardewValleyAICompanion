using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMiningResourceClumpFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine ||
            !request.ResourceClumpParentSheetIndex.HasValue ||
            !IsSupportedMiningResourceClumpIndex(
                request.ResourceClumpParentSheetIndex.Value))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_mining_resource_clump",
                "mining.resource_clumps[target].executor_status=" +
                    "native_executor_available",
                "location_or_parent_sheet_index=invalid",
                "mining_resource_clump_fixture_requires_loaded_mine_and_" +
                    "supported_parent_sheet_index");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var parentSheetIndex =
            request.ResourceClumpParentSheetIndex.Value;
        if (!TryFindMiningResourceClumpFixtureAnchor(
                mine,
                out var anchor))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_mining_resource_clump",
                "mining.resource_clumps[target].executor_status=" +
                    "native_executor_available",
                "fixture_anchor=unavailable",
                "mining_resource_clump_fixture_no_open_2x2_area");
        }

        var bounds = new Rectangle(
            anchor.X * Game1.tileSize,
            anchor.Y * Game1.tileSize,
            2 * Game1.tileSize,
            2 * Game1.tileSize);
        foreach (var existing in mine.resourceClumps
            .Where(clump => clump.getBoundingBox().Intersects(bounds))
            .ToList())
        {
            mine.resourceClumps.Remove(existing);
        }
        for (var x = anchor.X; x < anchor.X + 2; x++)
        {
            for (var y = anchor.Y; y < anchor.Y + 2; y++)
            {
                var tile = new Vector2(x, y);
                mine.objects.Remove(tile);
                mine.terrainFeatures.Remove(tile);
            }
        }
        foreach (var monster in mine.characters
            .OfType<Monster>()
            .ToList())
        {
            mine.characters.Remove(monster);
        }

        TryResourceClumpRequirement(
            parentSheetIndex,
            out _,
            out var minimumUpgrade);
        EnsureFixtureInventoryCapacity(Game1.player);
        var pickaxe = Game1.player.Items
            .OfType<Pickaxe>()
            .OrderBy(tool => tool.UpgradeLevel)
            .FirstOrDefault();
        if (pickaxe is null)
        {
            pickaxe = new Pickaxe();
            InstallFixtureItem(Game1.player, pickaxe);
        }
        pickaxe.UpgradeLevel = minimumUpgrade;
        pickaxe.additionalPower.Value = 0;
        Game1.player.Stamina = Math.Max(
            Game1.player.Stamina,
            200f);

        var before = MiningResourceClumpFixtureObservedEffect(
            mine,
            anchor);
        var clump = new ResourceClump(
            parentSheetIndex,
            2,
            2,
            new Vector2(anchor.X, anchor.Y));
        mine.resourceClumps.Add(clump);
        var moved = MoveFixtureFarmerOutsideResourceClump(
            mine,
            anchor,
            out var stand,
            out var moveReason);
        var after = MiningResourceClumpFixtureObservedEffect(
            mine,
            anchor);
        var verified = moved &&
            mine.resourceClumps.Any(candidate =>
                ReferenceEquals(candidate, clump)) &&
            pickaxe.UpgradeLevel == minimumUpgrade &&
            pickaxe.additionalPower.Value == 0 &&
            !ResourceClumpContainsTile(clump, stand) &&
            Neighbors(stand).Any(tile =>
                ResourceClumpContainsTile(clump, tile));
        var verificationAudit =
            "moved=" + moved.ToString().ToLowerInvariant() +
            ";present=" + mine.resourceClumps.Any(candidate =>
                ReferenceEquals(candidate, clump))
                .ToString()
                .ToLowerInvariant() +
            ";upgrade=" + pickaxe.UpgradeLevel +
            ";minimum_upgrade=" + minimumUpgrade +
            ";additional_power=" + pickaxe.additionalPower.Value +
            ";stand_inside=" +
                ResourceClumpContainsTile(clump, stand)
                    .ToString()
                    .ToLowerInvariant() +
            ";stand_adjacent=" +
                Neighbors(stand).Any(tile =>
                    ResourceClumpContainsTile(clump, tile))
                    .ToString()
                    .ToLowerInvariant();

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
            PrimitiveKind = "debug_setup_mining_resource_clump",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_native_mining_resource_clump_present",
                    "fixture_farmer_outside_and_adjacent_to_footprint",
                    "fixture_pickaxe_upgrade=" +
                        pickaxe.UpgradeLevel,
                    "fixture_pickaxe_additional_power=0"
                }
                : new[]
                {
                    moved
                        ? "fixture_mining_resource_clump_invalid"
                        : moveReason
                },
            RequestedEffect =
                "mining.resource_clumps[" + anchor.X + "," +
                anchor.Y + "].parent_sheet_index=" +
                parentSheetIndex,
            ObservedEffect = after +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";" + verificationAudit,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    moved
                        ? "fixture_mining_resource_clump_invalid"
                        : moveReason
                },
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = anchor.X,
            TargetTileY = anchor.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "mining.resource_clumps[" + anchor.X + "," +
                            anchor.Y + "]",
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

    private static bool TryFindMiningResourceClumpFixtureAnchor(
        MineShaft mine,
        out Point anchor)
    {
        anchor = Point.Zero;
        if (mine.map?.Layers.FirstOrDefault() is not { } layer)
        {
            return false;
        }

        var playerTile = Game1.player.TilePoint;
        var candidate = Enumerable
            .Range(1, Math.Max(0, layer.LayerWidth - 3))
            .SelectMany(x => Enumerable
                .Range(1, Math.Max(0, layer.LayerHeight - 3))
                .Select(y => new Point(x, y)))
            .Where(tile => MiningResourceClumpFootprintOpen(mine, tile))
            .Where(tile => MiningResourceClumpOutsideStandTiles(tile)
                .Any(stand =>
                    IsTileOnMap(mine, stand) &&
                    IsTileWalkable(mine, stand)))
            .OrderBy(tile => ManhattanDistance(playerTile, tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Cast<Point?>()
            .FirstOrDefault();
        if (!candidate.HasValue)
        {
            return false;
        }
        anchor = candidate.Value;
        return true;
    }

    private static bool MiningResourceClumpFootprintOpen(
        MineShaft mine,
        Point anchor)
    {
        for (var x = anchor.X; x < anchor.X + 2; x++)
        {
            for (var y = anchor.Y; y < anchor.Y + 2; y++)
            {
                var tile = new Point(x, y);
                if (!IsTileOnMap(mine, tile) ||
                    !IsTileWalkable(mine, tile))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static IEnumerable<Point>
        MiningResourceClumpOutsideStandTiles(Point anchor)
    {
        yield return new Point(anchor.X, anchor.Y - 1);
        yield return new Point(anchor.X + 1, anchor.Y - 1);
        yield return new Point(anchor.X, anchor.Y + 2);
        yield return new Point(anchor.X + 1, anchor.Y + 2);
        yield return new Point(anchor.X - 1, anchor.Y);
        yield return new Point(anchor.X - 1, anchor.Y + 1);
        yield return new Point(anchor.X + 2, anchor.Y);
        yield return new Point(anchor.X + 2, anchor.Y + 1);
    }

    private static bool MoveFixtureFarmerOutsideResourceClump(
        MineShaft mine,
        Point anchor,
        out Point stand,
        out string reason)
    {
        var playerTile = Game1.player.TilePoint;
        foreach (var candidate in
            MiningResourceClumpOutsideStandTiles(anchor)
                .Where(tile =>
                    IsTileOnMap(mine, tile) &&
                    IsTileWalkable(mine, tile))
                .OrderBy(tile =>
                    ManhattanDistance(playerTile, tile))
                .ThenBy(tile => tile.Y)
                .ThenBy(tile => tile.X))
        {
            stand = candidate;
            reason = string.Empty;
            Game1.currentLocation = mine;
            Game1.player.currentLocation = mine;
            Game1.player.Position = new Vector2(
                candidate.X * Game1.tileSize,
                candidate.Y * Game1.tileSize);
            var hitTile = Neighbors(candidate).First(tile =>
                tile.X >= anchor.X &&
                tile.X < anchor.X + 2 &&
                tile.Y >= anchor.Y &&
                tile.Y < anchor.Y + 2);
            Game1.player.faceDirection(
                DirectionTo(candidate, hitTile));
            return true;
        }

        stand = Point.Zero;
        reason =
            "fixture_no_collision_safe_resource_clump_perimeter_tile";
        return false;
    }

    private static string MiningResourceClumpFixtureObservedEffect(
        MineShaft mine,
        Point anchor)
    {
        var clump = mine.resourceClumps.FirstOrDefault(candidate =>
            (int)candidate.Tile.X == anchor.X &&
            (int)candidate.Tile.Y == anchor.Y);
        return clump is null
            ? "present=false;location=" + mine.NameOrUniqueName
            : "present=true;location=" + mine.NameOrUniqueName +
              ";parent_sheet_index=" +
              clump.parentSheetIndex.Value +
              ";width=" + clump.width.Value +
              ";height=" + clump.height.Value +
              ";health=" + clump.health.Value;
    }
}
