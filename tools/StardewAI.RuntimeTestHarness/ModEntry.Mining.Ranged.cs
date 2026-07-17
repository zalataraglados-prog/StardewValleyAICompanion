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
    private void StartShootMonster(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        const string requested = "target_monster.defeated=true;native_input=full_charge_slingshot";
        if (Game1.currentLocation is not MineShaft mine ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
            string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "target_or_location=missing", "slingshot_target_identity_required"));
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "matching_target_count=" + targets.Length, "slingshot_target_not_unique"));
            return;
        }
        if (!request.SlingshotSlotIndex.HasValue ||
            request.SlingshotSlotIndex.Value < 0 ||
            request.SlingshotSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.SlingshotSlotIndex.Value] is not Slingshot slingshot ||
            slingshot.attachments.Count == 0 ||
            slingshot.attachments[0] is not StardewValley.Object ammo ||
            ammo.Stack <= 0 ||
            !string.Equals(ammo.QualifiedItemId, request.SlingshotAmmoQualifiedItemId, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "slingshot=missing_or_ammo_drifted", "loaded_slingshot_contract_not_met"));
            return;
        }
        if (!HasClearProjectilePath(mine, Game1.player.TilePoint, targets[0].TilePoint))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested, "projectile_path=blocked", "slingshot_projectile_path_blocked"));
            return;
        }
        if (ammo.QualifiedItemId == "(O)441" &&
            !ExplosiveAmmoAreaIsSafe(mine, targets[0], out var explosiveSafetyReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "shoot_monster", requested,
                "explosive_area=unsafe", explosiveSafetyReason));
            return;
        }

        activeShootMonster = new ActiveShootMonster(
            pending,
            mine,
            targets[0],
            slingshot,
            ammo.QualifiedItemId,
            ammo.Stack,
            Game1.player.CurrentToolIndex,
            Math.Clamp(request.MaxAttacks, 1, 256),
            requested);
        SlingshotAimPatch.ActiveSlingshot = slingshot;
        SlingshotAimPatch.AimWorldPixel = targets[0].GetBoundingBox().Center;
    }

    private void TickShootMonster()
    {
        if (activeShootMonster is null)
        {
            return;
        }
        var active = activeShootMonster;
        SlingshotAimPatch.ActiveSlingshot = active.Slingshot;
        SlingshotAimPatch.AimWorldPixel = active.Target.GetBoundingBox().Center;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteShootMonsterBlocked(active, "slingshot_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteShootMonsterBlocked(active, "slingshot_timeout");
            return;
        }
        if (active.Target.Health <= 0 || !active.Mine.characters.Contains(active.Target))
        {
            if (active.ButtonHeld)
            {
                active.Slingshot.finish();
                active.ButtonHeld = false;
                return;
            }
            if (Game1.player.UsingTool || Game1.player.usingSlingshot)
            {
                return;
            }
            CompleteShootMonster(active);
            return;
        }
        if (!HasClearProjectilePath(active.Mine, Game1.player.TilePoint, active.Target.TilePoint))
        {
            CompleteShootMonsterBlocked(active, "slingshot_projectile_path_drifted");
            return;
        }
        if (active.Slingshot.attachments.Count == 0 ||
            active.Slingshot.attachments[0] is not StardewValley.Object ammo ||
            !string.Equals(ammo.QualifiedItemId, active.AmmoQualifiedItemId, StringComparison.Ordinal) ||
            ammo.Stack <= 0)
        {
            CompleteShootMonsterBlocked(active, "slingshot_ammo_exhausted_or_drifted");
            return;
        }
        if (active.AmmoQualifiedItemId == "(O)441" &&
            !ExplosiveAmmoAreaIsSafe(active.Mine, active.Target, out var explosiveSafetyReason))
        {
            CompleteShootMonsterBlocked(active, explosiveSafetyReason);
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        if (active.ButtonHeld)
        {
            active.HoldTicks++;
            if (active.HoldTicks < 20)
            {
                return;
            }
            active.Slingshot.onRelease(active.Mine, targetCenter.X, targetCenter.Y, Game1.player);
            active.ButtonHeld = false;
            active.CooldownTicks = 12;
            active.AttackCount++;
            active.AimPrepared = false;
            return;
        }
        if (active.CooldownTicks > 0)
        {
            active.CooldownTicks--;
            return;
        }
        if (Game1.player.UsingTool || Game1.player.usingSlingshot || Game1.activeClickableMenu is not null || Game1.eventUp)
        {
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteShootMonsterBlocked(active, "slingshot_attack_budget_exceeded");
            return;
        }
        if (active.Target.Health < active.LastTargetHealth)
        {
            active.HitCount++;
            active.LastTargetHealth = active.Target.Health;
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        Game1.player.CurrentToolIndex = active.SlingshotSlotIndex;
        var targetDirection = active.Target.GetBoundingBox().Center.ToVector2();
        Game1.player.faceGeneralDirection(targetDirection, 0);
        if (!active.AimPrepared)
        {
            active.AimPrepared = true;
            return;
        }
        Game1.player.lastClick = targetCenter.ToVector2();
        Game1.player.BeginUsingTool();
        if (!Game1.player.usingSlingshot)
        {
            CompleteShootMonsterBlocked(active, "slingshot_native_begin_using_not_observed");
            return;
        }
        active.ButtonHeld = true;
        active.HoldTicks = 0;
    }

    private void CompleteShootMonster(ActiveShootMonster active)
    {
        active.Slingshot.finish();
        SlingshotAimPatch.Clear(active.Slingshot);
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activeShootMonster = null;
        var ammoAfter = active.Slingshot.attachments.Count > 0 && active.Slingshot.attachments[0] is StardewValley.Object ammo
            ? ammo.Stack
            : 0;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            ToolQualifiedItemId = active.Slingshot.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "shoot_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_full_charge_slingshot_defeated_target", "ammo_consumption_observed" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "target_health=" + active.Target.Health + ";ammo_stack=" + ammoAfter,
            CombatMethod = "slingshot",
            CombatConsumableQualifiedItemId = active.AmmoQualifiedItemId,
            CombatConsumableCountBefore = active.AmmoCountBefore,
            CombatConsumableCountAfter = ammoAfter,
            CombatTargetRuntimeType = active.Target.GetType().FullName ?? active.Target.GetType().Name,
            CombatTargetRuntimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.Target).ToString("X8"),
            CombatTargetName = active.Target.Name,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatTargetDefeated = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.monsters[target].health", Before = active.TargetHealthBefore.ToString(), After = active.Target.Health.ToString() },
                new SimulatedFactChange { Path = "player.slingshot.ammo.stack", Before = active.AmmoCountBefore.ToString(), After = ammoAfter.ToString() }
            }
        });
    }

    private void CompleteShootMonsterBlocked(ActiveShootMonster active, string reason)
    {
        active.Slingshot.finish();
        SlingshotAimPatch.Clear(active.Slingshot);
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activeShootMonster = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "shoot_monster", active.RequestedEffect,
            "target_health=" + active.Target.Health, reason));
    }

    private static bool HasClearProjectilePath(GameLocation location, Point start, Point target)
    {
        var x = start.X;
        var y = start.Y;
        var deltaX = Math.Abs(target.X - start.X);
        var stepX = start.X < target.X ? 1 : -1;
        var deltaY = -Math.Abs(target.Y - start.Y);
        var stepY = start.Y < target.Y ? 1 : -1;
        var error = deltaX + deltaY;
        while (x != target.X || y != target.Y)
        {
            var doubled = 2 * error;
            if (doubled >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }
            if (doubled <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
            if ((x != target.X || y != target.Y) &&
                ((location.objects.TryGetValue(new Vector2(x, y), out var obj) && !obj.isPassable()) || location.BlocksDamageLOS(x, y)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ExplosiveAmmoAreaIsSafe(MineShaft mine, Monster target, out string reason)
    {
        const int radius = 2;
        const int targetMotionMargin = 1;
        for (var offsetX = -targetMotionMargin; offsetX <= targetMotionMargin; offsetX++)
        {
            for (var offsetY = -targetMotionMargin; offsetY <= targetMotionMargin; offsetY++)
            {
                var possibleCenter = new Point(target.TilePoint.X + offsetX, target.TilePoint.Y + offsetY);
                var damageRectangle = new Rectangle(
                    (possibleCenter.X - radius) * Game1.tileSize,
                    (possibleCenter.Y - radius) * Game1.tileSize,
                    (radius * 2 + 1) * Game1.tileSize,
                    (radius * 2 + 1) * Game1.tileSize);
                if (damageRectangle.Intersects(Game1.player.GetBoundingBox()))
                {
                    reason = "explosive_ammo_player_inside_target_motion_envelope";
                    return false;
                }
                if (mine.farmers.Any(farmer =>
                    farmer != Game1.player && damageRectangle.Intersects(farmer.GetBoundingBox())))
                {
                    reason = "explosive_ammo_other_farmer_inside_target_motion_envelope";
                    return false;
                }
                foreach (var tile in BombAffectedTiles(possibleCenter, radius))
                {
                    if (mine.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj) &&
                        !obj.IsBreakableStone() &&
                        obj is not BreakableContainer)
                    {
                        reason = "explosive_ammo_protected_object_inside_target_motion_envelope";
                        return false;
                    }
                    if (mine.terrainFeatures.ContainsKey(new Vector2(tile.X, tile.Y)))
                    {
                        reason = "explosive_ammo_terrain_feature_inside_target_motion_envelope";
                        return false;
                    }
                }
            }
        }
        reason = string.Empty;
        return true;
    }
}
