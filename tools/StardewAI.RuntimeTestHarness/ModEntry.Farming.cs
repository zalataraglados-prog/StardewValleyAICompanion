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
    private void StartMaintainCrops(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var farm = Game1.getFarm();
        var hasTargetTile = request.TargetTileX.HasValue && request.TargetTileY.HasValue;
        var targetTileX = request.TargetTileX.GetValueOrDefault();
        var targetTileY = request.TargetTileY.GetValueOrDefault();

        foreach (var pair in farm.terrainFeatures.Pairs.OrderBy(item => item.Key.Y).ThenBy(item => item.Key.X))
        {
            if (hasTargetTile &&
                ((int)pair.Key.X != targetTileX ||
                 (int)pair.Key.Y != targetTileY))
            {
                continue;
            }

            if (pair.Value is not HoeDirt dirt || dirt.crop is null || !dirt.needsWatering())
            {
                continue;
            }

            StartWaterCrop(pending, new Point((int)pair.Key.X, (int)pair.Key.Y));
            return;
        }

        pending.Completion.SetResult(ExecuteMaintainCropsNoOp(request));
    }

    private TrainingExecutionResult ExecuteMaintainCropsNoOp(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var energyBefore = Game1.player.Stamina;
        var farm = Game1.getFarm();
        var hasTargetTile = request.TargetTileX.HasValue && request.TargetTileY.HasValue;
        var targetTileX = request.TargetTileX.GetValueOrDefault();
        var targetTileY = request.TargetTileY.GetValueOrDefault();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "no_op",
            FeedbackAvailable = true,
            WateredCount = 0,
            EnergyBefore = energyBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = hasTargetTile ? targetTileX : null,
            TargetTileY = hasTargetTile ? targetTileY : null,
            FailureCategory = hasTargetTile ? "invalid_tile" : "skipped_no_candidate",
            TrainingImpactScope = "executor_calibration",
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "maintain_crops",
            PrimitiveVerificationStatus = "not_applicable_no_op",
            PrimitiveVerificationReasons = new[] { hasTargetTile ? "target_crop_not_found_or_not_needing_watering" : "no_crop_needed_watering" },
            RequestedEffect = hasTargetTile
                ? "farm.crops[" + targetTileX + "," + targetTileY + "].needs_watering=false"
                : "farm.crops.needs_watering=false",
            ObservedEffect = "watered_count=0"
        };
    }

    private void StartWaterCrop(PendingExecution pending, Point target)
    {
        var request = pending.Request;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var staminaBefore = Game1.player.Stamina;
        var can = FindTool<WateringCan>();
        var waterBefore = can?.WaterLeft;
        var estimatedTicks = EstimateRuntimeToolTicks(target);
        var requested = WaterCropRequestedEffect(target);

        if (Game1.currentLocation != farm)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, "wrong_location", requested, WaterCropObservedEffect(farm, target)));
            return;
        }

        var precheck = ValidateWaterCropTarget(farm, target, can);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, precheck[0], requested, WaterCropObservedEffect(farm, target), precheck));
            return;
        }

        var path = BuildAdjacentToolPath(farm, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "water_crop", target, can, waterBefore, staminaBefore, started, estimatedTicks, moveReason, requested, WaterCropObservedEffect(farm, target)));
            return;
        }

        activeNativeTool = ActiveNativeTool.Water(pending, farm.NameOrUniqueName, target, path, can!, staminaBefore, waterBefore, started, estimatedTicks, requested, IsCropWatered(farm, target));
    }

    private void StartTillSoil(PendingExecution pending)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "till_soil", "farm.terrain_features[target].type=HoeDirt", TillSoilObservedEffect(Game1.getFarm(), null), "target_tile_required"));
            return;
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var staminaBefore = Game1.player.Stamina;
        var hoe = FindTool<Hoe>();
        var estimatedTicks = EstimateRuntimeToolTicks(target);
        var requested = TillSoilRequestedEffect(target);

        if (Game1.currentLocation != farm)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, "wrong_location", requested, TillSoilObservedEffect(farm, target)));
            return;
        }

        var precheck = ValidateTillSoilTarget(farm, target, hoe);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, precheck[0], requested, TillSoilObservedEffect(farm, target), precheck));
            return;
        }

        var path = BuildAdjacentToolPath(farm, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "till_soil", target, hoe, null, staminaBefore, started, estimatedTicks, moveReason, requested, TillSoilObservedEffect(farm, target)));
            return;
        }

        var tile = new Vector2(target.X, target.Y);
        var hadHoeDirt = farm.terrainFeatures.TryGetValue(tile, out var beforeFeature) && beforeFeature is HoeDirt;
        activeNativeTool = ActiveNativeTool.Till(pending, farm.NameOrUniqueName, target, path, hoe!, staminaBefore, started, estimatedTicks, requested, hadHoeDirt);
    }

    private static string[] ValidateWaterCropTarget(Farm farm, Point target, WateringCan? can)
    {
        var reasons = new List<string>();
        var tile = new Vector2(target.X, target.Y);
        if (!IsTileOnMap(farm, target))
        {
            reasons.Add("invalid_tile");
        }
        if (can is null)
        {
            reasons.Add("missing_tool");
        }
        else if (can.WaterLeft <= 0 && !Game1.player.hasWateringCanEnchantment)
        {
            reasons.Add("no_water");
        }
        if (Game1.player.Stamina <= 0f)
        {
            reasons.Add("insufficient_stamina");
        }
        if (!farm.terrainFeatures.TryGetValue(tile, out var feature) || feature is not HoeDirt dirt || dirt.crop is null)
        {
            reasons.Add("invalid_tile");
        }
        else if (!dirt.needsWatering())
        {
            reasons.Add("already_satisfied_runtime_drift");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateTillSoilTarget(Farm farm, Point target, Hoe? hoe)
    {
        var reasons = new List<string>();
        var tile = new Vector2(target.X, target.Y);
        if (!IsTileOnMap(farm, target) || farm.doesTileHaveProperty(target.X, target.Y, "Diggable", "Back") is null)
        {
            reasons.Add("invalid_tile");
        }
        if (hoe is null)
        {
            reasons.Add("missing_tool");
        }
        if (Game1.player.Stamina <= 0f)
        {
            reasons.Add("insufficient_stamina");
        }
        if (farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt)
        {
            reasons.Add("already_satisfied_runtime_drift");
        }
        else if (farm.terrainFeatures.ContainsKey(tile) || farm.objects.ContainsKey(tile) || farm.IsTileBlockedBy(tile, ~(CollisionMask.Characters | CollisionMask.Farmers)))
        {
            reasons.Add("occupied_tile");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static List<Point>? BuildAdjacentToolPath(GameLocation location, Point target, int maxTiles, out string blockReason, bool avoidSoftObstacles = false, bool allowRemovableObstacles = true)
    {
        blockReason = string.Empty;
        if (AreAdjacent(Game1.player.TilePoint, target))
        {
            return new List<Point>();
        }

        var start = Game1.player.TilePoint;
        var standTiles = Neighbors(target)
            .Where(tile => IsTileOnMap(location, tile) && IsTileWalkable(location, tile) &&
                (!avoidSoftObstacles || !IsTileOccupiedByCharacter(location, tile)))
            .OrderBy(tile => ManhattanDistance(start, tile))
            .ToArray();
        foreach (var standTile in standTiles)
        {
            var path = TryBuildTilePath(location, start, standTile, Math.Clamp(maxTiles, 1, 512), out blockReason, avoidSoftObstacles, allowRemovableObstacles);
            if (path is null)
            {
                continue;
            }

            return path;
        }

        blockReason = "unreachable_target";
        return null;
    }

    private void TickNativeTool()
    {
        if (activeNativeTool is null)
        {
            return;
        }

        var tool = activeNativeTool;
        try
        {
            TickNativeToolCore(tool);
        }
        catch (Exception ex)
        {
            CleanupBlockedNativeToolLifecycle(tool);
            activeNativeTool = null;
            Monitor.Log($"Native tool execution failed: {ex}", LogLevel.Error);
            tool.Pending.Completion.SetResult(NativeToolBlocked(tool.Pending.Request, tool.PrimitiveKind, tool.Target, tool.Tool, tool.WaterBefore, tool.StaminaBefore, tool.StartedAt, tool.EstimatedTicks, "execution_exception:" + ex.GetType().Name, tool.RequestedEffect, NativeToolObservedEffect(tool), actualTicks: tool.ElapsedTicks));
        }
    }

    private void TickNativeToolCore(ActiveNativeTool tool)
    {
        tool.ElapsedTicks++;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteNativeToolBlocked(tool, "world_not_ready_during_tool_use");
            return;
        }

        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, tool.LocationId, StringComparison.Ordinal))
        {
            CompleteNativeToolBlocked(tool, "location_changed_during_tool_use");
            return;
        }

        if (tool.ElapsedTicks > tool.MaxTicks)
        {
            CompleteNativeToolBlocked(tool, tool.BeginIssued ? "tool_timeout" : "movement_timeout");
            return;
        }

        if (!tool.BeginIssued && !AreAdjacent(Game1.player.TilePoint, tool.Target))
        {
            if (tool.PathIndex >= tool.Path.Count)
            {
                CompleteNativeToolBlocked(tool, "unreachable_target");
                return;
            }

            var next = tool.Path[tool.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                tool.PathIndex++;
                tool.StuckTicks = 0;
                tool.LastPosition = Game1.player.Position;
                return;
            }

            if (!IsTileWalkable(Game1.currentLocation, next) || IsTileOccupiedByCharacter(Game1.currentLocation, next))
            {
                CompleteNativeToolBlocked(tool, "unreachable_target");
                return;
            }

            var direction = DirectionTo(Game1.player.TilePoint, next);
            var movedSinceLastTick = Vector2.DistanceSquared(tool.LastPosition, Game1.player.Position) >= 0.01f;
            tool.LastPosition = Game1.player.Position;
            StartMoving(direction);
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                tool.PathIndex++;
            }

            if (!movedSinceLastTick)
            {
                tool.StuckTicks++;
                if (tool.StuckTicks > 45)
                {
                    CompleteNativeToolBlocked(tool, "movement_stuck_or_collision_blocked");
                }
            }
            else
            {
                tool.StuckTicks = 0;
                tool.LastPosition = Game1.player.Position;
            }

            return;
        }

        StopAllMovement();
        if (!tool.BeginIssued)
        {
            var recheck = tool.PrimitiveKind switch
            {
                "water_crop" => ValidateWaterCropTarget(Game1.getFarm(), tool.Target, tool.Tool as WateringCan),
                "fill_pet_bowl" => ValidatePetBowlTarget(Game1.currentLocation, tool.Target, tool.Tool as WateringCan),
                "harvest_ginger" => ValidateGingerHarvestTarget(Game1.currentLocation, tool.Target, tool.Tool as Hoe, tool.Pending.Request),
                _ => ValidateTillSoilTarget(Game1.getFarm(), tool.Target, tool.Tool as Hoe)
            };
            if (recheck.Length > 0)
            {
                CompleteNativeToolBlocked(tool, recheck[0], recheck);
                return;
            }

            SelectTool(tool.Tool);
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, tool.Target));
            Game1.player.lastClick = new Vector2(tool.Target.X * Game1.tileSize, tool.Target.Y * Game1.tileSize);
            Game1.player.BeginUsingTool();
            tool.BeginIssued = true;
            return;
        }

        if (!tool.ReleaseIssued && Game1.player.UsingTool && Game1.player.canReleaseTool)
        {
            Game1.player.EndUsingTool();
            tool.ReleaseIssued = true;
            return;
        }

        if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }

        CompleteNativeTool(tool);
    }

    private void CompleteNativeToolBlocked(ActiveNativeTool tool, string reason, string[]? reasons = null)
    {
        CleanupBlockedNativeToolLifecycle(tool);
        activeNativeTool = null;
        tool.Pending.Completion.SetResult(NativeToolBlocked(tool.Pending.Request, tool.PrimitiveKind, tool.Target, tool.Tool, tool.WaterBefore, tool.StaminaBefore, tool.StartedAt, tool.EstimatedTicks, reason, tool.RequestedEffect, NativeToolObservedEffect(tool), reasons, tool.ElapsedTicks));
    }

    private void CleanupBlockedNativeToolLifecycle(ActiveNativeTool tool)
    {
        StopAllMovement();
        if (!tool.BeginIssued || !ReferenceEquals(Game1.player.CurrentTool, tool.Tool))
        {
            return;
        }

        Game1.player.completelyStopAnimatingOrDoingAction();
    }

    private void CompleteNativeTool(ActiveNativeTool tool)
    {
        StopAllMovement();
        activeNativeTool = null;

        if (tool.PrimitiveKind == "harvest_ginger")
        {
            CompleteHarvestGingerNativeTool(tool);
            return;
        }
        if (tool.PrimitiveKind == "fill_pet_bowl")
        {
            CompleteFillPetBowlNativeTool(tool);
            return;
        }

        var farm = Game1.getFarm();
        var verified = tool.PrimitiveKind == "water_crop"
            ? !tool.BeforeWatered.GetValueOrDefault() && IsCropWatered(farm, tool.Target)
            : !tool.BeforeHadHoeDirt.GetValueOrDefault() && farm.terrainFeatures.TryGetValue(new Vector2(tool.Target.X, tool.Target.Y), out var feature) && feature is HoeDirt;
        var failureCategory = verified ? string.Empty : "unchanged_postcondition";
        var waterAfter = tool.Tool is WateringCan can ? can.WaterLeft : (int?)null;
        var afterWatered = tool.PrimitiveKind == "water_crop" ? IsCropWatered(farm, tool.Target) : (bool?)null;
        var afterHoeDirt = tool.PrimitiveKind == "till_soil" ? farm.terrainFeatures.TryGetValue(new Vector2(tool.Target.X, tool.Target.Y), out var afterFeature) && afterFeature is HoeDirt : (bool?)null;

        tool.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = tool.Pending.Request.RunId,
            QueueId = tool.Pending.Request.QueueId,
            QueueItemId = tool.Pending.Request.QueueItemId,
            BeforeStateHash = tool.Pending.Request.BeforeStateHash,
            OptionId = tool.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            WateredCount = tool.PrimitiveKind == "water_crop" && verified ? 1 : 0,
            EnergyBefore = tool.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = tool.Target.X,
            TargetTileY = tool.Target.Y,
            ToolQualifiedItemId = tool.Tool.QualifiedItemId,
            ToolUpgradeLevel = tool.Tool.UpgradeLevel,
            ToolPower = Game1.player.toolPower.Value,
            WaterBefore = tool.WaterBefore,
            WaterAfter = waterAfter,
            EstimatedTicks = tool.EstimatedTicks,
            ActualTicks = tool.ElapsedTicks,
            FailureCategory = failureCategory,
            TrainingImpactScope = "executor_calibration",
            StartedAt = tool.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = tool.PrimitiveKind,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? NativeToolVerifiedReasons(tool) : NativeToolMismatchReasons(tool),
            RequestedEffect = tool.RequestedEffect,
            ObservedEffect = NativeToolObservedEffect(tool) + ";move_ticks=" + Math.Min(tool.ElapsedTicks, tool.MaxMovementTicks),
            BlockReasons = verified ? Array.Empty<string>() : new[] { failureCategory },
            ChangedFacts = NativeToolChangedFacts(tool, afterWatered, afterHoeDirt, waterAfter)
        });
    }

    private static TrainingExecutionResult NativeToolBlocked(TrainingExecutionRequest request, string primitiveKind, Point target, Tool? tool, int? waterBefore, double staminaBefore, string started, int estimatedTicks, string failureCategory, string requestedEffect, string observedEffect, string[]? reasons = null, int actualTicks = 0)
    {
        var blockReasons = reasons is { Length: > 0 } ? reasons : new[] { failureCategory };
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ToolQualifiedItemId = tool?.QualifiedItemId ?? string.Empty,
            ToolUpgradeLevel = tool?.UpgradeLevel,
            ToolPower = Game1.player.toolPower.Value,
            WaterBefore = waterBefore,
            WaterAfter = tool is WateringCan can ? can.WaterLeft : null,
            EstimatedTicks = estimatedTicks,
            ActualTicks = actualTicks,
            FailureCategory = failureCategory,
            TrainingImpactScope = "executor_calibration",
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = blockReasons,
            RequestedEffect = requestedEffect,
            ObservedEffect = observedEffect,
            BlockReasons = blockReasons
        };
    }

    private static string NativeToolObservedEffect(ActiveNativeTool tool)
    {
        if (tool.PrimitiveKind == "harvest_ginger")
        {
            return GingerHarvestObservedEffect(Game1.currentLocation, tool.Target);
        }
        if (tool.PrimitiveKind == "fill_pet_bowl")
        {
            return PetBowlObservedEffect(Game1.currentLocation, tool.Target);
        }

        var farm = Game1.getFarm();
        return tool.PrimitiveKind == "water_crop"
            ? WaterCropObservedEffect(farm, tool.Target)
            : TillSoilObservedEffect(farm, tool.Target);
    }

    private static string[] NativeToolVerifiedReasons(ActiveNativeTool tool)
    {
        return tool.PrimitiveKind switch
        {
            "water_crop" => new[] { "native_watering_can_lifecycle_watered_target_crop" },
            "fill_pet_bowl" => new[] { "native_watering_can_lifecycle_filled_pet_bowl", "pet_friendship_remains_pending_until_Pet.dayUpdate" },
            "harvest_ginger" => new[] { "native_hoe_lifecycle_removed_ginger_crop", "native_ginger_debris_created", "native_foraging_experience_delta_seven" },
            _ => new[] { "native_hoe_lifecycle_created_hoe_dirt" }
        };
    }

    private static string[] NativeToolMismatchReasons(ActiveNativeTool tool)
    {
        return tool.PrimitiveKind switch
        {
            "water_crop" => new[] { "target_crop_water_state_unchanged_after_native_tool_lifecycle" },
            "fill_pet_bowl" => new[] { "pet_bowl_water_state_unchanged_after_native_tool_lifecycle" },
            "harvest_ginger" => new[] { "ginger_native_postcondition_mismatch" },
            _ => new[] { "target_tile_unchanged_after_native_hoe_lifecycle" }
        };
    }

    private static SimulatedFactChange[] NativeToolChangedFacts(ActiveNativeTool tool, bool? afterWatered, bool? afterHoeDirt, int? waterAfter)
    {
        var changes = new List<SimulatedFactChange>
        {
            new() { Path = "player.energy", Before = tool.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
        };

        if (tool.PrimitiveKind == "water_crop")
        {
            changes.Insert(0, new SimulatedFactChange { Path = "farm.crops[" + tool.Target.X + "," + tool.Target.Y + "].watered", Before = tool.BeforeWatered.GetValueOrDefault().ToString().ToLowerInvariant(), After = afterWatered.GetValueOrDefault().ToString().ToLowerInvariant() });
            changes.Add(new SimulatedFactChange { Path = "player.watering_can.water_left", Before = tool.WaterBefore?.ToString() ?? "missing", After = waterAfter?.ToString() ?? "missing" });
        }
        else
        {
            changes.Insert(0, new SimulatedFactChange { Path = "farm.terrain_features[" + tool.Target.X + "," + tool.Target.Y + "].type", Before = tool.BeforeHadHoeDirt.GetValueOrDefault() ? "HoeDirt" : "none", After = afterHoeDirt.GetValueOrDefault() ? "HoeDirt" : "none" });
        }

        return changes.ToArray();
    }

    private static void SelectTool(Tool tool)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (ReferenceEquals(Game1.player.Items[index], tool))
            {
                Game1.player.CurrentToolIndex = index;
                return;
            }
        }
    }

    private static bool IsCropWatered(Farm farm, Point target)
    {
        return farm.terrainFeatures.TryGetValue(new Vector2(target.X, target.Y), out var feature) &&
            feature is HoeDirt dirt &&
            dirt.isWatered();
    }

    private static int EstimateRuntimeToolTicks(Point target)
    {
        return Math.Max(0, ManhattanDistance(Game1.player.TilePoint, target) - 1) * 30 + 85;
    }

    private static string WaterCropRequestedEffect(Point target)
    {
        return "farm.crops[" + target.X + "," + target.Y + "].needs_watering=false;native_tool=WateringCan";
    }

    private static string WaterCropObservedEffect(Farm farm, Point target)
    {
        var tile = new Vector2(target.X, target.Y);
        var water = FindTool<WateringCan>();
        var cropState = farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt dirt
            ? "has_hoe_dirt=true;has_crop=" + (dirt.crop is not null).ToString().ToLowerInvariant() + ";watered=" + dirt.isWatered().ToString().ToLowerInvariant() + ";needs_watering=" + dirt.needsWatering().ToString().ToLowerInvariant()
            : "has_hoe_dirt=false";
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y + ";" + cropState + ";water_left=" + (water?.WaterLeft.ToString() ?? "missing");
    }

    private static string TillSoilRequestedEffect(Point target)
    {
        return "farm.terrain_features[" + target.X + "," + target.Y + "].type=HoeDirt;native_tool=Hoe";
    }

    private static string TillSoilObservedEffect(Farm farm, Point? target)
    {
        if (!target.HasValue)
        {
            return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";target=missing";
        }

        var tile = new Vector2(target.Value.X, target.Value.Y);
        var feature = farm.terrainFeatures.TryGetValue(tile, out var existing) ? existing.GetType().Name : "none";
        var obj = farm.objects.ContainsKey(tile).ToString().ToLowerInvariant();
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";terrain_feature=" + feature + ";object_present=" + obj;
    }

    private TrainingExecutionResult ExecuteFaceDirection(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "face_direction", "player.facing_direction=" + (request.Direction?.ToString() ?? "missing"), "player.facing_direction=" + Game1.player.FacingDirection, reasons.ToArray());
        }

        if (!request.Direction.HasValue || request.Direction.Value < 0 || request.Direction.Value > 3)
        {
            return BlockedWithPrimitive(request, "face_direction", "player.facing_direction=" + (request.Direction?.ToString() ?? "missing"), "player.facing_direction=" + Game1.player.FacingDirection, "direction_0_3_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var before = Game1.player.FacingDirection;
        Game1.player.faceDirection(request.Direction.Value);
        var observed = Game1.player.FacingDirection;
        var verified = observed == request.Direction.Value;
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
            PrimitiveKind = "face_direction",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "facing_direction_matches_request" } : new[] { "facing_direction_mismatch" },
            RequestedEffect = "player.facing_direction=" + request.Direction.Value,
            ObservedEffect = "player.facing_direction=" + observed,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "facing_direction_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.facing_direction",
                    Before = before.ToString(),
                    After = observed.ToString()
                }
            }
        };
    }

    private TrainingExecutionResult ExecuteSelectSafeItemSlot(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var requested = request.SafeSlotIndex?.ToString() ?? "missing";
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "select_safe_item_slot", "player.current_tool_index=" + requested, SafeSlotObservedEffect(), reasons.ToArray());
        }

        if (!request.SafeSlotIndex.HasValue || request.SafeSlotIndex.Value < 0 || request.SafeSlotIndex.Value > 11)
        {
            return BlockedWithPrimitive(request, "select_safe_item_slot", "player.current_tool_index=" + requested, SafeSlotObservedEffect(), "safe_slot_index_0_11_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeIndex = Game1.player.CurrentToolIndex;
        var beforeActiveObject = Game1.player.ActiveObject?.QualifiedItemId ?? string.Empty;
        Game1.player.CurrentToolIndex = request.SafeSlotIndex.Value;
        var observedIndex = Game1.player.CurrentToolIndex;
        var observedActiveObject = Game1.player.ActiveObject?.QualifiedItemId ?? string.Empty;
        var verified = observedIndex == request.SafeSlotIndex.Value && string.IsNullOrEmpty(observedActiveObject);

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
            PrimitiveKind = "select_safe_item_slot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "current_tool_index_matches_safe_slot", "active_object_cleared" } : new[] { "safe_slot_selection_mismatch" },
            RequestedEffect = "player.current_tool_index=" + request.SafeSlotIndex.Value + ";player.active_object_qualified_id=null",
            ObservedEffect = SafeSlotObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "safe_slot_selection_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.current_tool_index",
                    Before = beforeIndex.ToString(),
                    After = observedIndex.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "player.active_object_qualified_id",
                    Before = beforeActiveObject,
                    After = observedActiveObject
                }
            }
        };
    }

    private static string SafeSlotObservedEffect()
    {
        return "player.current_tool_index=" + Game1.player.CurrentToolIndex + ";player.active_object_qualified_id=" + (Game1.player.ActiveObject?.QualifiedItemId ?? "null");
    }

    private TrainingExecutionResult ExecuteCloseMenu(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var menu = Game1.activeClickableMenu;
        var beforeOpen = menu is not null;
        var beforeType = menu?.GetType().Name ?? "none";
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), reasons.ToArray());
        }

        if (menu is null)
        {
            return CompletedCloseMenu(request, beforeOpen, beforeType, "no_op", "verified_no_active_menu", new[] { "active_menu_already_closed" });
        }

        if (beforeType == "DialogueBox" && menu is DialogueBox unsafeBox &&
            !CanAdvanceOrdinaryDialogue(unsafeBox, request.SocialContinuationDialogueRecovery))
        {
            var unsafeReasons = new List<string>();
            if (unsafeBox.isQuestion) unsafeReasons.Add("dialogue_is_question_true");
            if (unsafeBox.responses is { Length: > 0 }) unsafeReasons.Add("dialogue_responses_present:" + unsafeBox.responses.Length);
            if (Game1.eventUp) unsafeReasons.Add("dialogue_event_up_true");
            if (unsafeBox.characterDialogue is null) unsafeReasons.Add("dialogue_character_missing");
            else if (string.IsNullOrWhiteSpace(unsafeBox.characterDialogue.speaker?.Name)) unsafeReasons.Add("dialogue_speaker_name_missing_or_empty");
            if (!string.IsNullOrWhiteSpace(Game1.currentLocation?.lastQuestionKey)) unsafeReasons.Add("dialogue_last_question_key_present:" + Game1.currentLocation.lastQuestionKey);
            if (unsafeBox.transitioning) unsafeReasons.Add("dialogue_transitioning_true");
            var beforeSpeakerName = unsafeBox.characterDialogue?.speaker?.Name ?? string.Empty;
            return new TrainingExecutionResult
            {
                RunId = request.RunId,
                QueueId = request.QueueId,
                QueueItemId = request.QueueItemId,
                BeforeStateHash = request.BeforeStateHash,
                OptionId = request.OptionId,
                Status = "blocked",
                FeedbackAvailable = true,
                StartedAt = DateTimeOffset.UtcNow.ToString("O"),
                CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
                PrimitiveKind = "close_menu",
                PrimitiveVerificationStatus = "blocked",
                PrimitiveVerificationReasons = unsafeReasons.ToArray(),
                RequestedEffect = "menus.active_menu.is_open=false",
                ObservedEffect = CloseMenuObservedEffect(),
                BlockReasons = unsafeReasons.ToArray(),
                DialogueNativeHandled = false,
                DialoguePressAttempts = 0,
                DialogueAdvanceTicks = 0,
                DialogueMenuTypeBefore = "DialogueBox",
                DialogueMenuTypeAfter = "DialogueBox",
                DialogueIsQuestionBefore = unsafeBox.isQuestion,
                DialogueIsQuestionAfter = unsafeBox.isQuestion,
                DialogueResponseCountBefore = unsafeBox.responses?.Length ?? 0,
                DialogueResponseCountAfter = unsafeBox.responses?.Length ?? 0,
                DialogueSpeakerNameBefore = beforeSpeakerName,
                DialogueSpeakerNameAfter = beforeSpeakerName,
                DialogueEventUpBefore = Game1.eventUp,
                DialogueEventUpAfter = Game1.eventUp,
                ChangedFacts = new[]
                {
                    new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = "true", After = "true" },
                    new SimulatedFactChange { Path = "menus.active_menu.type", Before = "DialogueBox", After = "DialogueBox" }
                }
            };
        }

        if (!IsSafeCloseMenuType(beforeType))
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), "close_menu_type_not_whitelisted");
        }

        if (!menu.readyToClose())
        {
            return BlockedWithPrimitive(request, "close_menu", "menus.active_menu.is_open=false", CloseMenuObservedEffect(), "menu_not_ready_to_close");
        }

        Game1.exitActiveMenu();
        var verified = Game1.activeClickableMenu is null;
        return CompletedCloseMenu(
            request,
            beforeOpen,
            beforeType,
            verified ? "applied" : "blocked",
            verified ? "verified" : "observed_mismatch",
            verified ? new[] { "active_menu_closed" } : new[] { "active_menu_still_open" });
    }

    private static bool CanAdvanceOrdinaryDialogue(DialogueBox dialogueBox, bool allowSpeakerlessSocialContinuation = false)
    {
        return !dialogueBox.isQuestion &&
            (dialogueBox.responses is null || dialogueBox.responses.Length == 0) &&
            !string.Equals(Game1.currentLocation?.lastQuestionKey, "Sleep", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(Game1.currentLocation?.lastQuestionKey) &&
            !Game1.eventUp &&
            ((dialogueBox.characterDialogue is not null &&
                !string.IsNullOrWhiteSpace(dialogueBox.characterDialogue.speaker?.Name)) ||
                allowSpeakerlessSocialContinuation);
    }
}
