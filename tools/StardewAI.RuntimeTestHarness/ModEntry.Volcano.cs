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
    private void StartVolcanoCoolLava(PendingExecution pending)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", "volcano.tiles.cooled_lava_tiles contains target", "target=missing", "volcano_cooling_target_tile_required"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = "volcano.tiles.cooled_lava_tiles contains " + target.X + "," + target.Y + ";native_tool=WateringCan";
        if (Game1.currentLocation is not VolcanoDungeon volcano)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_requires_loaded_volcano_dungeon"));
            return;
        }

        if (volcano.level.Value == 5)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_level_five_cooling_disabled"));
            return;
        }

        if (!IsTileOnMap(volcano, target) || volcano.waterTiles is null || !volcano.waterTiles[target.X, target.Y])
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_target_not_native_water_or_lava_tile"));
            return;
        }

        if (volcano.cooledLavaTiles.ContainsKey(new Vector2(target.X, target.Y)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_target_already_cooled"));
            return;
        }

        if (!request.WateringCanSlotIndex.HasValue ||
            request.WateringCanSlotIndex.Value < 0 ||
            request.WateringCanSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.WateringCanSlotIndex.Value] is not WateringCan wateringCan)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_watering_can_slot_invalid"));
            return;
        }

        if (!wateringCan.IsBottomless && wateringCan.WaterLeft <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_watering_can_empty"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var requestedStand = request.StandTileX.HasValue && request.StandTileY.HasValue
            ? new Point(request.StandTileX.Value, request.StandTileY.Value)
            : (Point?)null;
        List<Point>? path = null;
        var pathReason = string.Empty;
        if (requestedStand.HasValue)
        {
            if (!AreAdjacent(requestedStand.Value, target) ||
                !IsTileOnMap(volcano, requestedStand.Value) ||
                !IsTileWalkable(volcano, requestedStand.Value) ||
                IsTileOccupiedByCharacter(volcano, requestedStand.Value))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_compiler_stand_tile_invalid"));
                return;
            }

            path = TryBuildTilePath(
                volcano,
                Game1.player.TilePoint,
                requestedStand.Value,
                maxMovementTiles,
                out pathReason,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
        }
        else
        {
            path = BuildAdjacentToolPath(
                volcano,
                target,
                maxMovementTiles,
                out pathReason,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
        }
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "cool_volcano_lava", requested, VolcanoCoolLavaObservedEffect(target), "volcano_cooling_path_unavailable:" + pathReason));
            return;
        }

        activeVolcanoCoolLava = new ActiveVolcanoCoolLava(
            pending,
            volcano,
            target,
            path,
            wateringCan,
            request.WateringCanSlotIndex.Value,
            Game1.player.CurrentToolIndex,
            wateringCan.WaterLeft,
            Game1.player.Stamina,
            maxMovementTiles,
            requested);
    }

    private void TickVolcanoCoolLava()
    {
        if (activeVolcanoCoolLava is null)
        {
            return;
        }

        var active = activeVolcanoCoolLava;
        try
        {
            TickVolcanoCoolLavaCore(active);
        }
        catch (Exception ex)
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickVolcanoCoolLavaCore(ActiveVolcanoCoolLava active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            Game1.currentLocation is not VolcanoDungeon volcano ||
            !ReferenceEquals(volcano, active.Volcano))
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_location_changed_or_world_unavailable");
            return;
        }

        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_timeout");
            return;
        }

        var targetVector = new Vector2(active.Target.X, active.Target.Y);
        if (volcano.cooledLavaTiles.ContainsKey(targetVector))
        {
            CompleteVolcanoCoolLava(active);
            return;
        }

        if (volcano.level.Value == 5 || volcano.waterTiles is null || !volcano.waterTiles[active.Target.X, active.Target.Y])
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_runtime_target_drift");
            return;
        }

        if (!active.BeginIssued && ImmediateVolcanoThreat(volcano))
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_unsafe_monster_window");
            return;
        }

        if (!active.BeginIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_path_exhausted_before_adjacent");
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
                CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_dynamic_path_blocked");
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
                    CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_movement_stuck");
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (!active.BeginIssued)
        {
            Game1.player.CurrentToolIndex = active.WateringCanSlotIndex;
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            Game1.player.lastClick = new Vector2(active.Target.X * Game1.tileSize, active.Target.Y * Game1.tileSize);
            Game1.player.BeginUsingTool();
            active.BeginIssued = true;
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

        active.CompletionWaitTicks++;
        if (active.CompletionWaitTicks > 180)
        {
            CompleteVolcanoCoolLavaBlocked(active, "volcano_cooling_native_action_completed_without_cooled_tile");
        }
    }

    private static bool ImmediateVolcanoThreat(VolcanoDungeon volcano)
    {
        return volcano.characters.OfType<Monster>().Any(monster =>
            monster.Health > 0 &&
            Math.Abs(monster.TilePoint.X - Game1.player.TilePoint.X) + Math.Abs(monster.TilePoint.Y - Game1.player.TilePoint.Y) <= 3);
    }

    private void CompleteVolcanoCoolLava(ActiveVolcanoCoolLava active)
    {
        StopAllMovement();
        RestoreVolcanoCoolingSlot(active);
        activeVolcanoCoolLava = null;
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
            EnergyBefore = active.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            WaterBefore = active.WaterBefore,
            WaterAfter = active.WateringCan.WaterLeft,
            TargetLocation = active.Volcano.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.WateringCan.QualifiedItemId,
            ToolUpgradeLevel = active.WateringCan.UpgradeLevel,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "cool_volcano_lava",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_watering_can_lifecycle_added_cooled_lava_tile" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = VolcanoCoolLavaObservedEffect(active.Target) + ";water_before=" + active.WaterBefore + ";water_after=" + active.WateringCan.WaterLeft,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "volcano.tiles.cooled_lava_tiles[" + active.Target.X + "," + active.Target.Y + "]", Before = "false", After = "true" },
                new SimulatedFactChange { Path = "player.inventory[" + active.WateringCanSlotIndex + "].water_left", Before = active.WaterBefore.ToString(), After = active.WateringCan.WaterLeft.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = active.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteVolcanoCoolLavaBlocked(ActiveVolcanoCoolLava active, string reason)
    {
        StopAllMovement();
        if (active.BeginIssued && ReferenceEquals(Game1.player.CurrentTool, active.WateringCan))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        RestoreVolcanoCoolingSlot(active);
        activeVolcanoCoolLava = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "cool_volcano_lava",
            active.RequestedEffect,
            VolcanoCoolLavaObservedEffect(active.Target) + ";water_before=" + active.WaterBefore + ";water_after=" + active.WateringCan.WaterLeft,
            reason));
    }

    private static void RestoreVolcanoCoolingSlot(ActiveVolcanoCoolLava active)
    {
        if (active.RestoreSlotIndex >= 0 && active.RestoreSlotIndex < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
    }

    private static string VolcanoCoolLavaObservedEffect(Point target)
    {
        var volcano = Game1.currentLocation as VolcanoDungeon;
        var cooled = volcano?.cooledLavaTiles.ContainsKey(new Vector2(target.X, target.Y)) == true;
        return "location=" + (volcano?.NameOrUniqueName ?? "none") +
            ";level=" + (volcano?.level.Value.ToString() ?? "none") +
            ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";target=" + target.X + "," + target.Y +
            ";cooled=" + cooled.ToString().ToLowerInvariant();
    }

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
            CompleteVolcanoObstacleBlocked(active, "volcano_obstacle_unsafe_monster_window");
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
    }
}
