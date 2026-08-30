using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mining;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using xTile.Tiles;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartSetupCalicoStatueFixture(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.CalicoStatueFixtureEffectId.HasValue ||
            request.CalicoStatueFixtureEffectId.Value is < 0 or > 17)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "debug_setup_calico_statue",
                "desert_festival_skull_cavern_calico_statue=ready",
                CalicoStatueFixtureObserved(),
                "calico_statue_fixture_effect_id_0_17_required"));
            return;
        }

        Game1.exitActiveMenu();
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 15;
        if (!Game1.netWorldState.Value.ActivePassiveFestivals.Contains("DesertFestival"))
        {
            Game1.netWorldState.Value.ActivePassiveFestivals.Add("DesertFestival");
        }
        activeCalicoStatueFixture = new ActiveCalicoStatueFixture(
            pending, request.CalicoStatueFixtureEffectId.Value);
        Game1.enterMine(121);
    }

    private void TickCalicoStatueFixture()
    {
        var active = activeCalicoStatueFixture;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (Game1.currentLocation is MineShaft mine && mine.mineLevel == 121 && mine.map is not null)
        {
            CompleteCalicoStatueFixture(active, mine);
            return;
        }
        if (active.ElapsedTicks >= active.MaxTicks)
        {
            activeCalicoStatueFixture = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(
                active.Pending.Request,
                "debug_setup_calico_statue",
                "desert_festival_skull_cavern_calico_statue=ready",
                CalicoStatueFixtureObserved(),
                "calico_statue_fixture_mine_load_timeout"));
        }
    }

    private void CompleteCalicoStatueFixture(ActiveCalicoStatueFixture active, MineShaft mine)
    {
        var request = active.Pending.Request;
        var targetAndStand = FindCalicoStatueFixtureTiles(mine);
        if (!targetAndStand.HasValue || !InstallCalicoStatueFixtureTile(mine, targetAndStand.Value.Target))
        {
            activeCalicoStatueFixture = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "debug_setup_calico_statue",
                "desert_festival_skull_cavern_calico_statue=ready",
                CalicoStatueFixtureObserved(),
                "calico_statue_fixture_tile_unavailable"));
            return;
        }

        var player = Game1.player;
        player.team.calicoStatueEffects.Clear();
        player.team.calicoEggSkullCavernRating.Value = 0;
        player.buffs.Remove("CalicoStatueSpeed");
        player.health = Math.Max(1, player.maxHealth / 2);
        player.Stamina = Math.Max(1f, player.MaxStamina / 2f);
        RemoveCalicoStatueFixtureEggs(mine);

        var averageDailyLuck = player.team.AverageDailyLuck(mine);
        var totalBefore = FindCalicoStatueActivationNumber(active.EffectId, averageDailyLuck);
        if (!totalBefore.HasValue)
        {
            activeCalicoStatueFixture = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "debug_setup_calico_statue",
                "desert_festival_skull_cavern_calico_statue=ready",
                CalicoStatueFixtureObserved(),
                "calico_statue_fixture_seed_not_found"));
            return;
        }

        MineShaft.totalCalicoStatuesActivatedToday = totalBefore.Value;
        mine.calicoStatueSpot.Value = targetAndStand.Value.Target;
        mine.recentlyActivatedCalicoStatue.Value = Point.Zero;
        Game1.player.Position = targetAndStand.Value.Stand.ToVector2() * Game1.tileSize;
        StopAllMovement();

        var actualEffectId = CalicoStatueEffectModel.SelectEffect(
            Utility.CreateDaySaveRandom(totalBefore.Value + 1),
            averageDailyLuck,
            CalicoStatueEffects());
        var verified = actualEffectId == active.EffectId &&
            Utility.GetDayOfPassiveFestival("DesertFestival") > 0 &&
            mine.getMineArea() == MineShaft.desertArea &&
            mine.getTileIndexAt(targetAndStand.Value.Target.X, targetAndStand.Value.Target.Y, "Buildings", "mine") == 284 &&
            Game1.player.TilePoint == targetAndStand.Value.Stand;
        activeCalicoStatueFixture = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_calico_statue",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_desert_festival_skull_cavern_statue_seeded_for_exact_effect" }
                : new[] { "calico_statue_fixture_setup_mismatch" },
            RequestedEffect = "calico_statue_projected_effect_id=" + active.EffectId,
            ObservedEffect = CalicoStatueFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "calico_statue_fixture_setup_mismatch" }
        });
    }

    private static (Point Target, Point Stand)? FindCalicoStatueFixtureTiles(MineShaft mine)
    {
        var existing = mine.calicoStatueSpot.Value;
        if (existing != Point.Zero)
        {
            var existingStand = Neighbors(existing).FirstOrDefault(tile =>
                IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile) && !IsTileOccupiedByCharacter(mine, tile));
            if (existingStand != Point.Zero)
            {
                return (existing, existingStand);
            }
        }

        var layer = mine.Map.GetLayer("Buildings");
        if (layer is null)
        {
            return null;
        }
        var playerTile = Game1.player.TilePoint;
        return Enumerable.Range(2, Math.Max(0, layer.LayerHeight - 4))
            .SelectMany(y => Enumerable.Range(2, Math.Max(0, layer.LayerWidth - 4)), (y, x) => new Point(x, y))
            .SelectMany(target => Neighbors(target).Select(stand => (Target: target, Stand: stand)))
            .Where(pair => IsTileOnMap(mine, pair.Target) && IsTileOnMap(mine, pair.Stand) &&
                IsTileWalkable(mine, pair.Stand) && !IsTileOccupiedByCharacter(mine, pair.Stand))
            .OrderBy(pair => ManhattanDistance(playerTile, pair.Stand))
            .Select(pair => ((Point Target, Point Stand)?)pair)
            .FirstOrDefault();
    }

    private static bool InstallCalicoStatueFixtureTile(MineShaft mine, Point target)
    {
        var layer = mine.Map.GetLayer("Buildings");
        var tileSheet = mine.Map.GetTileSheet("mine");
        if (layer is null || tileSheet is null || target.X < 0 || target.Y < 0 ||
            target.X >= layer.LayerWidth || target.Y >= layer.LayerHeight)
        {
            return false;
        }
        ClearMiningFixtureArea(mine, target, radius: 2);
        foreach (var monster in mine.characters.OfType<StardewValley.Monsters.Monster>().ToArray())
        {
            if (ManhattanDistance(monster.TilePoint, target) <= 4)
            {
                mine.characters.Remove(monster);
            }
        }
        layer.Tiles[target.X, target.Y] = new StaticTile(layer, tileSheet, BlendMode.Alpha, 284);
        return mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") == 284;
    }

    private static int? FindCalicoStatueActivationNumber(int wantedEffectId, double averageDailyLuck)
    {
        var emptyEffects = new Dictionary<int, int>();
        for (var totalBefore = 0; totalBefore < 200000; totalBefore++)
        {
            if (CalicoStatueEffectModel.SelectEffect(
                    Utility.CreateDaySaveRandom(totalBefore + 1), averageDailyLuck, emptyEffects) == wantedEffectId)
            {
                return totalBefore;
            }
        }
        return null;
    }

    private static void RemoveCalicoStatueFixtureEggs(MineShaft mine)
    {
        var player = Game1.player;
        for (var index = 0; index < player.MaxItems && index < player.Items.Count; index++)
        {
            if (player.Items[index]?.QualifiedItemId == "(O)CalicoEgg")
            {
                player.Items[index] = null;
            }
        }
        for (var index = mine.debris.Count - 1; index >= 0; index--)
        {
            if (mine.debris[index].item?.QualifiedItemId == "(O)CalicoEgg")
            {
                mine.debris.RemoveAt(index);
            }
        }
    }

    private static string CalicoStatueFixtureObserved() =>
        Game1.currentLocation is MineShaft mine
            ? CalicoStatueObservedEffect(mine) + ";festival_day=" + Utility.GetDayOfPassiveFestival("DesertFestival")
            : "current_location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none");
}
