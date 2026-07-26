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
    private void StartWait(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + (pending.Request.WaitTicks?.ToString() ?? "missing"), "executor.wait_ticks=0", reasons.ToArray()));
            return;
        }

        var waitTicks = pending.Request.WaitTicks ?? 0;
        if (waitTicks < 1 || waitTicks > 600)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + waitTicks, "executor.wait_ticks=0", "wait_ticks_1_600_required"));
            return;
        }

        if (activeWait is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "wait_ticks", "executor.wait_ticks=" + waitTicks, "executor.wait_ticks=0", "wait_executor_busy"));
            return;
        }

        activeWait = new ActiveWait(pending, waitTicks);
    }

    private void TickWait()
    {
        if (activeWait is null)
        {
            return;
        }

        activeWait.ElapsedTicks++;
        if (activeWait.ElapsedTicks < activeWait.TargetTicks)
        {
            return;
        }

        var wait = activeWait;
        activeWait = null;
        var request = wait.Pending.Request;
        wait.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = wait.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "wait_ticks",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "elapsed_ticks_reached_target" },
            RequestedEffect = "executor.wait_ticks=" + wait.TargetTicks,
            ObservedEffect = "executor.wait_ticks=" + wait.ElapsedTicks,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "executor.wait_ticks",
                    Before = "0",
                    After = wait.ElapsedTicks.ToString()
                }
            }
        });
    }

    private TrainingExecutionResult ExecuteAdvanceTimeTo(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + (request.TargetTime?.ToString() ?? "missing"), "time.time=" + Game1.timeOfDay, reasons.ToArray());
        }

        if (!request.TargetTime.HasValue || request.TargetTime.Value < 600 || request.TargetTime.Value > 2600 || request.TargetTime.Value % 10 != 0)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + (request.TargetTime?.ToString() ?? "missing"), "time.time=" + Game1.timeOfDay, "target_time_600_2600_step_10_required");
        }

        var before = Game1.timeOfDay;
        if (request.TargetTime.Value < before)
        {
            return BlockedWithPrimitive(request, "debug_advance_time_to", "time.time=" + request.TargetTime.Value, "time.time=" + before, "target_time_must_not_go_backward");
        }

        Game1.timeOfDay = request.TargetTime.Value;
        var verified = Game1.timeOfDay == request.TargetTime.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_advance_time_to",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "time_set_to_target_for_isolated_runtime_test" } : new[] { "time_set_mismatch" },
            RequestedEffect = "time.time=" + request.TargetTime.Value,
            ObservedEffect = "time.time=" + Game1.timeOfDay,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "time_set_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "time.time",
                    Before = before.ToString(),
                    After = Game1.timeOfDay.ToString()
                }
            }
        };
    }

    private TrainingExecutionResult ExecuteSetupClearObstacle(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_clear_obstacle", "current_location.obstacle[target]=terrain_feature:Grass", ClearObstacleObservedEffect(null), "target_tile_required");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!CanClearRouteObstacles(Game1.currentLocation) ||
            ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            MoveFixtureFarmerToFarmAdjacent(target);
        }

        var location = Game1.currentLocation;
        if (!CanClearRouteObstacles(location))
        {
            return BlockedWithPrimitive(request, "debug_setup_clear_obstacle", "current_location.obstacle[" + target.X + "," + target.Y + "]=terrain_feature:Grass", ClearObstacleObservedEffect(target), "setup_clear_obstacle_location_not_whitelisted");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var tile = new Vector2(target.X, target.Y);
        var before = ObstacleLabel(location, target);
        location.terrainFeatures.Remove(tile);
        location.objects.Remove(tile);
        location.terrainFeatures[tile] = new Grass(Grass.springGrass, 4);

        var after = ObstacleLabel(location, target);
        var verified = location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Grass;
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
            PrimitiveKind = "debug_setup_clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_grass_obstacle" }
                : new[] { "fixture_grass_obstacle_not_visible" },
            RequestedEffect = "current_location.obstacle[" + target.X + "," + target.Y + "]=terrain_feature:Grass",
            ObservedEffect = "before=" + before + ";after=" + after,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_grass_obstacle_not_visible" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                    Before = before,
                    After = after
                }
            }
        };
    }

    private static void MoveFixtureFarmerToFarmAdjacent(Point target)
    {
        MoveFixtureFarmerToFarmAdjacent(target, out _, out _);
    }

    private static bool MoveFixtureFarmerToFarmAdjacent(Point target, out Point standTile, out string blockReason)
    {
        var farm = Game1.getFarm();
        return MoveFixtureFarmerToLocationAdjacent(
            farm,
            target,
            out standTile,
            out blockReason);
    }

    private static bool MoveFixtureFarmerToLocationAdjacent(
        GameLocation location,
        Point target,
        out Point standTile,
        out string blockReason)
    {
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        foreach (var candidate in Neighbors(target)
            .Where(tile =>
                IsTileOnMap(location, tile) &&
                IsTileWalkable(location, tile))
            .OrderBy(tile => ManhattanDistance(Game1.player.TilePoint, tile)))
        {
            standTile = candidate;
            blockReason = string.Empty;
            Game1.player.Position = new Vector2(candidate.X * Game1.tileSize, candidate.Y * Game1.tileSize);
            Game1.player.faceDirection(DirectionTo(candidate, target));
            return true;
        }

        standTile = Point.Zero;
        blockReason = "fixture_no_collision_safe_adjacent_tile";
        return false;
    }

    private TrainingExecutionResult ExecuteSetupWateringTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_watering_target", "farm.crops[target].needs_watering=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforeTile = Game1.player.TilePoint;
        if (farm.objects.ContainsKey(tile))
        {
            farm.objects.Remove(tile);
        }

        if (farm.terrainFeatures.TryGetValue(tile, out var existing) && existing is not HoeDirt)
        {
            farm.terrainFeatures.Remove(tile);
        }

        var dirt = farm.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt existingDirt
            ? existingDirt
            : new HoeDirt(0, farm);
        dirt.state.Value = HoeDirt.dry;
        dirt.crop = new Crop("472", request.TargetTileX.Value, request.TargetTileY.Value, farm);
        farm.terrainFeatures[tile] = dirt;
        var fixtureMoved = MoveFixtureFarmerToFarmAdjacent(target, out var standTile, out var fixtureMoveReason);

        var verified = farm.terrainFeatures.TryGetValue(tile, out var afterFeature) &&
            afterFeature is HoeDirt afterDirt &&
            afterDirt.crop is not null &&
            afterDirt.needsWatering() &&
            !afterDirt.isWatered() &&
            fixtureMoved &&
            Game1.currentLocation == farm &&
            Game1.player.TilePoint == standTile &&
            AreAdjacent(Game1.player.TilePoint, target);

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
            PrimitiveKind = "debug_setup_watering_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_crop_needs_watering", "fixture_farmer_on_farm_adjacent_to_target" }
                : new[] { fixtureMoved ? "fixture_crop_not_waterable" : fixtureMoveReason },
            RequestedEffect = "farm.crops[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].needs_watering=true;player.location_id=Farm;player.adjacent_to_target=true",
            ObservedEffect = "needs_watering=" + (afterFeature is HoeDirt observedDirt && observedDirt.needsWatering()).ToString().ToLowerInvariant() + ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? string.Empty) + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.X + "," + target.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { fixtureMoved ? "fixture_crop_not_waterable" : fixtureMoveReason },
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[" + target.X + "," + target.Y + "].needs_watering",
                        Before = "unknown",
                        After = "true"
                    },
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = farm.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.tile", Before = beforeTile.X + "," + beforeTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                    new SimulatedFactChange { Path = "player.facing_direction", Before = "unknown", After = Game1.player.FacingDirection.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupTillSoilTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var farm = Game1.getFarm();
        var selectedTarget = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : FindTillSoilFixtureTarget(farm);
        if (!selectedTarget.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_till_soil_target", "farm.terrain_features[target].type=none;player.location_id=Farm;player.adjacent_to_target=true", TillSoilObservedEffect(farm, null), "till_soil_fixture_no_diggable_candidate");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var target = selectedTarget.Value;
        var tile = new Vector2(target.X, target.Y);
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforePlayerTile = Game1.player.TilePoint;
        var beforeFeature = farm.terrainFeatures.TryGetValue(tile, out var existingFeature) ? existingFeature.GetType().Name : "none";
        var beforeObject = farm.objects.ContainsKey(tile).ToString().ToLowerInvariant();

        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        farm.objects.Remove(tile);
        farm.terrainFeatures.Remove(tile);

        var fixtureMoved = MoveFixtureFarmerToFarmAdjacent(target, out var standTile, out var fixtureMoveReason);
        var precheck = ValidateTillSoilTarget(farm, target, FindTool<Hoe>());
        var verified = fixtureMoved &&
            precheck.Length == 0 &&
            Game1.currentLocation == farm &&
            Game1.player.TilePoint == standTile &&
            AreAdjacent(Game1.player.TilePoint, target) &&
            !farm.terrainFeatures.TryGetValue(tile, out _) &&
            !farm.objects.ContainsKey(tile);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = farm.NameOrUniqueName,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_till_soil_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_diggable_untilled_tile", "fixture_farmer_on_farm_adjacent_to_target" }
                : fixtureMoved ? precheck.DefaultIfEmpty("till_soil_fixture_state_mismatch").ToArray() : new[] { fixtureMoveReason },
            RequestedEffect = "farm.terrain_features[" + target.X + "," + target.Y + "].type=none;player.location_id=Farm;player.adjacent_to_target=true",
            ObservedEffect = TillSoilObservedEffect(farm, target),
            BlockReasons = verified ? Array.Empty<string>() : fixtureMoved ? precheck.DefaultIfEmpty("till_soil_fixture_state_mismatch").ToArray() : new[] { fixtureMoveReason },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "farm.terrain_features[" + target.X + "," + target.Y + "].type", Before = beforeFeature, After = "none" },
                    new SimulatedFactChange { Path = "farm.objects[" + target.X + "," + target.Y + "].present", Before = beforeObject, After = "false" },
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = farm.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.tile", Before = beforePlayerTile.X + "," + beforePlayerTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                    new SimulatedFactChange { Path = "player.facing_direction", Before = "unknown", After = Game1.player.FacingDirection.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindTillSoilFixtureTarget(Farm farm)
    {
        var dimensions = MapDimensions(farm);
        for (var y = 0; y < dimensions.Y; y++)
        {
            for (var x = 0; x < dimensions.X; x++)
            {
                var target = new Point(x, y);
                var tile = new Vector2(x, y);
                if (farm.doesTileHaveProperty(x, y, "Diggable", "Back") is null ||
                    farm.terrainFeatures.ContainsKey(tile) ||
                    farm.objects.ContainsKey(tile) ||
                    farm.IsTileBlockedBy(tile, ~(CollisionMask.Characters | CollisionMask.Farmers)) ||
                    !Neighbors(target).Any(stand => IsTileOnMap(farm, stand) && IsTileWalkable(farm, stand)))
                {
                    continue;
                }

                return target;
            }
        }

        return null;
    }

    private TrainingExecutionResult ExecuteSetupFishFrenzy(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "target_tile=missing", "target_tile_required");
        }

        var location = Game1.currentLocation;
        var tile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!location.canFishHere() || !location.isTileFishable(tile.X, tile.Y))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "fishable_tile=false", "fish_frenzy_fixture_tile_not_fishable");
        }

        Item fish;
        try
        {
            fish = ItemRegistry.Create(request.QualifiedItemId);
        }
        catch
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "qualified_item=invalid", "fish_frenzy_fixture_item_invalid");
        }

        if (fish.Category != StardewValley.Object.FishCategory)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_frenzy", "current_location.fish_frenzy.active=true", "qualified_item=" + fish.QualifiedItemId, "fish_frenzy_fixture_item_not_fish");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeFish = location.fishFrenzyFish.Value ?? string.Empty;
        var beforePoint = location.fishSplashPoint.Value;
        var frenzyTimeField = Helper.Reflection.GetField<int>(location, "fishSplashPointTime");
        var beforeFrenzyTime = frenzyTimeField.GetValue();
        location.fishFrenzyFish.Value = fish.QualifiedItemId;
        location.fishSplashPoint.Value = tile;
        frenzyTimeField.SetValue(Game1.timeOfDay);
        var verified = string.Equals(location.fishFrenzyFish.Value, fish.QualifiedItemId, StringComparison.Ordinal) &&
            location.fishSplashPoint.Value == tile &&
            frenzyTimeField.GetValue() == Game1.timeOfDay;

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
            PrimitiveKind = "debug_setup_fish_frenzy",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_fish_frenzy_active" }
                : new[] { "fish_frenzy_fixture_state_mismatch" },
            RequestedEffect = "current_location.fish_frenzy.active=true;qualified_item_id=" + fish.QualifiedItemId + ";center_tile=" + tile.X + "," + tile.Y + ";start_time=" + Game1.timeOfDay,
            ObservedEffect = "active=" + verified.ToString().ToLowerInvariant() + ";qualified_item_id=" + (location.fishFrenzyFish.Value ?? string.Empty) + ";center_tile=" + location.fishSplashPoint.Value.X + "," + location.fishSplashPoint.Value.Y + ";start_time=" + frenzyTimeField.GetValue(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fish_frenzy_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.qualified_item_id", Before = beforeFish, After = fish.QualifiedItemId },
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.center_tile", Before = beforePoint.X + "," + beforePoint.Y, After = tile.X + "," + tile.Y },
                    new SimulatedFactChange { Path = "current_location.fish_frenzy.start_time", Before = beforeFrenzyTime.ToString(), After = Game1.timeOfDay.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteSetupFishPond(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (Game1.currentLocation is not Farm farm || !ReferenceEquals(farm, Game1.getFarm()))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "location=" + Game1.currentLocation?.NameOrUniqueName, "fish_pond_fixture_requires_farm_location");
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "top_left_tile=missing", "target_tile_required");
        }

        Item fish;
        try
        {
            fish = ItemRegistry.Create(request.QualifiedItemId);
        }
        catch
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "qualified_item=invalid", "fish_pond_fixture_item_invalid");
        }

        if (fish.Category != StardewValley.Object.FishCategory || fish.HasContextTag("fish_legendary"))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "qualified_item=" + fish.QualifiedItemId, "fish_pond_fixture_item_not_legal_fish");
        }

        var requestedTopLeft = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var selectedTopLeft = FindFishPondFixturePlacement(farm, requestedTopLeft);
        if (!selectedTopLeft.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "requested_top_left_tile=" + request.TargetTileX.Value + "," + request.TargetTileY.Value, "fish_pond_fixture_no_legal_placement");
        }

        var topLeft = new Vector2(selectedTopLeft.Value.X, selectedTopLeft.Value.Y);
        var pond = new FishPond(topLeft);
        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeBuildingCount = farm.buildings.Count;
        if (!farm.buildStructure(pond, topLeft, Game1.player, skipSafetyChecks: false))
        {
            return BlockedWithPrimitive(request, "debug_setup_fish_pond", "current_location.fish_pond.catch_available=true", "top_left_tile=" + selectedTopLeft.Value.X + "," + selectedTopLeft.Value.Y, "fish_pond_fixture_placement_rejected");
        }

        pond.daysOfConstructionLeft.Value = 0;
        pond.fishType.Value = fish.ItemId;
        pond.currentOccupants.Value = 1;
        var fishableTile = new Vector2(pond.tileX.Value + 1, pond.tileY.Value + 1);
        var verified = farm.buildings.Contains(pond) &&
            pond.daysOfConstructionLeft.Value == 0 &&
            pond.FishCount == 1 &&
            string.Equals(pond.fishType.Value, fish.ItemId, StringComparison.Ordinal) &&
            pond.isTileFishable(fishableTile);

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
            PrimitiveKind = "debug_setup_fish_pond",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_fish_pond_catch_available" }
                : new[] { "fish_pond_fixture_state_mismatch" },
            RequestedEffect = "current_location.fish_pond.catch_available=true;qualified_item_id=" + fish.QualifiedItemId + ";top_left_tile=" + pond.tileX.Value + "," + pond.tileY.Value + ";fish_count=1",
            ObservedEffect = "building_present=" + farm.buildings.Contains(pond).ToString().ToLowerInvariant() + ";qualified_item_id=(O)" + (pond.fishType.Value ?? string.Empty) + ";top_left_tile=" + pond.tileX.Value + "," + pond.tileY.Value + ";fish_count=" + pond.FishCount + ";fishable_tile=" + (int)fishableTile.X + "," + (int)fishableTile.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fish_pond_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "farm.buildings.count", Before = beforeBuildingCount.ToString(), After = farm.buildings.Count.ToString() },
                    new SimulatedFactChange { Path = "current_location.fish_pond.top_left_tile", Before = string.Empty, After = pond.tileX.Value + "," + pond.tileY.Value },
                    new SimulatedFactChange { Path = "current_location.fish_pond.qualified_item_id", Before = string.Empty, After = fish.QualifiedItemId },
                    new SimulatedFactChange { Path = "current_location.fish_pond.fish_count", Before = "0", After = pond.FishCount.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindFishPondFixturePlacement(Farm farm, Point requestedTopLeft)
    {
        var probe = new FishPond(Vector2.Zero);
        var layer = farm.map?.Layers.FirstOrDefault();
        if (layer is null)
        {
            return null;
        }

        var candidates = new List<Point> { requestedTopLeft };
        for (var y = 1; y <= layer.LayerHeight - probe.tilesHigh.Value - 1; y++)
        {
            for (var x = 1; x <= layer.LayerWidth - probe.tilesWide.Value - 1; x++)
            {
                candidates.Add(new Point(x, y));
            }
        }

        foreach (var candidate in candidates
            .Distinct()
            .OrderBy(candidate => ManhattanDistance(candidate, requestedTopLeft))
            .ThenBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X))
        {
            if (CanPlaceFishPondFixture(farm, probe, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool CanPlaceFishPondFixture(Farm farm, FishPond pond, Point topLeft)
    {
        for (var y = 0; y < pond.tilesHigh.Value; y++)
        {
            for (var x = 0; x < pond.tilesWide.Value; x++)
            {
                if (!farm.isBuildable(new Vector2(topLeft.X + x, topLeft.Y + y)))
                {
                    return false;
                }
            }
        }

        return pond.isThereAnythingtoPreventConstruction(farm, new Vector2(topLeft.X, topLeft.Y)) is null;
    }
}
