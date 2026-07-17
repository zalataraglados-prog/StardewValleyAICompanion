using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartBreakContainer(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", "mining.objects[target].is_container=false", "target=missing", "break_container_target_tile_required"));
            return;
        }

        var mine = Game1.currentLocation as MineShaft;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        var requested = "mining.objects[" + target.X + "," + target.Y + "].is_container=false;native_input=use_tool";
        if (mine is null || !mine.objects.TryGetValue(targetVector, out var obj) || obj is not BreakableContainer container)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), "break_container_target_not_found"));
            return;
        }

        var tool = BestContainerTool();
        if (tool is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), "break_container_heavy_hitter_unavailable"));
            return;
        }
        var path = BuildAdjacentToolPath(mine, target, request.MaxMovementTiles ?? 512, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_container", requested, BreakContainerObservedEffect(target), pathReason));
            return;
        }

        activeBreakContainer = new ActiveBreakContainer(
            pending,
            mine,
            target,
            path,
            container,
            tool,
            ReadBreakableContainerHealth(container) ?? 3,
            Math.Clamp(request.MaxCrops, 1, 64),
            request.RestoreSlotIndex ?? Game1.player.CurrentToolIndex,
            requested);
    }

    private TrainingExecutionResult ExecuteSetupBreakableContainer(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(request, "debug_setup_breakable_container", "mining.objects[target].is_container=true", "location=not_mineshaft", "setup_breakable_container_requires_mineshaft");
        }

        var start = Game1.player.TilePoint;
        var candidates = Enumerable.Range(1, 8)
            .SelectMany(radius => Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offset => new[]
                {
                    new Point(start.X + offset, start.Y - radius),
                    new Point(start.X + offset, start.Y + radius),
                    new Point(start.X - radius, start.Y + offset),
                    new Point(start.X + radius, start.Y + offset)
                }))
            .Distinct()
            .Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile))
            .Where(tile => !mine.objects.ContainsKey(new Vector2(tile.X, tile.Y)) && !IsTileOccupiedByCharacter(mine, tile))
            .Where(tile => Neighbors(tile).Any(stand => IsTileOnMap(mine, stand) && IsTileWalkable(mine, stand)))
            .OrderBy(tile => ManhattanDistance(start, tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArray();
        var target = candidates.FirstOrDefault();
        if (target == default)
        {
            return BlockedWithPrimitive(request, "debug_setup_breakable_container", "mining.objects[target].is_container=true", "candidate=missing", "setup_breakable_container_no_reachable_tile");
        }

        var tileVector = new Vector2(target.X, target.Y);
        var beforeCount = mine.objects.Count();
        var container = BreakableContainer.GetBarrelForMines(tileVector, mine);
        mine.objects[tileVector] = container;
        var health = ReadBreakableContainerHealth(container);
        var verified = mine.objects.TryGetValue(tileVector, out var observed) && ReferenceEquals(observed, container) && health.HasValue;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_breakable_container",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified ? new[] { "native_breakable_container_fixture_present", "health=" + health } : new[] { "breakable_container_fixture_missing" },
            RequestedEffect = "mining.objects[" + target.X + "," + target.Y + "].is_container=true",
            ObservedEffect = BreakContainerObservedEffect(target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "setup_breakable_container_fixture_mismatch" },
            ChangedFacts = verified
                ? new[] { new SimulatedFactChange { Path = "mining.objects.count", Before = beforeCount.ToString(), After = mine.objects.Count().ToString() } }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupMiningCombatFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "location=not_mineshaft", "setup_mining_combat_fixture_requires_mineshaft");
        }

        var fixtureKind = string.Equals(request.TargetName, "explosive_ammo", StringComparison.Ordinal)
            ? "explosive_ammo"
            : "mummy_chain";
        var target = FindMiningCombatFixtureTarget(
            mine,
            requireClearProjectilePath: fixtureKind == "explosive_ammo",
            requireBombEscape: fixtureKind == "mummy_chain");
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "candidate=missing", "setup_mining_combat_fixture_no_reachable_tile");
        }

        foreach (var monster in mine.characters.OfType<Monster>().ToArray())
        {
            mine.characters.Remove(monster);
        }
        ClearMiningFixtureArea(mine, target.Value, radius: 4);
        var bombEscape = fixtureKind == "mummy_chain"
            ? FindMiningCombatFixtureBombEscape(mine, target.Value)
            : null;
        if (fixtureKind == "mummy_chain" && !bombEscape.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_mining_combat_fixture",
                "mining.combat_fixture=ready", "bomb_escape=missing", "setup_mining_combat_fixture_no_bomb_escape");
        }
        EnsureFixtureInventoryCapacity(Game1.player);

        Monster targetMonster;
        int weaponSlot;
        int consumableSlot;
        if (fixtureKind == "explosive_ammo")
        {
            targetMonster = new GreenSlime(target.Value.ToVector2() * Game1.tileSize, mine.mineLevel);
            var slingshot = new Slingshot("34");
            var ammo = new StardewValley.Object("441", 99);
            slingshot.attach(ammo);
            weaponSlot = InstallFixtureItem(Game1.player, slingshot);
            consumableSlot = weaponSlot;
            var playerTile = Game1.player.TilePoint;
            var resourceTiles = Math.Abs(target.Value.X - playerTile.X) >= Math.Abs(target.Value.Y - playerTile.Y)
                ? new[] { new Point(target.Value.X, target.Value.Y - 1), new Point(target.Value.X, target.Value.Y + 1) }
                : new[] { new Point(target.Value.X - 1, target.Value.Y), new Point(target.Value.X + 1, target.Value.Y) };
            foreach (var tile in resourceTiles.Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile)))
            {
                var vector = new Vector2(tile.X, tile.Y);
                mine.objects[vector] = new StardewValley.Object("751", 1)
                {
                    MinutesUntilReady = 3,
                    TileLocation = vector
                };
            }
        }
        else
        {
            targetMonster = new Mummy(target.Value.ToVector2() * Game1.tileSize);
            weaponSlot = InstallFixtureItem(Game1.player, new MeleeWeapon("9"));
            consumableSlot = InstallFixtureItem(Game1.player, new StardewValley.Object("286", 20));
        }
        targetMonster.Speed = 0;
        targetMonster.moveTowardPlayerThreshold.Value = -1;
        mine.characters.Add(targetMonster);
        Game1.player.health = Game1.player.maxHealth;
        Game1.player.CurrentToolIndex = weaponSlot;

        var runtimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(targetMonster).ToString("X8");
        var verified = mine.characters.Contains(targetMonster) &&
            weaponSlot >= 0 &&
            consumableSlot >= 0;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.Value.X,
            TargetTileY = target.Value.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mining_combat_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_mining_combat_fixture_present", "fixture_kind=" + fixtureKind }
                : new[] { "isolated_mining_combat_fixture_missing" },
            RequestedEffect = "mining.combat_fixture=" + fixtureKind,
            ObservedEffect = "target_identity=" + runtimeIdentity +
                ";target_type=" + (targetMonster.GetType().FullName ?? targetMonster.GetType().Name) +
                ";target_tile=" + target.Value.X + "," + target.Value.Y +
                ";weapon_slot=" + weaponSlot +
                ";consumable_slot=" + consumableSlot,
            CombatTargetRuntimeType = targetMonster.GetType().FullName ?? targetMonster.GetType().Name,
            CombatTargetRuntimeIdentity = runtimeIdentity,
            CombatTargetName = targetMonster.Name,
            BombEscapeTileX = bombEscape?.X,
            BombEscapeTileY = bombEscape?.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "setup_mining_combat_fixture_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "mining.monsters[" + runtimeIdentity + "].present", Before = "false", After = "true" },
                    new SimulatedFactChange { Path = "player.current_tool_index", Before = string.Empty, After = weaponSlot.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindMiningCombatFixtureTarget(
        MineShaft mine,
        bool requireClearProjectilePath,
        bool requireBombEscape)
    {
        var start = Game1.player.TilePoint;
        var candidates = Enumerable.Range(5, 10)
            .SelectMany(radius => Enumerable.Range(-radius, radius * 2 + 1)
                .SelectMany(offset => new[]
                {
                    new Point(start.X + offset, start.Y - radius),
                    new Point(start.X + offset, start.Y + radius),
                    new Point(start.X - radius, start.Y + offset),
                    new Point(start.X + radius, start.Y + offset)
                }))
            .Distinct()
            .Where(tile => IsTileOnMap(mine, tile) && IsTileWalkable(mine, tile))
            .Where(tile => !mine.objects.ContainsKey(tile.ToVector2()) && !IsTileOccupiedByCharacter(mine, tile))
            .Where(tile => !requireClearProjectilePath || HasClearProjectilePath(mine, start, tile))
            .Where(tile => !requireBombEscape || FindMiningCombatFixtureBombEscape(mine, tile).HasValue)
            .Select(tile => new
            {
                Tile = tile,
                Stand = Neighbors(tile)
                    .Where(stand => IsTileOnMap(mine, stand) && IsTileWalkable(mine, stand))
                    .Select(stand => new
                    {
                        Tile = stand,
                        Path = TryBuildTilePath(mine, start, stand, 512, out _, avoidSoftObstacles: true)
                    })
                    .FirstOrDefault(row => row.Path is not null)
            })
            .Where(row => row.Stand is not null)
            .OrderBy(row => ManhattanDistance(start, row.Tile))
            .ThenBy(row => row.Tile.Y)
            .ThenBy(row => row.Tile.X)
            .ToArray();
        return candidates.FirstOrDefault()?.Tile;
    }

    private static Point? FindMiningCombatFixtureBombEscape(MineShaft mine, Point target)
    {
        const int minimumDistance = 4;
        foreach (var direction in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
        {
            var clear = true;
            for (var distance = 1; distance <= minimumDistance; distance++)
            {
                var tile = new Point(target.X + direction.X * distance, target.Y + direction.Y * distance);
                if (!IsTileOnMap(mine, tile) || !IsTileWalkable(mine, tile) || IsTileOccupiedByCharacter(mine, tile))
                {
                    clear = false;
                    break;
                }
            }
            if (clear)
            {
                return new Point(target.X + direction.X * minimumDistance, target.Y + direction.Y * minimumDistance);
            }
        }
        return null;
    }

    private static void EnsureFixtureInventoryCapacity(Farmer player)
    {
        if (player.MaxItems < 36)
        {
            player.increaseBackpackSize(36 - player.MaxItems);
        }
        while (player.Items.Count < player.MaxItems)
        {
            player.Items.Add(null);
        }
    }

    private static int InstallFixtureItem(Farmer player, Item item)
    {
        var slot = FirstEmptyInventorySlot(player);
        if (slot < 0)
        {
            slot = Math.Max(0, Math.Min(player.Items.Count, player.MaxItems) - 1);
        }
        player.Items[slot] = item;
        return slot;
    }

    private void TickBreakContainer()
    {
        if (activeBreakContainer is null)
        {
            return;
        }

        var active = activeBreakContainer;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteBreakContainerBlocked(active, "break_container_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteBreakContainerBlocked(active, "break_container_timeout");
            return;
        }

        var targetVector = new Vector2(active.Target.X, active.Target.Y);
        if (!active.Mine.objects.TryGetValue(targetVector, out var obj))
        {
            active.HeavyHitterAction.RecordRemoval();
            CompleteBreakContainer(active);
            return;
        }
        if (!ReferenceEquals(obj, active.Container) || obj is not BreakableContainer container)
        {
            CompleteBreakContainerBlocked(active, "break_container_runtime_target_drift");
            return;
        }

        if (!active.HeavyHitterAction.ButtonHeld && ImmediateMiningThreat(active.Mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteBreakContainerBlocked(active, "break_container_unreachable_target");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.Mine, next) || IsTileOccupiedByCharacter(active.Mine, next))
            {
                CompleteBreakContainerBlocked(active, "break_container_path_changed");
                return;
            }
            if (Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) < 0.01f)
            {
                active.StuckTicks++;
            }
            else
            {
                active.StuckTicks = 0;
                active.LastPosition = Game1.player.Position;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (active.StuckTicks > 45)
            {
                CompleteBreakContainerBlocked(active, "break_container_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        if (!TryTickNativeHeavyHitterAction(
                active.HeavyHitterAction,
                active.Target,
                ReadBreakableContainerHealth(container),
                out var heavyHitterReason))
        {
            CompleteBreakContainerBlocked(active, "break_container_" + heavyHitterReason);
        }
    }

    private void CompleteBreakContainer(ActiveBreakContainer active)
    {
        ReleaseNativeHeavyHitterAction(active.HeavyHitterAction);
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeBreakContainer = null;
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.Tool.QualifiedItemId,
            ToolUpgradeLevel = active.Tool.UpgradeLevel,
            ToolUseCount = active.SwingCount,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "break_container",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_heavy_hitter_input_removed_container", "released_contents_left_as_game_debris", "native_swing_count=" + active.SwingCount },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = BreakContainerObservedEffect(active.Target) + ";health_sequence=" + string.Join(",", active.ObservedHealth) + ";native_swings=" + active.SwingCount,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.Container.QualifiedItemId + ":health=" + active.HealthBefore, After = "removed" },
                new SimulatedFactChange { Path = "mining.debris.count", Before = active.DebrisCountBefore.ToString(), After = active.Mine.debris.Count.ToString() }
            }
        });
    }

    private void CompleteBreakContainerBlocked(ActiveBreakContainer active, string reason)
    {
        ReleaseNativeHeavyHitterAction(active.HeavyHitterAction);
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeBreakContainer = null;
        var result = BlockedWithPrimitive(
            active.Pending.Request,
            "break_container",
            active.RequestedEffect,
            BreakContainerObservedEffect(active.Target) + ";health_sequence=" + string.Join(",", active.ObservedHealth) + ";native_swings=" + active.SwingCount,
            reason);
        result.ToolQualifiedItemId = active.Tool.QualifiedItemId;
        result.ToolUpgradeLevel = active.Tool.UpgradeLevel;
        result.ToolUseCount = active.SwingCount;
        result.ActualTicks = active.ElapsedTicks;
        result.TrainingImpactScope = "executor_calibration";
        active.Pending.Completion.SetResult(result);
    }

    private static Tool? BestContainerTool()
    {
        return Game1.player.Items.OfType<Tool>()
            .Where(tool => tool.isHeavyHitter())
            .OrderByDescending(tool => tool is MeleeWeapon weapon && weapon.type.Value == MeleeWeapon.club ? 2 : 1)
            .ThenBy(tool => tool is MeleeWeapon weapon ? Math.Max(40, 400 - weapon.speed.Value * 40) : 400)
            .FirstOrDefault();
    }

    private static void RestoreSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = slotIndex;
        }
    }

    private static int? ReadBreakableContainerHealth(BreakableContainer container)
    {
        var netInt = BreakableContainerHealthField?.GetValue(container);
        return netInt?.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(netInt) as int?;
    }

    private static string BreakContainerObservedEffect(Point target)
    {
        var mine = Game1.currentLocation as MineShaft;
        var exists = mine?.objects.TryGetValue(new Vector2(target.X, target.Y), out var obj) == true && obj is BreakableContainer;
        return "location=" + (mine?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";container_present=" + exists.ToString().ToLowerInvariant();
    }

    private static bool ImmediateMiningThreat(MineShaft mine)
    {
        var playerTile = Game1.player.TilePoint;
        return mine.characters.OfType<Monster>()
            .Any(monster => monster.Health > 0 && ManhattanDistance(playerTile, monster.TilePoint) <= 3);
    }
}
