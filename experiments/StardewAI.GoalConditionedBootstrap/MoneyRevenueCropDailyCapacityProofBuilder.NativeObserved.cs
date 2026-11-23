using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropDailyCapacityProofBuilder
{
    private sealed record NativeRefillFlow(RefillReceiptState Receipt, string AfterPath, string AfterSha256, string AfterStateHash,
        int MovementTicks, int RefillTicks, HashSet<(string OptionId, string LocationId, int X, int Y)> CropWork);

    private static CropMaintenanceCapacityEvaluation EvaluateNativeSatisfiedDay(SnapshotEnvelope snapshot,
        LayoutState[] layouts, int day)
    {
        Require(layouts.All(row => row.Commitment.PlantingTotalDay != day &&
                row.Commitment.HarvestWaves.All(wave => wave.HarvestTotalDay != day)),
            "Native satisfied capacity cannot omit planting or harvest work.");
        var due = layouts.Where(row => row.Commitment.RequiredWateringTotalDays.Contains(day)).ToArray();
        Require(due.Length > 0, "Native satisfied capacity has no original due obligation.");
        foreach (var layout in due)
            foreach (var tile in layout.Proof.Tiles)
                RequireSatisfiedCrop(snapshot, layout, tile.TileX, tile.TileY);
        var time = NativeExecutionSourceSupport.Value(snapshot, "time", "time").GetInt32();
        var energy = NativeExecutionSourceSupport.Value(snapshot, "player", "energy").GetDouble();
        Require(time is >= 600 and <= 2600 && time % 100 < 60 && double.IsFinite(energy) && energy >= 0 &&
                !string.IsNullOrWhiteSpace(NativeExecutionSourceSupport.Value(snapshot, "player", "location_id").GetString()),
            "Native satisfied capacity source time/energy/location is unreadable.");
        var available = Math.Max(0, GameClockBudgetPolicy.ClockMinutesBetween(time, GameClockBudgetPolicy.AutonomousRecoveryStartTime));
        // No mechanical work means no claimed Farm route, tool use or energy reserve expenditure.
        return new(time, available, 0, 0, 0, 0, 0, 0, 0, 0, energy, 0, 0,
            null, null, null, null, false, 0, true, true, true, true, Array.Empty<string>());
    }

    private static void RequireSatisfiedCrop(SnapshotEnvelope snapshot, LayoutState layout, int x, int y)
    {
        var crops = NativeExecutionSourceSupport.Value(snapshot, "farm", "crops");
        Require(crops.ValueKind == JsonValueKind.Array, "Native satisfied crop projection is missing.");
        var rows = crops.EnumerateArray().Where(row => TryReadInt(row, "tile_x", out var tx) && tx == x &&
            TryReadInt(row, "tile_y", out var ty) && ty == y).ToArray();
        Require(rows.Length == 1, "Native satisfied crop tile is missing or duplicated.");
        var crop = rows.Single();
        var seed = ReadString(crop, "harvest_source_seed_id");
        if (!seed.StartsWith("(O)", StringComparison.Ordinal)) seed = "(O)" + seed;
        Require(ReadString(crop, "location_id") == layout.Proof.LocationId && seed == layout.Commitment.SeedQualifiedItemId &&
                ReadString(crop, "harvest_item_qualified_id") == layout.Commitment.HarvestItemQualifiedId &&
                !ReadRequiredBool(crop, "dead") && !ReadRequiredBool(crop, "needs_watering"),
            "Native satisfied crop differs from the original live already-satisfied obligation.");
    }

    private static Dictionary<int, NativeRefillFlow> ReadObservedRefillFlows(
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions, IReadOnlyDictionary<int, string> snapshotPaths,
        Func<string, SnapshotState> readSnapshot)
    {
        Require(transitions.Length > 0 && transitions.All(row => row is not null) &&
                transitions.Select(row => Path.GetFullPath(row.QueuePath)).Distinct(PathComparer).Count() == transitions.Length &&
                snapshotPaths.Keys.All(day => transitions.Any(row => row.TotalDay == day)),
            "Native day transition denominator is missing or duplicated.");
        var result = new Dictionary<int, NativeRefillFlow>();
        foreach (var group in transitions.GroupBy(row => row.TotalDay))
        {
            var rows = group.ToArray();
            var refillRows = rows.Where(row => HasObservedRefill(row.ExecutionReceiptPath)).ToArray();
            if (refillRows.Length == 0) continue;
            Require(snapshotPaths.TryGetValue(group.Key, out var dayPath), "Native refill is outside the original day denominator.");
            var day = readSnapshot(dayPath!);
            var first = Array.FindIndex(rows, row => PathComparer.Equals(Path.GetFullPath(row.BeforeSnapshotPath), day.Path));
            var last = Array.FindLastIndex(rows, row => row.TransitionKind == "crop_actions");
            Require(first >= 0 && last >= first && refillRows.All(row =>
                    Array.IndexOf(rows, row) >= first && Array.IndexOf(rows, row) <= last),
                "Native refill does not continue the original complete day prefix.");
            var previous = day.Snapshot;
            var ticks = 0;
            var movementTicks = 0; var refillTicks = 0;
            var cropWork = new HashSet<(string OptionId, string LocationId, int X, int Y)>();
            SnapshotEnvelope? final = null;
            foreach (var row in rows.Skip(first).Take(last - first + 1))
            {
                Require(row.ExecutorVersion is PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor or PolicyTrajectoryVersionPins.ProductExecutor,
                    "Native refill prefix changed its declared executor.");
                var native = ReadObservedPrefixQueue(row);
                Require(previous.StateHash == native.Before.StateHash && previous.GameTick == native.Before.GameTick,
                    "Native refill prefix changed its preceding frame.");
                for (var index = 0; index < native.Actions.Length; index++)
                {
                    var frame = native.Actions[index];
                    CropTeacherNativeStepBoundary.Verify(frame.Before, frame.After, day.Snapshot,
                        native.Receipt.StepResults[index], day.TotalDay, "Native observed refill prefix");
                    var actual = frame.Result.ActualTicks ?? 0;
                    Require(actual > 0,
                        "Native refill prefix has no observed action ticks.");
                    ticks = checked(ticks + actual);
                    if (frame.Item.OptionId is "executor.move_to_tile" or "executor.traverse_connector")
                        movementTicks = checked(movementTicks + actual);
                    if (IsObservedRefill(frame.Result)) refillTicks = checked(refillTicks + actual);
                    if (frame.Result.Status == "applied" && frame.Item.OptionId is "executor.plant_seed" or "executor.water_crop" or "executor.harvest_crop" &&
                             frame.Result.TargetTileX is { } x && frame.Result.TargetTileY is { } y)
                        cropWork.Add((frame.Item.OptionId, frame.Result.TargetLocation, x, y));
                }
                previous = native.After;
                final = native.After;
            }
            Require(ticks > 0 && final is not null, "Native refill prefix is empty.");
            var receipt = refillRows[0].ExecutionReceiptPath;
            var afterPath = rows[last].AfterSnapshotPath;
            result.Add(day.TotalDay, new(new(receipt, CurrentTeacherFrontierSupport.HashFile(receipt), ticks),
                afterPath, CurrentTeacherFrontierSupport.HashFile(afterPath), final!.StateHash, movementTicks, refillTicks, cropWork));
        }
        return result;
    }

    private static VerifiedNativeExecutionQueue ReadObservedPrefixQueue(MoneyRevenueCropNativeLifecycleTransitionRequest row)
    {
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(row.ExecutionReceiptPath,
            "native refill prefix receipt");
        if (!receipt.Success && row.TransitionKind == "crop_actions")
            return NativeExecutionActionSourceReader.ReadOwnedCropPrefix(row);
        var product = row.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor;
        var native = product
            ? NativeExecutionActionSourceReader.ReadSelectedProductActionQueue(row.QueuePath, row.BeforeSnapshotPath,
                row.ExecutionReceiptPath, row.AfterSnapshotPath, row.RunId, row.SelectedCandidateId)
            : NativeExecutionActionSourceReader.ReadSelectedNativeActionQueue(row.QueuePath, row.BeforeSnapshotPath,
                row.ExecutionReceiptPath, row.AfterSnapshotPath, row.RunId, row.SelectedCandidateId);
        Require(native.Actions.All(frame => new Uri(frame.Endpoint, UriKind.Absolute).AbsolutePath ==
                (product ? "/api/v1/product/execute" : "/api/v1/training/execute")),
            "Native refill prefix executor differs from its verified endpoint.");
        return native;
    }

    private static bool HasObservedRefill(string path)
    {
        var receipt = CurrentTeacherFrontierSupport.Read<JsonElement>(path, "native observed refill discovery");
        return receipt.GetProperty("step_results").EnumerateArray().Any(step =>
            ReadString(step, "option_id") == "executor.water_crop" &&
            step.TryGetProperty("execution_path", out var execution) && execution.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(execution.GetString()) &&
            IsObservedRefill(CurrentTeacherFrontierSupport.Read<TrainingExecutionResult>(execution.GetString()!, "native refill execution result")));
    }

    private static bool IsObservedRefill(TrainingExecutionResult result) => result.OptionId == "executor.water_crop" &&
        result.WaterBefore == 0 && result.WaterAfter > 0;

    private static CropMaintenanceCapacityEvaluation ApplyObservedRefillFlow(SnapshotState day, LayoutState[] layouts,
        CropMaintenanceTileTarget[] targets, CropMaintenanceCapacityEvaluation evaluation, NativeRefillFlow flow)
    {
        var water = targets.Where(target => target.Water).ToArray();
        Require(water.Length > 0 && !evaluation.WaterBudgetFits, "Native refill supplement has no original water shortage.");
        Require(CurrentTeacherFrontierSupport.HashFile(flow.AfterPath) == flow.AfterSha256,
            "Native refill final capture bytes changed.");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(flow.AfterPath, "native refill final capture");
        Require(after.StateHash == flow.AfterStateHash && after.StateHash == SnapshotHash.ComputeStateHash(after.State),
            "Native refill final capture identity changed.");
        foreach (var tile in water)
        {
            var owner = layouts.Single(layout => layout.Proof.Tiles.Any(row => row.TileX == tile.TileX && row.TileY == tile.TileY));
            RequireSatisfiedCrop(after, owner, tile.TileX, tile.TileY);
        }
        foreach (var target in targets)
        {
            var location = layouts.Single(layout => layout.Proof.Tiles.Any(tile =>
                tile.TileX == target.TileX && tile.TileY == target.TileY)).Proof.LocationId;
            Require((!target.Plant || flow.CropWork.Contains(("executor.plant_seed", location, target.TileX, target.TileY))) &&
                    (!target.Water || flow.CropWork.Contains(("executor.water_crop", location, target.TileX, target.TileY))) &&
                    (!target.Harvest || flow.CropWork.Contains(("executor.harvest_crop", location, target.TileX, target.TileY))),
                "Native refill measured prefix does not cover the complete original crop-work denominator.");
        }
        // The verified complete prefix replaces estimates over the SAME work, rather than being added to them.
        var totalTicks = flow.Receipt.ActualTicks;
        var actionTicks = checked(totalTicks - flow.MovementTicks);
        var minutes = GameClockBudgetPolicy.TicksToGameMinutes(totalTicks);
        var timeFits = minutes <= evaluation.AvailableGameMinutes;
        var reasons = evaluation.BlockingReasons.Where(reason =>
            reason != "crop_capacity_watering_can_requires_unmodeled_refill_route" && reason != "crop_capacity_time_budget_exceeded").ToList();
        if (!timeFits && !reasons.Contains("crop_capacity_time_budget_exceeded")) reasons.Add("crop_capacity_time_budget_exceeded");
        return evaluation with { MovementTicks = flow.MovementTicks, ActionTicks = actionTicks, TotalTicks = totalTicks, RequiredGameMinutes = minutes,
            WateringCanRefillRouteVerified = true, WateringCanRefillTicks = flow.RefillTicks,
            WaterBudgetFits = true, TimeBudgetFits = timeFits, BlockingReasons = reasons.OrderBy(reason => reason, StringComparer.Ordinal).ToArray() };
    }
}
