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
    private void StartVolcanoCombat(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var requested = "volcano.monsters[target].present=false;native_input=melee";
        if (string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
            string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "target=missing_or_incomplete", "volcano_combat_target_identity_required"));
            return;
        }
        if (Game1.currentLocation is not VolcanoDungeon volcano)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "location=not_loaded_volcano", "volcano_combat_requires_loaded_volcano_dungeon"));
            return;
        }

        var targets = volcano.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.Name, request.TargetName, StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "matching_target_count=" + targets.Length, targets.Length == 0 ? "volcano_combat_target_not_found_or_moved" : "volcano_combat_target_ambiguous"));
            return;
        }

        var target = targets[0];
        if (target is Spiker || target.GetType().Assembly != typeof(Monster).Assembly)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "target_type=" + target.GetType().FullName, "volcano_combat_target_melee_semantics_unsupported"));
            return;
        }
        if (!request.CombatWeaponSlotIndex.HasValue ||
            request.CombatWeaponSlotIndex.Value < 0 ||
            request.CombatWeaponSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.CombatWeaponSlotIndex.Value] is not MeleeWeapon weapon ||
            weapon.isScythe())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "weapon=missing", "volcano_combat_melee_weapon_slot_invalid"));
            return;
        }

        activeVolcanoCombat = new ActiveVolcanoCombat(
            pending,
            volcano,
            target,
            weapon,
            request.CombatWeaponSlotIndex.Value,
            Game1.player.CurrentToolIndex,
            Math.Clamp(request.MaxAttacks, 1, 256),
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
            requested);
    }

    private void TickVolcanoCombat()
    {
        if (activeVolcanoCombat is null)
        {
            return;
        }

        var active = activeVolcanoCombat;
        try
        {
            TickVolcanoCombatCore(active);
        }
        catch (Exception ex)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickVolcanoCombatCore(ActiveVolcanoCombat active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            Game1.currentLocation is not VolcanoDungeon volcano ||
            !ReferenceEquals(volcano, active.Volcano))
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_location_changed_or_world_unavailable");
            return;
        }

        RecordVolcanoCombatHealth(active);
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_timeout");
            return;
        }
        if (Game1.player.health <= 0)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_player_defeated");
            return;
        }

        var targetPresent = volcano.characters.Contains(active.Target);
        if (active.Target.Health <= 0 || !targetPresent)
        {
            if (active.Target.Health <= 0)
            {
                CompleteVolcanoCombat(active);
            }
            else
            {
                CompleteVolcanoCombatBlocked(active, "volcano_combat_target_disappeared_without_defeat");
            }
            return;
        }

        if (TrackVolcanoCombatProgress(active) > 600)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_no_movement_or_damage_progress");
            return;
        }

        var releasedAttackThisTick = false;
        if (active.AttackButtonHeld)
        {
            if (!TryApplySmapiButtonOverride(SButton.C, pressed: false, out var releaseReason))
            {
                CompleteVolcanoCombatBlocked(active, releaseReason);
                return;
            }
            active.AttackButtonHeld = false;
            releasedAttackThisTick = true;
        }

        if (!IsMonsterWithinCombatReach(active.Target, active.Weapon))
        {
            var targetTile = active.Target.TilePoint;
            if (active.PathIndex >= active.Path.Count || ManhattanDistance(active.PathTarget, targetTile) > 2)
            {
                var path = BuildAdjacentToolPath(
                    volcano,
                    targetTile,
                    Math.Max(1, active.MaxMovementTiles - active.MovementTiles),
                    out var pathReason,
                    avoidSoftObstacles: true,
                    allowRemovableObstacles: false);
                if (path is null)
                {
                    active.PathFailures++;
                    if (active.PathFailures > 120)
                    {
                        CompleteVolcanoCombatBlocked(active, "volcano_combat_dynamic_path_unavailable:" + pathReason);
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
            if (!IsTileWalkable(volcano, next) || IsTileOccupiedByCharacter(volcano, next))
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.PathFailures++;
                return;
            }

            ObserveVolcanoCombatMovement(active);
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteVolcanoCombatBlocked(active, "volcano_combat_movement_budget_exceeded");
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

        StopAllMovement();
        if (active.Target.isInvincible() || Game1.player.UsingTool || releasedAttackThisTick ||
            Game1.activeClickableMenu is not null || Game1.eventUp)
        {
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_attack_budget_exceeded");
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        Game1.player.CurrentToolIndex = active.WeaponSlotIndex;
        Game1.player.faceDirection(DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection));
        Game1.player.lastClick = new Vector2(targetCenter.X, targetCenter.Y);
        if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var inputReason))
        {
            CompleteVolcanoCombatBlocked(active, inputReason);
            return;
        }
        active.AttackButtonHeld = true;
        active.AttackCount++;
    }

    private static int TrackVolcanoCombatProgress(ActiveVolcanoCombat active)
    {
        if (Vector2.DistanceSquared(active.LastProgressPosition, Game1.player.Position) >= 0.01f ||
            active.Target.Health < active.LastProgressTargetHealth)
        {
            active.LastProgressPosition = Game1.player.Position;
            active.LastProgressTargetHealth = active.Target.Health;
            active.NoProgressTicks = 0;
        }
        else
        {
            active.NoProgressTicks++;
        }
        return active.NoProgressTicks;
    }

    private static void ObserveVolcanoCombatMovement(ActiveVolcanoCombat active)
    {
        var currentPosition = Game1.player.Position;
        active.StuckTicks = Vector2.DistanceSquared(active.LastMovementPosition, currentPosition) < 0.01f
            ? active.StuckTicks + 1
            : 0;
        var currentTile = Game1.player.TilePoint;
        if (currentTile != active.LastMovementTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastMovementTile, currentTile);
        }
        active.LastMovementPosition = currentPosition;
        active.LastMovementTile = currentTile;
    }

    private static void RecordVolcanoCombatHealth(ActiveVolcanoCombat active)
    {
        if (active.TargetHealthSequence.Count == 0 || active.TargetHealthSequence[^1] != active.Target.Health)
        {
            if (active.TargetHealthSequence.Count > 0 && active.Target.Health < active.TargetHealthSequence[^1])
            {
                active.HitCount++;
            }
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        if (active.PlayerHealthSequence.Count == 0 || active.PlayerHealthSequence[^1] != Game1.player.health)
        {
            active.PlayerHealthSequence.Add(Game1.player.health);
        }
    }

    private void CompleteVolcanoCombat(ActiveVolcanoCombat active)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoCombat = null;
        RecordVolcanoCombatHealth(active);
        var request = active.Pending.Request;
        var damageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Volcano.NameOrUniqueName,
            TargetTileX = request.TargetTileX,
            TargetTileY = request.TargetTileY,
            ToolQualifiedItemId = active.Weapon.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "combat_volcano_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = damageTaken == 0
                ? new[] { "native_melee_defeated_volcano_target", "player_health_unchanged" }
                : new[] { "native_melee_defeated_volcano_target", "player_damage_observed=" + damageTaken },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = VolcanoCombatObservedEffect(active),
            CombatTargetRuntimeType = active.TargetRuntimeType,
            CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity,
            CombatTargetName = active.TargetName,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray(),
            CombatDamageTaken = damageTaken,
            CombatTargetDefeated = true,
            CombatMethod = "melee",
            CombatTerminalState = "defeat",
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "volcano.monsters[" + active.TargetRuntimeIdentity + "].present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "player.health", Before = active.PlayerHealthBefore.ToString(), After = Game1.player.health.ToString() }
            }
        });
    }

    private void CompleteVolcanoCombatBlocked(ActiveVolcanoCombat active, string reason)
    {
        TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
        StopAllMovement();
        if (ReferenceEquals(Game1.player.CurrentTool, active.Weapon))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoCombat = null;
        RecordVolcanoCombatHealth(active);
        var result = BlockedWithPrimitive(active.Pending.Request, "combat_volcano_monster", active.RequestedEffect, VolcanoCombatObservedEffect(active), reason);
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
        result.CombatTerminalState = active.Target.Health <= 0 ? "defeat" : "blocked";
        active.Pending.Completion.SetResult(result);
    }

    private static string VolcanoCombatObservedEffect(ActiveVolcanoCombat active)
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";target_type=" + active.TargetRuntimeType +
            ";target_name=" + active.TargetName +
            ";target_health=" + active.Target.Health +
            ";player_health=" + Game1.player.health +
            ";attacks=" + active.AttackCount +
            ";hits=" + active.HitCount;
    }}
