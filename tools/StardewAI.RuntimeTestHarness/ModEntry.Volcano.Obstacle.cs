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
    private void StartVolcanoObstacle(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var isStone = request.OptionId == "executor.break_volcano_stone";
        var primitiveKind = isStone ? "break_volcano_stone" : "break_volcano_container";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, "volcano.objects[target].present=false", "target_or_stand=missing", "volcano_obstacle_target_and_stand_required"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var requested = "volcano.objects[" + target.X + "," + target.Y + "].present=false;native_tool=" + (isStone ? "Pickaxe" : "HeavyHitter");
        if (Game1.currentLocation is not VolcanoDungeon volcano)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, requested, VolcanoObstacleObservedEffect(target, isStone), "volcano_obstacle_requires_loaded_volcano_dungeon"));
            return;
        }

        var targetVector = target.ToVector2();
        if (!volcano.objects.TryGetValue(targetVector, out var targetObject) ||
            (isStone && !targetObject.IsBreakableStone()) ||
            (!isStone && targetObject is not BreakableContainer) ||
            (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
             !string.Equals(targetObject.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, requested, VolcanoObstacleObservedEffect(target, isStone), "volcano_obstacle_target_not_live_or_type_drift"));
            return;
        }

        if (!request.ToolSlotIndex.HasValue ||
            request.ToolSlotIndex.Value < 0 ||
            request.ToolSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.ToolSlotIndex.Value] is not Tool tool ||
            (isStone && tool is not Pickaxe) ||
            (!isStone && !tool.isHeavyHitter()))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, requested, VolcanoObstacleObservedEffect(target, isStone), "volcano_obstacle_tool_slot_invalid"));
            return;
        }

        if (!AreAdjacent(stand, target) ||
            !IsTileOnMap(volcano, stand) ||
            !IsTileWalkable(volcano, stand) ||
            IsTileOccupiedByCharacter(volcano, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, requested, VolcanoObstacleObservedEffect(target, isStone), "volcano_obstacle_compiler_stand_tile_invalid"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            volcano,
            Game1.player.TilePoint,
            stand,
            maxMovementTiles,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, requested, VolcanoObstacleObservedEffect(target, isStone), "volcano_obstacle_path_unavailable:" + pathReason));
            return;
        }

        var healthBefore = isStone
            ? targetObject.MinutesUntilReady
            : ReadBreakableContainerHealth((BreakableContainer)targetObject) ?? 3;
        activeVolcanoObstacle = new ActiveVolcanoObstacle(
            pending,
            volcano,
            target,
            stand,
            path,
            targetObject,
            tool,
            request.ToolSlotIndex.Value,
            Game1.player.CurrentToolIndex,
            isStone,
            healthBefore,
            Game1.player.Stamina,
            Math.Clamp(request.MaxCrops, 1, 64),
            maxMovementTiles,
            requested);
    }

    private void TickVolcanoObstacle()
    {
        if (activeVolcanoObstacle is null)
        {
            return;
        }

        var active = activeVolcanoObstacle;
        try
        {
            TickVolcanoObstacleCore(active);
        }
        catch (Exception ex)
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickVolcanoObstacleCore(ActiveVolcanoObstacle active)
    {
        if (activeVolcanoCombat is not null)
        {
            return;
        }

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            Game1.currentLocation is not VolcanoDungeon volcano ||
            !ReferenceEquals(volcano, active.Volcano))
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_location_changed_or_world_unavailable");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_timeout");
            return;
        }

        var targetVector = active.Target.ToVector2();
        if (!volcano.objects.TryGetValue(targetVector, out var current))
        {
            if (!active.IsStone)
            {
                active.HeavyHitterAction!.RecordRemoval();
            }
            CompleteVolcanoObstacle(active);
            return;
        }
        if (!ReferenceEquals(current, active.TargetObject) ||
            (active.IsStone && !current.IsBreakableStone()) ||
            (!active.IsStone && current is not BreakableContainer))
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_runtime_target_drift");
            return;
        }

        if ((active.IsStone || !active.HeavyHitterAction!.ButtonHeld) && ImmediateVolcanoThreat(volcano))
        {
            if (!TryStartReactiveVolcanoCombat(
                    volcano,
                    "obstacle_immediate_threat"))
            {
                CompleteVolcanoObstacleBlocked(
                    active,
                    "volcano_obstacle_unsafe_monster_window");
            }
            return;
        }

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_path_exhausted_before_adjacent");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                active.StuckTicks = 0;
                return;
            }
            if (!IsTileWalkable(volcano, next) || IsTileOccupiedByCharacter(volcano, next))
            {
                CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_dynamic_path_blocked");
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_movement_stuck");
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.IsStone && active.SwingCount >= active.MaxSwings)
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_swing_budget_exceeded");
            return;
        }
        if (Game1.player.Stamina <= 0f)
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_energy_exhausted");
            return;
        }

        if (active.IsStone)
        {
            if (!active.BeginIssued)
            {
                Game1.player.CurrentToolIndex = active.ToolSlotIndex;
                Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
                Game1.player.lastClick = new Vector2(active.Target.X * Game1.tileSize, active.Target.Y * Game1.tileSize);
                Game1.player.BeginUsingTool();
                active.BeginIssued = true;
                active.SwingCount++;
                return;
            }
            if (!active.ReleaseIssued && Game1.player.UsingTool && Game1.player.canReleaseTool)
            {
                Game1.player.EndUsingTool();
                active.ReleaseIssued = true;
                return;
            }
            if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
            {
                return;
            }
            RecordVolcanoObstacleSwing(active, current);
            return;
        }

        if (!TryTickNativeHeavyHitterAction(
                active.HeavyHitterAction!,
                active.Target,
                current is BreakableContainer container ? ReadBreakableContainerHealth(container) : null,
                out var heavyHitterReason))
        {
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_" + heavyHitterReason);
        }
    }

    private static void RecordVolcanoObstacleSwing(ActiveVolcanoObstacle active, StardewValley.Object current)
    {
        var health = current.MinutesUntilReady;
        if (active.ObservedHealth.Count == 0 || active.ObservedHealth[^1] != health)
        {
            active.ObservedHealth.Add(health);
        }
        active.BeginIssued = false;
        active.ReleaseIssued = false;
    }

    private void CompleteVolcanoObstacle(ActiveVolcanoObstacle active)
    {
        if (active.HeavyHitterAction is not null)
        {
            ReleaseNativeHeavyHitterAction(active.HeavyHitterAction);
        }
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoObstacle = null;
        var swingCount = active.EffectiveSwingCount;
        if (active.EffectiveObservedHealth.Count == 0 || active.EffectiveObservedHealth[^1] != 0)
        {
            if (active.HeavyHitterAction is not null)
            {
                active.HeavyHitterAction.RecordRemoval();
            }
            else
            {
                active.ObservedHealth.Add(0);
            }
        }
        var observedHealth = active.EffectiveObservedHealth;
        var request = active.Pending.Request;
        var primitiveKind = active.IsStone ? "break_volcano_stone" : "break_volcano_container";
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            EnergyBefore = active.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = active.Volcano.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.Tool.QualifiedItemId,
            ToolUpgradeLevel = active.Tool.UpgradeLevel,
            ToolUseCount = swingCount,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = active.IsStone
                ? new[] { "native_pickaxe_lifecycle_removed_volcano_stone", "native_swing_count=" + swingCount }
                : new[] { "native_heavy_hitter_input_removed_volcano_container", "released_contents_left_as_game_debris", "native_swing_count=" + swingCount },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = VolcanoObstacleObservedEffect(active.Target, active.IsStone) + ";health_sequence=" + string.Join(",", observedHealth) + ";native_swings=" + swingCount,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "volcano.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.TargetObject.QualifiedItemId + ":health=" + active.HealthBefore, After = "removed" },
                new SimulatedFactChange { Path = "volcano.debris.count", Before = active.DebrisCountBefore.ToString(), After = active.Volcano.debris.Count.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = active.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteVolcanoObstacleBlocked(ActiveVolcanoObstacle active, string reason)
    {
        if (active.HeavyHitterAction is not null)
        {
            ReleaseNativeHeavyHitterAction(active.HeavyHitterAction);
        }
        StopAllMovement();
        if (active.BeginIssued && ReferenceEquals(Game1.player.CurrentTool, active.Tool))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoObstacle = null;
        var primitiveKind = active.IsStone ? "break_volcano_stone" : "break_volcano_container";
        var swingCount = active.EffectiveSwingCount;
        var result = BlockedWithPrimitive(
            active.Pending.Request,
            primitiveKind,
            active.RequestedEffect,
            VolcanoObstacleObservedEffect(active.Target, active.IsStone) + ";health_sequence=" + string.Join(",", active.EffectiveObservedHealth) + ";native_swings=" + swingCount,
            reason);
        result.ToolQualifiedItemId = active.Tool.QualifiedItemId;
        result.ToolUpgradeLevel = active.Tool.UpgradeLevel;
        result.ToolUseCount = swingCount;
        result.EnergyBefore = active.StaminaBefore;
        result.EnergyAfter = Game1.player.Stamina;
        result.ActualTicks = active.ElapsedTicks;
        result.TrainingImpactScope = "executor_calibration";
        active.Pending.Completion.SetResult(result);
    }

    private static string VolcanoObstacleObservedEffect(Point target, bool isStone)
    {
        var volcano = Game1.currentLocation as VolcanoDungeon;
        var present = volcano?.objects.TryGetValue(target.ToVector2(), out var obj) == true &&
            (isStone ? obj.IsBreakableStone() : obj is BreakableContainer);
        return "location=" + (volcano?.NameOrUniqueName ?? "none") +
            ";level=" + (volcano?.level.Value.ToString() ?? "none") +
            ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";target=" + target.X + "," + target.Y +
            ";target_present=" + present.ToString().ToLowerInvariant();
    }

}
