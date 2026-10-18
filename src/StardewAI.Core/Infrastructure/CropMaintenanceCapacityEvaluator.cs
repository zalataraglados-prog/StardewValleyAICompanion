using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public sealed record CropMaintenanceTileTarget(
    int TileX,
    int TileY,
    bool Plant,
    bool Water,
    bool Harvest,
    bool BlocksMovement);

public sealed record CropMaintenanceCapacityEvaluation(
    int SnapshotStartTime,
    int AvailableGameMinutes,
    int PlantActionCount,
    int WaterActionCount,
    int HarvestActionCount,
    int MovementTileCount,
    int MovementTicks,
    int ActionTicks,
    int TotalTicks,
    int RequiredGameMinutes,
    double AvailableEnergy,
    double RequiredEnergy,
    double MinimumEnergyReserve,
    int? WateringCanSlotIndex,
    int? WateringCanWaterLeft,
    int? WateringCanCapacity,
    bool? WateringCanBottomless,
    bool TimeBudgetFits,
    bool EnergyBudgetFits,
    bool WaterBudgetFits,
    bool AllTargetsReachable,
    string[] BlockingReasons);

public readonly record struct CropMaintenanceRouteEvaluation(
    int MovementTileCount,
    int EndTileX,
    int EndTileY);

public static class CropMaintenanceCapacityEvaluator
{
    public static CropMaintenanceCapacityEvaluation Evaluate(
        SnapshotEnvelope snapshot,
        string locationId,
        IReadOnlyCollection<CropMaintenanceTileTarget> targets)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (targets is null)
            throw new ArgumentNullException(nameof(targets));
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException("Crop location is required.", nameof(locationId));
        if (targets.Count == 0)
            throw new ArgumentException("At least one crop tile is required.", nameof(targets));
        var orderedTargets = targets
            .OrderBy(value => value.TileY)
            .ThenBy(value => value.TileX)
            .ToArray();
        if (orderedTargets.Select(value => (value.TileX, value.TileY))
                .Distinct().Count() != orderedTargets.Length)
            throw new InvalidDataException("Crop capacity targets are duplicated.");
        if (orderedTargets.Any(value =>
                !value.Plant && !value.Water && !value.Harvest))
        {
            throw new InvalidDataException(
                "Every crop capacity target must carry at least one operation.");
        }

