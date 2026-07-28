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
    private void StartCombatMonster(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var terminalState = string.IsNullOrWhiteSpace(request.CombatTerminalState) ? "defeat" : request.CombatTerminalState;
        var requested = "target_monster.terminal_state=" + terminalState + ";native_input=Farmer.FireTool";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) || string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "target=missing_or_incomplete", "combat_target_identity_required"));
            return;
        }

        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "location=not_loaded_mineshaft", "combat_requires_loaded_mineshaft"));
            return;
        }

        var targets = mine.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.Name, request.TargetName, StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "matching_target_count=" + targets.Length, targets.Length == 0 ? "combat_target_not_found_or_moved" : "combat_target_ambiguous"));
            return;
        }
        if (!ValidateQuestSlayTarget(request, targets[0], out var questSlayReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "combat_monster",
                requested,
                "quest_slay_target=drifted",
                questSlayReason));
            return;
        }
        var questResourceReason = ValidateQuestResourceSourceTarget(
            request,
            new[] { request.QualifiedItemId });
        if (!string.IsNullOrWhiteSpace(questResourceReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "combat_monster",
                requested,
                "quest_resource_source=drifted",
                questResourceReason));
            return;
        }
        if (!ValidateSpecialOrderCollectSourceTarget(
                request,
                new[] { request.QualifiedItemId },
                out var specialCollectReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "combat_monster",
                requested,
                "special_order_collect_source=drifted",
                specialCollectReason));
            return;
        }

        var target = targets[0];
        if (terminalState == "knockdown_requires_bomb_finish" && target is not Mummy)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "target=not_mummy", "combat_terminal_state_target_mismatch"));
            return;
        }
        var weapon = ResolveCombatWeapon(target, request.CombatWeaponSlotIndex, request.RequiredWeaponEnchantmentRuntimeType);
        if (weapon is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_monster", requested, "weapon=missing", "combat_melee_weapon_unavailable"));
            return;
        }

        activeCombatMonster = new ActiveCombatMonster(
            pending,
            mine.NameOrUniqueName,
            target,
            weapon,
            Math.Clamp(request.MaxAttacks, 1, 256),
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
            string.Equals(Environment.GetEnvironmentVariable("STARDEWAI_COMBAT_MANUAL_MOVEMENT"), "1", StringComparison.Ordinal),
            terminalState,
            requested);
        Monitor.Log($"Combat lock: {target.Name} [{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target):X8}], health={target.Health}, manual_movement={activeCombatMonster.ManualMovement}.", LogLevel.Info);
    }

    private void TickCombatMonster()
    {
        if (activeCombatMonster is null)
        {
            return;
        }

        var active = activeCombatMonster;
        try
        {
            TickCombatMonsterCore(active);
        }
        catch (Exception ex)
        {
            CompleteCombatMonsterBlocked(active, "combat_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickManualAutoCombat()
    {
        var executorCombatInterrupt = activeMineStone?.CombatInterrupted == true ||
            activeResourceClump?.CombatInterrupted == true ||
            activeBreakContainer?.CombatInterrupted == true ||
            activePickupDebris?.CombatInterrupted == true ||
            activeDescendLadder?.CombatInterrupted == true ||
            activeDescendShaft?.CombatInterrupted == true ||
            activeExitMine?.CombatInterrupted == true;
        var enabled = manualAutoCombatEnabled || executorCombatInterrupt;
        if (!enabled || activeCombatMonster is not null || activeShootMonster is not null || activePlaceBomb is not null ||
            !Context.IsWorldReady || Game1.currentLocation is not MineShaft mine)
        {
            ReleaseManualAutoCombatInput();
            RestoreManualAutoCombatTool();
            manualAutoCombatTarget = null;
            return;
        }

        if (manualAutoCombatInputHeld)
        {
            ReleaseManualAutoCombatInput();
            return;
        }

        var target = manualAutoCombatTarget is { Health: > 0 } lockedTarget &&
            mine.characters.Contains(lockedTarget) &&
            (manualAutoCombatEnabled || ManhattanDistance(Game1.player.TilePoint, lockedTarget.TilePoint) <= 4)
                ? lockedTarget
                : mine.characters.OfType<Monster>()
                    .Where(monster => monster.Health > 0)
                    .Where(monster => manualAutoCombatEnabled || ManhattanDistance(Game1.player.TilePoint, monster.TilePoint) <= 3)
                    .OrderBy(monster => Vector2.DistanceSquared(
                        Game1.player.GetBoundingBox().Center.ToVector2(),
                        monster.GetBoundingBox().Center.ToVector2()))
                    .FirstOrDefault();
        if (target is null)
        {
            StopAllMovement();
            RestoreManualAutoCombatTool();
            manualAutoCombatTarget = null;
            return;
        }

        if (!ReferenceEquals(target, manualAutoCombatTarget))
        {
            manualAutoCombatTarget = target;
            manualAutoCombatTargetHealth = target.Health;
            Monitor.Log($"Manual auto-combat target: {target.Name} [{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target):X8}], health={target.Health}.", LogLevel.Info);
        }
        else if (target.Health < manualAutoCombatTargetHealth)
        {
            manualAutoCombatHitCount++;
            Monitor.Log($"Manual auto-combat hit {manualAutoCombatHitCount}: {target.Name} health {manualAutoCombatTargetHealth}->{target.Health}.", LogLevel.Info);
            manualAutoCombatTargetHealth = target.Health;
        }

        var weapon = BestCombatWeapon(target);
        if (weapon is null)
        {
            RestoreManualAutoCombatTool();
            return;
        }
        if (!IsMonsterWithinCombatReach(target, weapon))
        {
            RestoreManualAutoCombatTool();
            if (executorCombatInterrupt && !manualAutoCombatEnabled)
            {
                MoveTowardCombatTarget(mine, target);
            }
            return;
        }
        if (target.isInvincible() || Game1.player.UsingTool)
        {
            return;
        }

        StopAllMovement();
        var targetCenter = target.GetBoundingBox().Center;
        manualAutoCombatRestoreSlotIndex ??= Game1.player.CurrentToolIndex;
        SelectTool(weapon);
        Game1.player.faceDirection(DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection));
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var reason))
        {
            Monitor.Log($"Manual auto-combat input failed: {reason}.", LogLevel.Error);
            manualAutoCombatEnabled = false;
            return;
        }

        manualAutoCombatInputHeld = true;
        manualAutoCombatAttackCount++;
        Monitor.Log($"Manual auto-combat attack {manualAutoCombatAttackCount}: {target.Name} health={target.Health}.", LogLevel.Info);
    }

    private void MoveTowardCombatTarget(MineShaft mine, Monster target)
    {
        var path = BuildAdjacentToolPath(mine, target.TilePoint, 512, out _);
        if (path is null)
        {
            return;
        }

        var nextIndex = path.FindIndex(tile => tile != Game1.player.TilePoint);
        if (nextIndex < 0)
        {
            return;
        }

        var next = path[nextIndex];
        if (!IsTileWalkable(mine, next) || IsTileOccupiedByCharacter(mine, next))
        {
            return;
        }

        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
    }

    private void ReleaseManualAutoCombatInput()
    {
        if (!manualAutoCombatInputHeld)
        {
            return;
        }

        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        manualAutoCombatInputHeld = false;
    }

    private void RestoreManualAutoCombatTool()
    {
        if (!manualAutoCombatRestoreSlotIndex.HasValue || Game1.player.UsingTool)
        {
            return;
        }

        Game1.player.CurrentToolIndex = manualAutoCombatRestoreSlotIndex.Value;
        manualAutoCombatRestoreSlotIndex = null;
    }

    private void TickCombatMonsterCore(ActiveCombatMonster active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteCombatMonsterBlocked(active, "combat_location_changed_or_world_unavailable");
            return;
        }

        RecordCombatHealth(active);
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteCombatMonsterBlocked(active, "combat_timeout");
            return;
        }

        if (Game1.player.health <= 0)
        {
            CompleteCombatMonsterBlocked(active, "combat_player_defeated");
            return;
        }

        if (active.ManualMovement && active.Target.Health > 0)
        {
            var nearestTarget = mine.characters.OfType<Monster>()
                .Where(monster => monster.Health > 0)
                .OrderBy(monster => Vector2.DistanceSquared(
                    Game1.player.GetBoundingBox().Center.ToVector2(),
                    monster.GetBoundingBox().Center.ToVector2()))
                .FirstOrDefault();
            if (nearestTarget is not null && !ReferenceEquals(nearestTarget, active.Target))
            {
                active.Retarget(nearestTarget);
                Monitor.Log($"Combat retarget: {nearestTarget.Name} [{active.TargetRuntimeIdentity}], health={nearestTarget.Health}.", LogLevel.Info);
            }
        }

        var targetPresent = mine.characters.Contains(active.Target);
        if (active.TerminalState == "knockdown_requires_bomb_finish" &&
            active.Target is Mummy mummy &&
            mummy.reviveTimer.Value > 0)
        {
            CompleteCombatMonster(active, targetDefeated: false, terminalVerificationReason: "native_melee_knocked_down_mummy_for_bomb_finish");
            return;
        }
        if (active.Target.Health <= 0 || !targetPresent)
        {
            if (active.Target.Health <= 0)
            {
                CompleteCombatMonster(active);
            }
            else
            {
                CompleteCombatMonsterBlocked(active, "combat_target_disappeared_without_defeat");
            }
            return;
        }

        if (TrackCombatProgress(active) > 600)
        {
            var detail = string.IsNullOrWhiteSpace(active.LastNoProgressReason) ? "unknown" : active.LastNoProgressReason;
            CompleteCombatMonsterBlocked(active, "combat_no_movement_or_damage_progress:" + detail);
            return;
        }

        var releasedAttackThisTick = false;
        if (active.AttackButtonHeld)
        {
            if (!TryApplySmapiButtonOverride(SButton.C, pressed: false, out var releaseReason))
            {
                CompleteCombatMonsterBlocked(active, releaseReason);
                return;
            }
            active.AttackButtonHeld = false;
            releasedAttackThisTick = true;
        }

        if (TickCombatClearance(active, mine))
        {
            return;
        }

        var targetTile = active.Target.TilePoint;
        if (!IsMonsterWithinCombatReach(active.Target, active.Weapon))
        {
            if (active.ManualMovement)
            {
                return;
            }

            if (AreAdjacent(Game1.player.TilePoint, targetTile))
            {
                ObserveCombatMovement(active);
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteCombatMonsterBlocked(active, "combat_movement_budget_exceeded");
                    return;
                }
                var adjacentTargetCenter = active.Target.GetBoundingBox().Center;
                StartMoving(DirectionToPixel(Game1.player.GetBoundingBox().Center, adjacentTargetCenter, Game1.player.FacingDirection));
                MovePlayerForTick();
                return;
            }

            if (active.PathIndex >= active.Path.Count || ManhattanDistance(active.PathTarget, targetTile) > 4)
            {
                var path = BuildAdjacentToolPath(mine, targetTile, Math.Max(1, active.MaxMovementTiles - active.MovementTiles), out var pathReason, avoidSoftObstacles: true);
                if (path is null)
                {
                    active.PathFailures++;
                    if (active.PathFailures > 120)
                    {
                        CompleteCombatMonsterBlocked(active, "combat_dynamic_path_unavailable:" + pathReason);
                    }
                    return;
                }

                active.Path = path;
                active.PathIndex = 0;
                active.PathTarget = targetTile;
                active.PathFailures = 0;
            }

            if (active.PathIndex >= active.Path.Count)
            {
                return;
            }

            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }

            if (IsTileOccupiedByCharacter(mine, next))
            {
                active.LastNoProgressReason = "combat_next_tile_soft_occupied";
                active.Path.Clear();
                active.PathIndex = 0;
                return;
            }
            if (!IsTileWalkable(mine, next))
            {
                if (BeginCombatClearance(active, mine, next))
                {
                    return;
                }

                active.LastNoProgressReason = "combat_next_tile_hard_blocked";
                active.Path.Clear();
                active.PathIndex = 0;
                return;
            }

            ObserveCombatMovement(active);
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteCombatMonsterBlocked(active, "combat_movement_budget_exceeded");
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (active.StuckTicks > 45)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.StuckTicks = 0;
            }
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        var attackDirection = DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection);
        if (active.Target.isInvincible())
        {
            return;
        }
        if (Game1.player.UsingTool)
        {
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteCombatMonsterBlocked(active, "combat_attack_budget_exceeded");
            return;
        }
        if (releasedAttackThisTick || Game1.activeClickableMenu is not null || Game1.eventUp)
        {
            return;
        }

        SelectTool(active.Weapon);
        Game1.player.faceDirection(attackDirection);
        Game1.player.lastClick = new Vector2(targetCenter.X, targetCenter.Y);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteCombatMonsterBlocked(active, inputReason);
            return;
        }
        active.AttackButtonHeld = true;
        active.AttackCount++;
    }

    private bool BeginCombatClearance(ActiveCombatMonster active, MineShaft mine, Point tile)
    {
        var tool = SelectClearanceTool(mine, tile);
        if (tool is null || !AreAdjacent(Game1.player.TilePoint, tile))
        {
            return false;
        }

        StopAllMovement();
        active.ClearanceTarget = tile;
        active.ClearanceTool = tool;
        active.ClearanceBefore = ObstacleLabel(mine, tile);
        active.ClearanceSwings = 0;
        active.LastNoProgressReason = "combat_clearing_route_obstacle";
        active.Path.Clear();
        active.PathIndex = 0;
        Monitor.Log($"Combat clearance: {active.ClearanceBefore} at {tile.X},{tile.Y} with {tool.GetType().Name}.", LogLevel.Info);
        return true;
    }

    private bool TickCombatClearance(ActiveCombatMonster active, MineShaft mine)
    {
        if (!active.ClearanceTarget.HasValue)
        {
            return false;
        }

        var target = active.ClearanceTarget.Value;
        if (active.ClearanceButtonHeld)
        {
            TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
            active.ClearanceButtonHeld = false;
            return true;
        }

        if (string.Equals(ObstacleLabel(mine, target), "clear", StringComparison.Ordinal))
        {
            active.Pending.MovementClearanceActions++;
            active.Pending.ChangedFacts.Add(new SimulatedFactChange
            {
                Path = "combat.route_clearance[" + target.X + "," + target.Y + "]",
                Before = active.ClearanceBefore,
                After = ObstacleLabel(mine, target)
            });
            active.ClearanceTarget = null;
            active.ClearanceTool = null;
            active.ClearanceBefore = string.Empty;
            active.ClearanceSwings = 0;
            active.LastNoProgressReason = string.Empty;
            active.NoProgressTicks = 0;
            return true;
        }

        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_target_no_longer_adjacent");
            return true;
        }

        var tool = SelectClearanceTool(mine, target);
        if (tool is null)
        {
            CompleteCombatMonsterBlocked(active, "combat_route_obstacle_not_clearable");
            return true;
        }
        if (Game1.player.UsingTool)
        {
            return true;
        }
        if (active.ClearanceSwings >= 64)
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_swing_budget_exceeded");
            return true;
        }

        active.ClearanceTool = tool;
        SelectTool(tool);
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        Game1.player.lastClick = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteCombatMonsterBlocked(active, "combat_clearance_" + inputReason);
            return true;
        }

        active.ClearanceButtonHeld = true;
        active.ClearanceSwings++;
        active.NoProgressTicks = 0;
        return true;
    }

    private static bool IsMonsterWithinCombatReach(Monster target, MeleeWeapon weapon)
    {
        if (AreAdjacent(Game1.player.TilePoint, target.TilePoint))
        {
            return true;
        }

        var playerCenter = Game1.player.GetBoundingBox().Center;
        var targetBox = target.GetBoundingBox();
        var targetCenter = targetBox.Center;
        var reach = weapon.type.Value == MeleeWeapon.dagger ? 64 : 96 + Math.Max(0, weapon.addedAreaOfEffect.Value);
        var deltaX = targetCenter.X - playerCenter.X;
        var deltaY = targetCenter.Y - playerCenter.Y;
        return targetBox.Intersects(Game1.player.GetBoundingBox()) ||
            deltaX * deltaX + deltaY * deltaY <= reach * reach;
    }

    private static int TrackCombatProgress(ActiveCombatMonster active)
    {
        var playerPosition = Game1.player.Position;
        if (Vector2.DistanceSquared(active.LastProgressPosition, playerPosition) >= 0.01f ||
            active.Target.Health < active.LastProgressTargetHealth)
        {
            active.LastProgressPosition = playerPosition;
            active.LastProgressTargetHealth = active.Target.Health;
            active.NoProgressTicks = 0;
        }
        else
        {
            active.NoProgressTicks++;
        }

        return active.NoProgressTicks;
    }

    private static void ObserveCombatMovement(ActiveCombatMonster active)
    {
        var currentPosition = Game1.player.Position;
        if (Vector2.DistanceSquared(active.LastMovementPosition, currentPosition) < 0.01f)
        {
            active.StuckTicks++;
        }
        else
        {
            active.StuckTicks = 0;
        }

        var currentTile = Game1.player.TilePoint;
        if (currentTile != active.LastMovementTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastMovementTile, currentTile);
        }

        active.LastMovementPosition = currentPosition;
        active.LastMovementTile = currentTile;
    }

    private static int DirectionToPixel(Point from, Point to, int fallback)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        if (deltaX == 0 && deltaY == 0)
        {
            return fallback;
        }
        if (Math.Abs(deltaY) >= Math.Abs(deltaX))
        {
            return deltaY < 0 ? 0 : 2;
        }
        return deltaX > 0 ? 1 : 3;
    }

    private void RecordCombatHealth(ActiveCombatMonster active)
    {
        if (active.TargetHealthSequence.Count == 0 || active.TargetHealthSequence[^1] != active.Target.Health)
        {
            var previousHealth = active.TargetHealthSequence.Count > 0 ? active.TargetHealthSequence[^1] : active.Target.Health;
            if (active.TargetHealthSequence.Count > 0 && active.Target.Health < active.TargetHealthSequence[^1])
            {
                active.HitCount++;
                Monitor.Log($"Combat hit {active.HitCount}: {active.TargetName} health {previousHealth}->{active.Target.Health}; attacks={active.AttackCount}.", LogLevel.Info);
            }
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        if (active.PlayerHealthSequence.Count == 0 || active.PlayerHealthSequence[^1] != Game1.player.health)
        {
            Monitor.Log($"Combat player health: {active.PlayerHealthSequence[^1]}->{Game1.player.health}.", LogLevel.Info);
            active.PlayerHealthSequence.Add(Game1.player.health);
        }
    }

    private static MeleeWeapon? BestCombatWeapon(Monster target, string requiredEnchantmentRuntimeType = "")
    {
        return Game1.player.Items.OfType<MeleeWeapon>()
            .Where(weapon => !weapon.isScythe())
            .Where(weapon => string.IsNullOrWhiteSpace(requiredEnchantmentRuntimeType) ||
                weapon.enchantments.Any(enchantment => string.Equals(enchantment.GetType().Name, requiredEnchantmentRuntimeType, StringComparison.Ordinal)))
            .OrderByDescending(weapon => CombatWeaponScore(weapon, target))
            .ThenByDescending(weapon => weapon.maxDamage.Value)
            .ThenBy(weapon => weapon.QualifiedItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static MeleeWeapon? ResolveCombatWeapon(Monster target, int? requestedSlotIndex, string requiredEnchantmentRuntimeType)
    {
        if (!requestedSlotIndex.HasValue)
        {
            return BestCombatWeapon(target, requiredEnchantmentRuntimeType);
        }
        var slot = requestedSlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count || Game1.player.Items[slot] is not MeleeWeapon weapon || weapon.isScythe())
        {
            return null;
        }
        return string.IsNullOrWhiteSpace(requiredEnchantmentRuntimeType) ||
            weapon.enchantments.Any(enchantment => string.Equals(enchantment.GetType().Name, requiredEnchantmentRuntimeType, StringComparison.Ordinal))
                ? weapon
                : null;
    }

    private static double CombatWeaponScore(MeleeWeapon weapon, Monster target)
    {
        var attackMultiplier = 1d + Game1.player.buffs.AttackMultiplier;
        var averageDamage = ((weapon.minDamage.Value + weapon.maxDamage.Value) / 2d) * attackMultiplier;
        var postResilience = Math.Max(1d, averageDamage - target.resilience.Value);
        var precision = weapon.addedPrecision.Value * (1d + Game1.player.buffs.WeaponPrecisionMultiplier);
        var hitChance = 1d - Math.Max(0d, target.missChance.Value - target.missChance.Value * precision);
        double criticalChance = weapon.critChance.Value;
        if (weapon.type.Value == MeleeWeapon.dagger)
        {
            criticalChance = (criticalChance + 0.005f) * 1.12f;
        }
        criticalChance = Math.Clamp(criticalChance * (1d + Game1.player.buffs.CriticalChanceMultiplier), 0d, 1d);
        var criticalMultiplier = weapon.critMultiplier.Value * (1d + Game1.player.buffs.CriticalPowerMultiplier);
        var expectedDamage = postResilience * hitChance * (1d + criticalChance * Math.Max(0d, criticalMultiplier - 1d));
        var swipeSpeed = Math.Max(40d, (400d - weapon.speed.Value * 40d) * (1d - Game1.player.buffs.WeaponSpeedMultiplier));
        var animationFactor = weapon.type.Value == MeleeWeapon.dagger ? 0.5d : weapon.type.Value == MeleeWeapon.club ? 1.6d : 0.75d;
        return expectedDamage / Math.Max(40d, swipeSpeed * animationFactor);
    }

    private void CompleteCombatMonster(ActiveCombatMonster active, bool targetDefeated = true, string terminalVerificationReason = "native_fire_tool_defeated_target")
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        activeCombatMonster = null;
        RecordCombatHealth(active);
        var request = active.Pending.Request;
        var damageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        var inventoryAfter = InventoryStackSignature();
        var changedFacts = active.Pending.ChangedFacts.Concat(new[]
        {
            new SimulatedFactChange { Path = "mining.monsters[target].health", Before = active.TargetHealthBefore.ToString(), After = active.Target.Health.ToString() },
            new SimulatedFactChange { Path = "player.health", Before = active.PlayerHealthBefore.ToString(), After = Game1.player.health.ToString() }
        }).ToList();
        if (!string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal))
        {
            changedFacts.Add(new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter });
        }
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.LocationId,
            TargetTileX = request.TargetTileX,
            TargetTileY = request.TargetTileY,
            ToolQualifiedItemId = active.Weapon.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "combat_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = (damageTaken == 0
                ? new[] { terminalVerificationReason, "player_health_unchanged" }
                : new[] { terminalVerificationReason, "player_damage_observed=" + damageTaken })
                .Concat(string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal)
                    ? Array.Empty<string>()
                    : new[] { "natural_incidental_pickup_observed" })
                .ToArray(),
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = CombatObservedEffect(active),
            CombatTargetRuntimeType = active.TargetRuntimeType,
            CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity,
            CombatTargetName = active.TargetName,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray(),
            CombatDamageTaken = damageTaken,
            CombatTargetDefeated = targetDefeated,
            CombatMethod = "melee",
            CombatTerminalState = active.TerminalState,
            ChangedFacts = changedFacts.ToArray()
        };
        ApplyQuestSlayFeedback(
            result,
            request,
            requireProgress: targetDefeated && request.QuestSlayTargetStep);
        ApplyQuestResourceSourceFeedback(result, request);
        ApplySpecialOrderCollectSourceFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompleteCombatMonsterBlocked(ActiveCombatMonster active, string reason)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        if (ReferenceEquals(Game1.player.CurrentTool, active.Weapon))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        activeCombatMonster = null;
        RecordCombatHealth(active);
        var result = BlockedWithPrimitive(active.Pending.Request, "combat_monster", active.RequestedEffect, CombatObservedEffect(active), reason);
        result.ToolQualifiedItemId = active.Weapon.QualifiedItemId;
        result.ActualTicks = active.ElapsedTicks;
        result.TrainingImpactScope = "executor_calibration";
        result.CombatTargetRuntimeType = active.TargetRuntimeType;
        result.CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity;
        result.CombatTargetName = active.TargetName;
        result.CombatAttackCount = active.AttackCount;
        result.CombatHitCount = active.HitCount;
        result.CombatTargetHealthSequence = active.TargetHealthSequence.ToArray();
        result.CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray();
        result.CombatDamageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        result.CombatTargetDefeated = active.Target.Health <= 0;
        result.CombatMethod = "melee";
        result.CombatTerminalState = active.TerminalState;
        var inventoryAfter = InventoryStackSignature();
        result.ChangedFacts = active.Pending.ChangedFacts
            .Concat(string.Equals(active.InventoryBefore, inventoryAfter, StringComparison.Ordinal)
                ? Array.Empty<SimulatedFactChange>()
                : new[] { new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter } })
            .ToArray();
        active.Pending.Completion.SetResult(result);
    }

    private static string CombatObservedEffect(ActiveCombatMonster active)
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";target_type=" + active.TargetRuntimeType +
            ";target_name=" + active.TargetName +
            ";target_health=" + active.Target.Health +
            ";player_health=" + Game1.player.health +
            ";attacks=" + active.AttackCount +
            ";hits=" + active.HitCount;
    }
}
