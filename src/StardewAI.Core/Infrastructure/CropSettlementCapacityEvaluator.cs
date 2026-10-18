using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public sealed record CropSettlementCapacityEvaluation(
    int SnapshotStartTime,
    int AvailableGameMinutes,
    int ExistingMaintenanceTicks,
    int TailMovementTileCount,
    int TailMovementTicks,
    int ShippingActionCount,
    int ShippingActionTicks,
    int RequiredQueueCount,
    int QueueItemCountPerTransition,
    int TotalTicks,
    int RequiredGameMinutes,
    int ShippingBinTileX,
    int ShippingBinTileY,
    int ShippingStandTileX,
    int ShippingStandTileY,
    bool TimeBudgetFits,
    bool AllTargetsReachable,
    bool ShippingBinReachable,
    string[] BlockingReasons);

public static class CropSettlementCapacityEvaluator
{
    public static CropSettlementCapacityEvaluation Evaluate(
        SnapshotEnvelope snapshot,
        string locationId,
        IReadOnlyCollection<CropMaintenanceTileTarget> maintenanceTargets,
        int existingMaintenanceTicks,
        int shippingActionCount)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (maintenanceTargets is null)
            throw new ArgumentNullException(nameof(maintenanceTargets));
        if (string.IsNullOrWhiteSpace(locationId))
            throw new ArgumentException(
                "Crop location is required.",
                nameof(locationId));
        if (maintenanceTargets.Count == 0)
            throw new ArgumentException(
                "At least one crop maintenance target is required.",
                nameof(maintenanceTargets));
        if (existingMaintenanceTicks < 0)
            throw new ArgumentOutOfRangeException(
                nameof(existingMaintenanceTicks));
        if (shippingActionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(shippingActionCount));

        var reasons = new List<string>();
        var startTime = ReadStateFieldIntOptional(snapshot, "time", "time");
        if (!startTime.HasValue || startTime.Value < 600 ||
            startTime.Value >= GameClockBudgetPolicy.AutonomousRecoveryStartTime)
        {
            reasons.Add(
                "crop_settlement_start_time_missing_or_outside_autonomous_window");
        }
        var maintenanceRoute = CropMaintenanceCapacityEvaluator.EvaluateRoute(
            snapshot,
            locationId,
            maintenanceTargets);
        if (!maintenanceRoute.HasValue)
            reasons.Add("crop_settlement_maintenance_route_unavailable");

        var shippingBin = ShippingBinRouteProjector.SelectFirstCompleted(
            snapshot);
        if (shippingBin is null)
            reasons.Add("crop_settlement_shipping_bin_route_unavailable");

        var tailTiles = 0;
        var shippingReachable = false;
        var collisionGrid = ReadStateFieldValue(
            snapshot,
            "locations",
            "collision_grid");
        if (maintenanceRoute.HasValue && shippingBin is not null &&
            collisionGrid.HasValue &&
            collisionGrid.Value.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(collisionGrid.Value, "location_id"),
                locationId,
                StringComparison.OrdinalIgnoreCase))
        {
            var width = ReadInt(collisionGrid.Value, "width");
            var height = ReadInt(collisionGrid.Value, "height");
            var blocked = RouteGridPathfinder.ReadBlockedTiles(
                collisionGrid.Value);
            foreach (var target in maintenanceTargets.Where(value =>
                         value.BlocksMovement))
            {
                blocked.Add(RouteGridPathfinder.Key(
                    target.TileX,
                    target.TileY));
            }
            var unsupported = RouteGridPathfinder.ReadUnsupportedActionTiles(
                snapshot);
            var distance = RouteGridPathfinder.ShortestPathLength(
                maintenanceRoute.Value.EndTileX,
                maintenanceRoute.Value.EndTileY,
                shippingBin.StandTileX,
                shippingBin.StandTileY,
                width,
                height,
                blocked,
                unsupported);
            if (distance.HasValue)
            {
                tailTiles = distance.Value;
                shippingReachable = true;
            }
            else
            {
                reasons.Add("crop_settlement_shipping_bin_unreachable");
            }
        }
        else if (shippingBin is not null)
        {
            reasons.Add("crop_settlement_collision_grid_unavailable");
        }

        var tailTicks = CropMaintenanceBudgetPolicy.MovementTicks(tailTiles);
        var shippingTicks = checked(
            shippingActionCount *
            ShippingSettlementBudgetPolicy.NativeSingleItemDepositTicks);
        var totalTicks = checked(
            existingMaintenanceTicks + tailTicks + shippingTicks);
        var requiredMinutes = GameClockBudgetPolicy.TicksToGameMinutes(
            totalTicks);
        var availableMinutes = startTime.HasValue
            ? Math.Max(0, GameClockBudgetPolicy.ClockMinutesBetween(
                startTime.Value,
                GameClockBudgetPolicy.AutonomousRecoveryStartTime))
            : 0;
        var timeFits = maintenanceRoute.HasValue && shippingReachable &&
            requiredMinutes <= availableMinutes;
        if (!timeFits)
            reasons.Add("crop_settlement_time_budget_exceeded");

        return new CropSettlementCapacityEvaluation(
            startTime ?? 0,
            availableMinutes,
            existingMaintenanceTicks,
            tailTiles,
            tailTicks,
            shippingActionCount,
            shippingTicks,
            shippingActionCount,
            1,
            totalTicks,
            requiredMinutes,
            shippingBin?.TileX ?? -1,
            shippingBin?.TileY ?? -1,
            shippingBin?.StandTileX ?? -1,
            shippingBin?.StandTileY ?? -1,
            timeFits,
            maintenanceRoute.HasValue,
            shippingReachable,
            reasons.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }
}