        var reasons = new List<string>();
        var startTime = ReadStateFieldIntOptional(snapshot, "time", "time");
        var playerLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        var availableEnergy = ReadStateFieldDouble(snapshot, "player", "energy");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "time", "time")) ||
            !startTime.HasValue || startTime.Value < 600 || startTime.Value >=
            GameClockBudgetPolicy.AutonomousRecoveryStartTime)
        {
            reasons.Add("crop_capacity_start_time_missing_or_outside_autonomous_window");
        }
        if (!ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "player",
                "location_id")) ||
            !string.Equals(
                playerLocation,
                locationId,
                StringComparison.OrdinalIgnoreCase))
            reasons.Add("crop_capacity_player_location_mismatch");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "tile_x")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "player", "tile_y")) ||
            !startX.HasValue || !startY.HasValue)
            reasons.Add("crop_capacity_player_tile_missing");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "energy")) ||
            !availableEnergy.HasValue || availableEnergy.Value < 0d)
            reasons.Add("crop_capacity_player_energy_missing_or_invalid");

        var collisionGrid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
        var width = 0;
        var height = 0;
        var movementTiles = 0;
        var allTargetsReachable = false;
        if (!ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "locations",
                "collision_grid")) ||
            !collisionGrid.HasValue ||
            collisionGrid.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(collisionGrid.Value, "location_id"),
                locationId,
                StringComparison.OrdinalIgnoreCase) ||
            (width = ReadInt(collisionGrid.Value, "width")) <= 0 ||
            (height = ReadInt(collisionGrid.Value, "height")) <= 0)
        {
            reasons.Add("crop_capacity_collision_grid_missing_or_mismatched");
        }
        else if (orderedTargets.Any(value =>
                     value.TileX < 0 || value.TileX >= width ||
                     value.TileY < 0 || value.TileY >= height))
        {
            reasons.Add("crop_capacity_target_outside_collision_grid");
        }
        else if (startX.HasValue && startY.HasValue)
        {
            var blocked = RouteGridPathfinder.ReadBlockedTiles(collisionGrid.Value);
            var unsupported = RouteGridPathfinder.ReadUnsupportedActionTiles(snapshot);
            foreach (var target in orderedTargets.Where(value =>
                         value.BlocksMovement))
            {
                blocked.Add(RouteGridPathfinder.Key(
                    target.TileX,
                    target.TileY));
            }

            var route = BuildRoute(
                startX.Value,
                startY.Value,
                width,
                height,
                blocked,
                unsupported,
                orderedTargets);
            if (route.HasValue)
            {
                movementTiles = route.Value.MovementTileCount;
                allTargetsReachable = true;
            }
            else
            {
                reasons.Add("crop_capacity_target_route_unreachable");
            }
        }

        var plantCount = orderedTargets.Count(value => value.Plant);
        var waterCount = orderedTargets.Count(value => value.Water);
        var harvestCount = orderedTargets.Count(value => value.Harvest);
        var movementTicks = CropMaintenanceBudgetPolicy.MovementTicks(
            movementTiles);
        var actionTicks = checked(
            CropMaintenanceBudgetPolicy.PlantingTicks(plantCount) +
            CropMaintenanceBudgetPolicy.WateringTicks(waterCount) +
            CropHarvestBudgetPolicy.HarvestTicksPerCrop * harvestCount);
        var totalTicks = checked(movementTicks + actionTicks);
        var requiredMinutes = GameClockBudgetPolicy.TicksToGameMinutes(totalTicks);
        var availableMinutes = startTime.HasValue
            ? Math.Max(0, GameClockBudgetPolicy.ClockMinutesBetween(
                startTime.Value,
                GameClockBudgetPolicy.AutonomousRecoveryStartTime))
            : 0;

        int? canSlot = null;
        int? waterLeft = null;
        int? waterCapacity = null;
        bool? bottomless = null;
        var energyPerWater = 0d;
        var waterBudgetFits = waterCount == 0;
        if (waterCount > 0)
        {
            var can = ReadStateFieldValue(snapshot, "player", "watering_can_state");
            if (!ReadableStatus(ReadStateFieldStatus(
                    snapshot,
                    "player",
                    "watering_can_state")) ||
                !TryReadExactWateringCan(
                    can,
                    out canSlot,
                    out waterLeft,
                    out waterCapacity,
                    out bottomless,
                    out energyPerWater))
            {
                reasons.Add("crop_capacity_exact_watering_can_state_missing");
            }
            else
            {
                waterBudgetFits = bottomless == true || waterLeft >= waterCount;
                if (!waterBudgetFits)
                {
                    reasons.Add(
                        "crop_capacity_watering_can_requires_unmodeled_refill_route");
                }
            }
        }

        var requiredEnergy = waterCount * energyPerWater;
        var timeFits = allTargetsReachable && startTime.HasValue &&
            requiredMinutes <= availableMinutes;
        var energyFits = availableEnergy.HasValue &&
            availableEnergy.Value - requiredEnergy >=
            CropMaintenanceBudgetPolicy.MinimumEnergyReserve;
        if (!timeFits)
            reasons.Add("crop_capacity_time_budget_exceeded");
        if (!energyFits)
            reasons.Add("crop_capacity_energy_budget_exceeded");

        return new CropMaintenanceCapacityEvaluation(
            startTime ?? 0,
            availableMinutes,
            plantCount,
            waterCount,
            harvestCount,
            movementTiles,
            movementTicks,
            actionTicks,
            totalTicks,
            requiredMinutes,
            availableEnergy ?? 0d,
            requiredEnergy,
            CropMaintenanceBudgetPolicy.MinimumEnergyReserve,
            canSlot,
            waterLeft,
            waterCapacity,
            bottomless,
            timeFits,
            energyFits,
            waterBudgetFits,
            allTargetsReachable,
            reasons.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    public static CropMaintenanceRouteEvaluation? EvaluateRoute(
        SnapshotEnvelope snapshot,
        string locationId,
        IReadOnlyCollection<CropMaintenanceTileTarget> targets)
    {
        var playerLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
        var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
        var collisionGrid = ReadStateFieldValue(
            snapshot,
            "locations",
            "collision_grid");
        if (!string.Equals(
                playerLocation,
                locationId,
                StringComparison.OrdinalIgnoreCase) ||
            !startX.HasValue ||
            !startY.HasValue ||
            !collisionGrid.HasValue ||
            collisionGrid.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(collisionGrid.Value, "location_id"),
                locationId,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var width = ReadInt(collisionGrid.Value, "width");
        var height = ReadInt(collisionGrid.Value, "height");
        if (width <= 0 || height <= 0)
            return null;
        var blocked = RouteGridPathfinder.ReadBlockedTiles(
            collisionGrid.Value);
        var unsupported = RouteGridPathfinder.ReadUnsupportedActionTiles(
            snapshot);
        foreach (var target in targets.Where(value => value.BlocksMovement))
        {
            blocked.Add(RouteGridPathfinder.Key(
                target.TileX,
                target.TileY));
        }
        return BuildRoute(
            startX.Value,
            startY.Value,
            width,
            height,
            blocked,
            unsupported,
            targets);
    }

    private static CropMaintenanceRouteEvaluation? BuildRoute(
        int startX,
        int startY,
        int width,
        int height,
        ISet<string> blocked,
        ISet<string> unsupported,
        IReadOnlyCollection<CropMaintenanceTileTarget> targets)
    {
        var remaining = targets.ToList();
        var currentX = startX;
        var currentY = startY;
        var total = 0;
        while (remaining.Count > 0)
        {
            var distances = RouteGridPathfinder.DistancesFrom(
                currentX,
                currentY,
                width,
                height,
                blocked,
                unsupported);
            var choices = remaining
                .SelectMany(target => RouteGridPathfinder
                    .Neighbors(target.TileX, target.TileY)
                    .Select(stand => new
                    {
                        Target = target,
                        Stand = stand,
                        Distance = RouteGridPathfinder.InBounds(
                                stand.X,
                                stand.Y,
                                width,
                                height)
                            ? distances[stand.Y * width + stand.X]
                            : -1
                    }))
                .Where(value => value.Distance >= 0)
                .OrderBy(value => value.Distance)
                .ThenBy(value => value.Target.TileY)
                .ThenBy(value => value.Target.TileX)
                .ThenBy(value => value.Stand.Y)
                .ThenBy(value => value.Stand.X)
                .ToArray();
            if (choices.Length == 0)
                return null;

            var selected = choices[0];
            total = checked(total + selected.Distance);
            currentX = selected.Stand.X;
            currentY = selected.Stand.Y;
            remaining.Remove(selected.Target);
        }
        return new CropMaintenanceRouteEvaluation(
            total,
            currentX,
            currentY);
    }

    private static bool TryReadExactWateringCan(
        JsonElement? field,
        out int? slot,
        out int? waterLeft,
        out int? capacity,
        out bool? bottomless,
        out double energyPerWater)
    {
        slot = null;
        waterLeft = null;
        capacity = null;
        bottomless = null;
        energyPerWater = 0d;
        if (!field.HasValue || field.Value.ValueKind != JsonValueKind.Object ||
            ReadString(field.Value, "projection_status") !=
                "exact_vanilla_power_zero" ||
            ReadString(field.Value, "runtime_type") !=
                "StardewValley.Tools.WateringCan")
        {
            return false;
        }

        slot = NullableReadInt(field.Value, "slot_index");
        waterLeft = NullableReadInt(field.Value, "water_left");
        capacity = NullableReadInt(field.Value, "capacity");
        bottomless = ReadBool(field.Value, "is_bottomless");
        var energy = NullableReadDouble(field.Value, "energy_cost_per_power_zero_action");
        if (!slot.HasValue || slot < 0 ||
            !waterLeft.HasValue || waterLeft < 0 ||
            !capacity.HasValue || capacity <= 0 || waterLeft > capacity ||
            !bottomless.HasValue ||
            !energy.HasValue || energy < 0d || energy > 2d)
        {
            return false;
        }

        energyPerWater = energy.Value;
        return true;
    }

    private static double? ReadStateFieldDouble(
        SnapshotEnvelope snapshot,
        string section,
        string name)
    {
        var value = ReadStateFieldValue(snapshot, section, name);
        return value.HasValue && value.Value.ValueKind == JsonValueKind.Number &&
            value.Value.TryGetDouble(out var parsed)
                ? parsed
                : null;
    }

    private static int? NullableReadInt(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.Number &&
        field.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static double? NullableReadDouble(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) &&
        field.ValueKind == JsonValueKind.Number &&
        field.TryGetDouble(out var parsed)
            ? parsed
            : null;
}
