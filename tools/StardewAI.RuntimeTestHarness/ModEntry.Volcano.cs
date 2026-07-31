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
        if (activeVolcanoCombat is not null)
        {
            return;
        }

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
            StopAllMovement();
            if (Game1.player.UsingTool ||
                !Game1.player.CanMove ||
                Game1.player.FarmerSprite.PauseForSingleAnimation)
            {
                return;
            }

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
            if (!TryStartReactiveVolcanoCombat(
                    volcano,
                    "cool_lava_immediate_threat"))
            {
                CompleteVolcanoCoolLavaBlocked(
                    active,
                    "volcano_cooling_unsafe_monster_window");
            }
            return;
        }

        if (!active.BeginIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (!TryAdvanceExecutorPath(
                    volcano,
                    active.Path,
                    active.PathCursor,
                    out var pathReason))
            {
                WriteExecutorDiagnosticDump(
                    "volcano_cooling_" + pathReason);
                CompleteVolcanoCoolLavaBlocked(
                    active,
                    "volcano_cooling_" + pathReason);
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

    private static Monster? NearestImmediateVolcanoThreat(
        VolcanoDungeon volcano)
    {
        return volcano.characters
            .OfType<Monster>()
            .Where(monster =>
                monster.Health > 0 &&
                monster is not Spiker &&
                monster.GetType().Assembly ==
                    typeof(Monster).Assembly &&
                ManhattanDistance(
                    monster.TilePoint,
                    Game1.player.TilePoint) <= 3 &&
                (monster.isGlider.Value ||
                    BuildAdjacentToolPath(
                        volcano,
                        monster.TilePoint,
                        3,
                        out _,
                        avoidSoftObstacles: true,
                        allowRemovableObstacles: false) is not null))
            .OrderBy(monster => Vector2.DistanceSquared(
                monster.GetBoundingBox().Center.ToVector2(),
                Game1.player.GetBoundingBox().Center.ToVector2()))
            .FirstOrDefault();
    }

    private static bool ImmediateVolcanoThreat(
        VolcanoDungeon volcano)
    {
        return NearestImmediateVolcanoThreat(volcano) is not null;
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

}
