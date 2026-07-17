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
    private void StartSetupMineFishingFloor(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue || request.MineLevel.Value < 80 || request.MineLevel.Value > 120)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_mine_fishing_floor", "current_location.mine_area=80;can_fish_here=true", "mine_level=" + request.MineLevel, "mine_fishing_fixture_level_not_in_lava_area"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var prerequisiteFacts = EnsureMineFishingFixtureEquipment();
        activeMineFishingSetup = new ActiveMineFishingSetup(pending, request.MineLevel.Value, beforeLocation, prerequisiteFacts);
        Game1.enterMine(request.MineLevel.Value);
    }

    private void StartMineStone(PendingExecution pending)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", "mining.objects[target].is_breakable_stone=false", "target=missing", "mine_stone_target_tile_required"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var mine = Game1.currentLocation as MineShaft;
        var pickaxe = FindTool<Pickaxe>();
        var requested = "mining.objects[" + target.X + "," + target.Y + "].is_breakable_stone=false;native_tool=Pickaxe";
        if (mine is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_requires_loaded_mineshaft"));
            return;
        }

        var tile = new Vector2(target.X, target.Y);
        if (!mine.objects.TryGetValue(tile, out var stone) || !stone.IsBreakableStone())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_target_not_breakable_stone"));
            return;
        }

        if (pickaxe is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_pickaxe_unavailable"));
            return;
        }

        if (Game1.player.Stamina <= 0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "mine_stone", requested, MineStoneObservedEffect(target), "mine_stone_energy_exhausted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var requestedStand = request.StandTileX.HasValue && request.StandTileY.HasValue
            ? new Point(request.StandTileX.Value, request.StandTileY.Value)
            : (Point?)null;
        var path = BuildCompilerAdjacentPath(mine, target, requestedStand, maxMovementTiles, out var moveReason);

        activeMineStone = new ActiveMineStone(
            pending,
            mine.NameOrUniqueName,
            target,
            path ?? new List<Point>(),
            pickaxe,
            stone.QualifiedItemId,
            stone.MinutesUntilReady,
            Game1.player.Stamina,
            Math.Clamp(request.MaxCrops, 1, 64),
            maxMovementTiles,
            requestedStand,
            requested);
    }

    private static List<Point>? BuildCompilerAdjacentPath(
        MineShaft mine,
        Point target,
        Point? requestedStand,
        int maxMovementTiles,
        out string blockReason)
    {
        blockReason = string.Empty;
        if (requestedStand.HasValue &&
            AreAdjacent(requestedStand.Value, target) &&
            IsTileOnMap(mine, requestedStand.Value) &&
            IsTileWalkable(mine, requestedStand.Value) &&
            !IsTileOccupiedByCharacter(mine, requestedStand.Value))
        {
            var requestedPath = TryBuildTilePath(
                mine,
                Game1.player.TilePoint,
                requestedStand.Value,
                maxMovementTiles,
                out blockReason,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
            if (requestedPath is not null)
            {
                return requestedPath;
            }
        }

        return BuildAdjacentToolPath(
            mine,
            target,
            maxMovementTiles,
            out blockReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
    }

    private static List<Point>? BuildCompilerMineExitPath(
        MineShaft mine,
        Point target,
        Point? requestedStand,
        int maxMovementTiles,
        out string blockReason)
    {
        blockReason = string.Empty;
        var stands = new List<Point>();
        if (requestedStand.HasValue && ManhattanDistance(requestedStand.Value, target) is >= 1 and <= 2)
        {
            stands.Add(requestedStand.Value);
        }
        for (var offsetX = -2; offsetX <= 2; offsetX++)
        {
            for (var offsetY = -2; offsetY <= 2; offsetY++)
            {
                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance is >= 1 and <= 2)
                {
                    stands.Add(new Point(target.X + offsetX, target.Y + offsetY));
                }
            }
        }

        foreach (var stand in stands.Distinct().OrderBy(point => ManhattanDistance(Game1.player.TilePoint, point)))
        {
            if (!IsTileOnMap(mine, stand) || !IsTileWalkable(mine, stand) || IsTileOccupiedByCharacter(mine, stand))
            {
                continue;
            }
            var path = TryBuildTilePath(
                mine,
                Game1.player.TilePoint,
                stand,
                maxMovementTiles,
                out blockReason,
                avoidSoftObstacles: true,
                allowRemovableObstacles: false);
            if (path is not null)
            {
                return path;
            }
        }

        blockReason = string.IsNullOrWhiteSpace(blockReason) ? "mine_exit_interaction_stand_unreachable" : blockReason;
        return null;
    }

    private void TickMineStone()
    {
        if (activeMineStone is null)
        {
            return;
        }

        var active = activeMineStone;
        try
        {
            TickMineStoneCore(active);
        }
        catch (Exception ex)
        {
            CompleteMineStoneBlocked(active, "mine_stone_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickMineStoneCore(ActiveMineStone active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is not MineShaft mine ||
            !string.Equals(mine.NameOrUniqueName, active.LocationId, StringComparison.Ordinal))
        {
            CompleteMineStoneBlocked(active, "mine_stone_location_changed_or_world_unavailable");
            return;
        }

        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteMineStoneBlocked(active, "mine_stone_timeout");
            return;
        }

        var currentPlayerTile = Game1.player.TilePoint;
        if (currentPlayerTile != active.LastObservedTile)
        {
            if (!active.CombatInterrupted)
            {
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, currentPlayerTile);
            }
            active.LastObservedTile = currentPlayerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteMineStoneBlocked(active, "mine_stone_movement_budget_exceeded");
                return;
            }
        }

        var targetVector = new Vector2(active.Target.X, active.Target.Y);
        if (!mine.objects.TryGetValue(targetVector, out var current))
        {
            if (active.BeginIssued)
            {
                RecordMineStoneCompletedSwing(active, 0);
            }
            CompleteMineStone(active);
            return;
        }

        if (!current.IsBreakableStone() || !string.Equals(current.QualifiedItemId, active.QualifiedItemId, StringComparison.Ordinal))
        {
            CompleteMineStoneBlocked(active, "mine_stone_runtime_target_drift");
            return;
        }

        if (!active.BeginIssued && ImmediateMiningThreat(mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!active.BeginIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                if (!TryReplanMineStone(active, mine, out var exhaustedReason))
                {
                    DelayOrBlockMineStoneReplan(active, "mine_stone_dynamic_path_unavailable:" + exhaustedReason);
                }
                return;
            }

            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                active.StuckTicks = 0;
                return;
            }

            if (!IsTileWalkable(mine, next) || IsTileOccupiedByCharacter(mine, next))
            {
                StopAllMovement();
                if (!TryReplanMineStone(active, mine, out var changedReason))
                {
                    DelayOrBlockMineStoneReplan(active, "mine_stone_dynamic_path_unavailable:" + changedReason);
                }
                return;
            }

            active.PathFailureTicks = 0;
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
                    StopAllMovement();
                    if (!TryReplanMineStone(active, mine, out var stuckReason))
                    {
                        DelayOrBlockMineStoneReplan(active, "mine_stone_movement_stuck:" + stuckReason);
                    }
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.SwingCount >= active.MaxSwings)
        {
            CompleteMineStoneBlocked(active, "mine_stone_max_swings_exceeded");
            return;
        }

        if (Game1.player.Stamina <= 0f)
        {
            CompleteMineStoneBlocked(active, "mine_stone_energy_exhausted");
            return;
        }

        if (!active.BeginIssued)
        {
            SelectTool(active.Pickaxe);
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

        RecordMineStoneCompletedSwing(active, mine.objects.TryGetValue(targetVector, out var afterSwing) ? afterSwing.MinutesUntilReady : 0);
    }

    private static bool TryReplanMineStone(ActiveMineStone active, MineShaft mine, out string blockReason)
    {
        var remainingMovementTiles = active.MaxMovementTiles - active.MovementTiles;
        if (remainingMovementTiles <= 0)
        {
            blockReason = "movement_budget_exhausted";
            return false;
        }

        var path = BuildCompilerAdjacentPath(
            mine,
            active.Target,
            active.RequestedStand,
            remainingMovementTiles,
            out blockReason);
        if (path is null)
        {
            return false;
        }

        active.Path = path;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.PathFailureTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private void DelayOrBlockMineStoneReplan(ActiveMineStone active, string reason)
    {
        active.PathFailureTicks++;
        if (active.PathFailureTicks > 180)
        {
            CompleteMineStoneBlocked(active, reason);
        }
    }

    private static void RecordMineStoneCompletedSwing(ActiveMineStone active, int remainingHealth)
    {
        active.SwingCount++;
        active.ObservedHealth.Add(remainingHealth);
        active.BeginIssued = false;
        active.ReleaseIssued = false;
    }

    private void CompleteMineStone(ActiveMineStone active)
    {
        StopAllMovement();
        activeMineStone = null;
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
            TargetLocation = active.LocationId,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ToolQualifiedItemId = active.Pickaxe.QualifiedItemId,
            ToolUpgradeLevel = active.Pickaxe.UpgradeLevel,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "mine_stone",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_pickaxe_lifecycle_removed_breakable_stone", "native_swing_count=" + active.SwingCount },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = MineStoneObservedEffect(active.Target) + ";health_sequence=" + string.Join(",", active.ObservedHealth) + ";native_swings=" + active.SwingCount,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.QualifiedItemId + ":health=" + active.HealthBefore, After = "removed" },
                new SimulatedFactChange { Path = "player.energy", Before = active.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private void CompleteMineStoneBlocked(ActiveMineStone active, string reason)
    {
        StopAllMovement();
        if (active.BeginIssued && ReferenceEquals(Game1.player.CurrentTool, active.Pickaxe))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        activeMineStone = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "mine_stone", active.RequestedEffect, MineStoneObservedEffect(active.Target) + ";native_swings=" + active.SwingCount, reason));
    }

    private static string MineStoneObservedEffect(Point target)
    {
        var location = Game1.currentLocation as MineShaft;
        var tile = new Vector2(target.X, target.Y);
        var state = location?.objects.TryGetValue(tile, out var obj) == true
            ? obj.QualifiedItemId + ":breakable=" + obj.IsBreakableStone().ToString().ToLowerInvariant() + ":health=" + obj.MinutesUntilReady
            : "removed_or_missing";
        return "location=" + (location?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";stone=" + state;
    }

    private void StartResourceClump(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var farmRequest = request.OptionId == "executor.break_farm_resource_clump";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ResourceClumpTileX.HasValue || !request.ResourceClumpTileY.HasValue ||
            !request.ResourceClumpWidth.HasValue || !request.ResourceClumpHeight.HasValue ||
            !request.ResourceClumpParentSheetIndex.HasValue || !request.ToolSlotIndex.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "break_resource_clump",
                (farmRequest ? "farm" : "mining") + ".resource_clumps[anchor].present=false",
                "request=missing_typed_clump_fields",
                "resource_clump_typed_target_fields_required"));
            return;
        }

        GameLocation location;
        if (farmRequest)
        {
            if (Game1.currentLocation is not Farm farm)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(
                    request,
                    "break_resource_clump",
                    "farm.resource_clumps[anchor].present=false",
                    "location=not_loaded_farm",
                    "farm_resource_clump_requires_loaded_farm"));
                return;
            }
            location = farm;
        }
        else if (Game1.currentLocation is MineShaft mine)
        {
            location = mine;
        }
        else
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "break_resource_clump",
                "mining.resource_clumps[anchor].present=false",
                "location=not_loaded_mineshaft",
                "resource_clump_requires_loaded_mineshaft"));
            return;
        }

        var anchor = new Point(request.ResourceClumpTileX.Value, request.ResourceClumpTileY.Value);
        var hitTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var clump = location.resourceClumps.FirstOrDefault(candidate =>
            (int)candidate.Tile.X == anchor.X &&
            (int)candidate.Tile.Y == anchor.Y &&
            candidate.width.Value == request.ResourceClumpWidth.Value &&
            candidate.height.Value == request.ResourceClumpHeight.Value &&
            candidate.parentSheetIndex.Value == request.ResourceClumpParentSheetIndex.Value);
        var factPathPrefix = farmRequest ? "farm.resource_clumps" : "mining.resource_clumps";
        var requested = factPathPrefix + "[" + anchor.X + "," + anchor.Y + "].present=false;native_tool=" + request.RequiredToolKind;
        if (clump is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_target_not_found_or_drifted"));
            return;
        }
        if (!ResourceClumpContainsTile(clump, hitTile) ||
            !AreAdjacent(stand, hitTile) ||
            ResourceClumpContainsTile(clump, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_hit_or_stand_geometry_invalid"));
            return;
        }
        if (!TryResourceClumpRequirement(clump.parentSheetIndex.Value, out var requiredToolKind, out var minimumUpgradeLevel) ||
            !string.Equals(requiredToolKind, request.RequiredToolKind, StringComparison.Ordinal) ||
            (farmRequest && clump.parentSheetIndex.Value is not ResourceClump.stumpIndex and not ResourceClump.hollowLogIndex))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_type_unsupported_or_requirement_mismatch"));
            return;
        }
        if (request.ToolSlotIndex.Value < 0 ||
            request.ToolSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.ToolSlotIndex.Value] is not Tool tool ||
            !ResourceClumpToolMatches(tool, requiredToolKind) ||
            tool.UpgradeLevel < minimumUpgradeLevel)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_required_tool_or_upgrade_unavailable"));
            return;
        }
        if (!IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_compiler_stand_tile_invalid"));
            return;
        }
        if (Game1.player.Stamina <= 0f)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_energy_exhausted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            location,
            Game1.player.TilePoint,
            stand,
            maxMovementTiles,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "break_resource_clump", requested, ResourceClumpObservedEffect(location, anchor), "resource_clump_path_unavailable:" + pathReason));
            return;
        }

        activeResourceClump = new ActiveResourceClump(
            pending,
            location,
            clump,
            anchor,
            hitTile,
            stand,
            path,
            tool,
            requiredToolKind,
            minimumUpgradeLevel,
            clump.health.Value,
            Math.Clamp(request.MaxCrops, 1, 64),
            maxMovementTiles,
            request.RestoreSlotIndex ?? Game1.player.CurrentToolIndex,
            factPathPrefix,
            farmRequest,
            requested);
    }

    private void TickResourceClump()
    {
        if (activeResourceClump is null)
        {
            return;
        }

        var active = activeResourceClump;
        try
        {
            TickResourceClumpCore(active);
        }
        catch (Exception ex)
        {
            CompleteResourceClumpBlocked(active, "resource_clump_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickResourceClumpCore(ActiveResourceClump active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteResourceClumpBlocked(active, "resource_clump_location_changed_or_world_unavailable");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteResourceClumpBlocked(active, "resource_clump_timeout");
            return;
        }

        var currentPlayerTile = Game1.player.TilePoint;
        if (currentPlayerTile != active.LastObservedTile)
        {
            if (!active.CombatInterrupted)
            {
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, currentPlayerTile);
            }
            active.LastObservedTile = currentPlayerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteResourceClumpBlocked(active, "resource_clump_movement_budget_exceeded");
                return;
            }
        }

        var targetPresent = active.Location.resourceClumps.Any(clump => ReferenceEquals(clump, active.Clump));
        if (!targetPresent)
        {
            if (active.BeginIssued)
            {
                RecordResourceClumpSwing(active, 0f);
            }
            CompleteResourceClump(active);
            return;
        }
        if ((int)active.Clump.Tile.X != active.Anchor.X ||
            (int)active.Clump.Tile.Y != active.Anchor.Y ||
            active.Clump.parentSheetIndex.Value != active.ParentSheetIndex ||
            active.Clump.width.Value != active.Width ||
            active.Clump.height.Value != active.Height)
        {
            CompleteResourceClumpBlocked(active, "resource_clump_runtime_target_drift");
            return;
        }

        if (!active.BeginIssued && active.Location is MineShaft mine && ImmediateMiningThreat(mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!active.BeginIssued && Game1.player.TilePoint != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteResourceClumpBlocked(active, "resource_clump_path_exhausted_before_stand");
                return;
            }

            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                active.StuckTicks = 0;
                return;
            }
            if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
            {
                CompleteResourceClumpBlocked(active, "resource_clump_dynamic_path_blocked");
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
                    CompleteResourceClumpBlocked(active, "resource_clump_movement_stuck");
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.SwingCount >= active.MaxSwings)
        {
            CompleteResourceClumpBlocked(active, "resource_clump_swing_budget_exceeded");
            return;
        }
        if (Game1.player.Stamina <= 0f)
        {
            CompleteResourceClumpBlocked(active, "resource_clump_energy_exhausted");
            return;
        }
        if (!active.BeginIssued)
        {
            SelectTool(active.Tool);
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.HitTile));
            Game1.player.lastClick = new Vector2(active.HitTile.X * Game1.tileSize, active.HitTile.Y * Game1.tileSize);
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

        RecordResourceClumpSwing(active, active.Clump.health.Value);
    }

    private static void RecordResourceClumpSwing(ActiveResourceClump active, float health)
    {
        active.SwingCount++;
        active.ObservedHealth.Add(health);
        active.BeginIssued = false;
        active.ReleaseIssued = false;
    }

    private void CompleteResourceClump(ActiveResourceClump active)
    {
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeResourceClump = null;
        var request = active.Pending.Request;
        var changedFacts = new List<SimulatedFactChange>
        {
            new SimulatedFactChange
            {
                Path = active.FactPathPrefix + "[" + active.Anchor.X + "," + active.Anchor.Y + "]",
                Before = "parent_sheet_index=" + active.ParentSheetIndex + ":health=" + active.HealthBefore.ToString("0.###", CultureInfo.InvariantCulture),
                After = "removed"
            },
            new SimulatedFactChange
            {
                Path = "player.energy",
                Before = active.StaminaBefore.ToString("0.###", CultureInfo.InvariantCulture),
                After = Game1.player.Stamina.ToString("0.###", CultureInfo.InvariantCulture)
            }
        };
        if (active.TrackForagingExperience)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.skills.foraging.experience",
                Before = active.ForagingExperienceBefore.ToString(CultureInfo.InvariantCulture),
                After = Game1.player.experiencePoints[Farmer.foragingSkill].ToString(CultureInfo.InvariantCulture)
            });
        }
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
            TargetLocation = active.Location.NameOrUniqueName,
            TargetTileX = active.Anchor.X,
            TargetTileY = active.Anchor.Y,
            ToolQualifiedItemId = active.Tool.QualifiedItemId,
            ToolUpgradeLevel = active.Tool.UpgradeLevel,
            ToolUseCount = active.SwingCount,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "break_resource_clump",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_" + active.RequiredToolKind + "_lifecycle_removed_resource_clump",
                "multi_tile_clump_identity_verified",
                "natural_resource_clump_drops_left_as_game_debris",
                "native_swing_count=" + active.SwingCount
            },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = ResourceClumpObservedEffect(active.Location, active.Anchor) +
                ";parent_sheet_index=" + active.ParentSheetIndex +
                ";size=" + active.Width + "x" + active.Height +
                ";health_sequence=" + string.Join(",", active.ObservedHealth.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))) +
                ";native_swings=" + active.SwingCount,
            ChangedFacts = changedFacts.ToArray()
        });
    }

    private void CompleteResourceClumpBlocked(ActiveResourceClump active, string reason)
    {
        StopAllMovement();
        if (active.BeginIssued && ReferenceEquals(Game1.player.CurrentTool, active.Tool))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        RestoreSlot(active.RestoreSlotIndex);
        activeResourceClump = null;
        var result = BlockedWithPrimitive(
            active.Pending.Request,
            "break_resource_clump",
            active.RequestedEffect,
            ResourceClumpObservedEffect(active.Location, active.Anchor) + ";native_swings=" + active.SwingCount,
            reason);
        result.ToolQualifiedItemId = active.Tool.QualifiedItemId;
        result.ToolUpgradeLevel = active.Tool.UpgradeLevel;
        result.ToolUseCount = active.SwingCount;
        result.ActualTicks = active.ElapsedTicks;
        result.EnergyBefore = active.StaminaBefore;
        result.EnergyAfter = Game1.player.Stamina;
        active.Pending.Completion.SetResult(result);
    }

    private static bool ResourceClumpContainsTile(ResourceClump clump, Point tile)
    {
        var x = (int)clump.Tile.X;
        var y = (int)clump.Tile.Y;
        return tile.X >= x && tile.X < x + clump.width.Value &&
            tile.Y >= y && tile.Y < y + clump.height.Value;
    }

    private static bool TryResourceClumpRequirement(int parentSheetIndex, out string requiredToolKind, out int minimumUpgradeLevel)
    {
        (requiredToolKind, minimumUpgradeLevel) = parentSheetIndex switch
        {
            ResourceClump.stumpIndex => ("axe", 1),
            ResourceClump.hollowLogIndex => ("axe", 2),
            ResourceClump.quarryBoulderIndex or ResourceClump.meteoriteIndex => ("pickaxe", 3),
            ResourceClump.boulderIndex => ("pickaxe", 2),
            ResourceClump.mineRock1Index or ResourceClump.mineRock2Index or ResourceClump.mineRock3Index or ResourceClump.mineRock4Index => ("pickaxe", 0),
            _ => (string.Empty, 0)
        };
        return !string.IsNullOrWhiteSpace(requiredToolKind);
    }

    private static bool ResourceClumpToolMatches(Tool tool, string requiredToolKind)
    {
        return requiredToolKind == "axe" && tool is Axe ||
            requiredToolKind == "pickaxe" && tool is Pickaxe;
    }

    private static string ResourceClumpObservedEffect(GameLocation location, Point anchor)
    {
        var clump = location.resourceClumps.FirstOrDefault(candidate =>
            (int)candidate.Tile.X == anchor.X && (int)candidate.Tile.Y == anchor.Y);
        return clump is null
            ? "location=" + location.NameOrUniqueName + ";anchor=" + anchor.X + "," + anchor.Y + ";resource_clump=removed_or_missing"
            : "location=" + location.NameOrUniqueName +
                ";anchor=" + anchor.X + "," + anchor.Y +
                ";resource_clump=present" +
                ";parent_sheet_index=" + clump.parentSheetIndex.Value +
                ";size=" + clump.width.Value + "x" + clump.height.Value +
                ";health=" + clump.health.Value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
