using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFarmResourceClumpFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            request.ResourceClumpParentSheetIndex is not (
                ResourceClump.stumpIndex or
                ResourceClump.hollowLogIndex))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_farm_resource_clump",
                "farm.resource_clumps[target].clear_kind=resource_stump_or_hollow_log",
                "target_or_parent_sheet_index=invalid",
                "farm_resource_clump_fixture_typed_target_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var anchor = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var parentSheetIndex =
            request.ResourceClumpParentSheetIndex.Value;
        var minimumUpgrade = parentSheetIndex == ResourceClump.stumpIndex
            ? 1
            : 2;
        var clearKind = parentSheetIndex == ResourceClump.stumpIndex
            ? "resource_stump"
            : "hollow_log";
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
        axe.UpgradeLevel = Math.Max(axe.UpgradeLevel, minimumUpgrade);

        var before = FarmResourceClumpFixtureObservedEffect(farm, anchor);
        var clump = new ResourceClump(
            parentSheetIndex,
            2,
            2,
            new Vector2(anchor.X, anchor.Y),
            10);
        farm.resourceClumps.Add(clump);
        var moved = MoveFixtureFarmerToLocationAdjacent(
            farm,
            anchor,
            out var stand,
            out var moveReason);
        var after = FarmResourceClumpFixtureObservedEffect(farm, anchor);
        var verified = moved &&
            farm.resourceClumps.Any(candidate =>
                ReferenceEquals(candidate, clump)) &&
            axe.UpgradeLevel >= minimumUpgrade &&
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
            PrimitiveKind = "debug_setup_farm_resource_clump",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_runtime_fixture_" + clearKind + "_present",
                    "fixture_farmer_adjacent_to_resource_clump",
                    "fixture_axe_upgrade=" + axe.UpgradeLevel
                }
                : new[]
                {
                    moved
                        ? "fixture_farm_resource_clump_invalid"
                        : moveReason
                },
            RequestedEffect =
                "farm.resource_clumps[" + anchor.X + "," + anchor.Y +
                "].clear_kind=" + clearKind,
            ObservedEffect = after,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    moved
                        ? "fixture_farm_resource_clump_invalid"
                        : moveReason
                },
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = anchor.X,
            TargetTileY = anchor.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "farm.resource_clumps[" + anchor.X + "," +
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

    private static string FarmResourceClumpFixtureObservedEffect(
        GameLocation location,
        Point anchor)
    {
        var clump = location.resourceClumps.FirstOrDefault(candidate =>
            (int)candidate.Tile.X == anchor.X &&
            (int)candidate.Tile.Y == anchor.Y);
        return clump is null
            ? "present=false;location=" + location.NameOrUniqueName
            : "present=true;location=" + location.NameOrUniqueName +
              ";parent_sheet_index=" + clump.parentSheetIndex.Value +
              ";width=" + clump.width.Value +
              ";height=" + clump.height.Value +
              ";health=" + clump.health.Value;
    }
}
