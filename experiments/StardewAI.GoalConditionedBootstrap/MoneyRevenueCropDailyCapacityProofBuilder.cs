using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropDailyCapacityProofBuilder
{
    public static MoneyRevenueCropDailyCapacityProof Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop daily-capacity proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropDailyCapacityProofRequest>(
                fullRequestPath,
                "Money Revenue crop daily-capacity proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_daily_capacity_proof_request.v1" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                request.PlantingLayoutProofPaths is not null &&
                request.DailySnapshotPaths is not null &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop daily-capacity proof request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                plan.Commitments.Length > 0 &&
                plan.Commitments.Any(value => value.CommitmentId ==
                    request.CommitmentId) &&
                plan.PendingEvidenceRequestIds.Contains(
                    "daily-capacity:" + request.CommitmentId,
                    StringComparer.Ordinal) &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");

        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            RequiredFile(plan.SnapshotPath, "Money Revenue crop source snapshot"),
            "Money Revenue crop source snapshot");
        var sourceCalendar = ReadCalendarIdentity(sourceSnapshot);
        var cropBySeed = ReadCropCatalog(sourceSnapshot);
        var requestedLayoutPaths = request.PlantingLayoutProofPaths ??
            throw new InvalidDataException(
                "Money Revenue crop planting-layout proof paths are missing.");
        var layoutPaths = requestedLayoutPaths
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop planting-layout proof"))
            .ToArray();
        Require(layoutPaths.Length == plan.Commitments.Length &&
                layoutPaths.Distinct(PathComparer).Count() ==
                    layoutPaths.Length,
            "Money Revenue crop daily-capacity layout proof set is incomplete or duplicated.");

        var layouts = layoutPaths.Select(path =>
        {
            var proof = CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropPlantingLayoutProof>(
                    path,
                    "Money Revenue crop planting-layout proof");
            var rebuilt = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
                proof.RequestPath);
            Require(proof.SchemaVersion ==
                        "money_revenue_crop_planting_layout_proof.v1" &&
                    BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                    string.Equals(
                        Path.GetFullPath(proof.CropProductionPlanPath),
                        planPath,
                        PathComparison) &&
                    proof.CropProductionPlanSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(planPath) &&
                    proof.PlantingLayoutVerified &&
                    !proof.FormalProductTrainingAuthorized,
                "Money Revenue crop planting-layout proof is not exact.");
            var commitment = plan.Commitments.Single(value =>
                value.CommitmentId == proof.CommitmentId);
            Require(proof.TileCount == commitment.TileCount &&
                    proof.SeedQualifiedItemId ==
                        commitment.SeedQualifiedItemId,
                "Money Revenue crop planting-layout proof drifted from its commitment.");
            Require(cropBySeed.TryGetValue(
                    commitment.SeedQualifiedItemId,
                    out var crop),
                "Money Revenue crop catalog is missing the commitment seed " +
                commitment.SeedQualifiedItemId + ".");
            return new LayoutState(
                path,
                CurrentTeacherFrontierSupport.HashFile(path),
                proof,
                commitment,
                ReadRequiredBool(crop, "is_raised"));
        }).OrderBy(value => value.Commitment.CommitmentId, StringComparer.Ordinal)
            .ToArray();
        Require(layouts.Select(value => value.Commitment.CommitmentId)
                    .SequenceEqual(
                        plan.Commitments.Select(value => value.CommitmentId)
                            .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal),
            "Money Revenue crop daily-capacity layout proof denominator drifted.");
        Require(layouts.Select(value => value.Proof.LocationId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1,
            "Money Revenue crop daily-capacity v1 requires one shared crop location.");
        Require(layouts.SelectMany(value => value.Proof.Tiles)
                    .Select(value => (value.TileX, value.TileY))
                    .Distinct().Count() ==
                layouts.Sum(value => value.Proof.Tiles.Length),
            "Money Revenue crop daily-capacity layouts overlap.");

        var requiredDays = plan.Commitments
            .SelectMany(RequiredDays)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var requestedSnapshotPaths = request.DailySnapshotPaths ??
            throw new InvalidDataException(
                "Money Revenue crop daily-capacity snapshot paths are missing.");
        var snapshotPaths = requestedSnapshotPaths
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop daily-capacity snapshot"))
            .ToArray();
        Require(snapshotPaths.Length == requiredDays.Length &&
                snapshotPaths.Distinct(PathComparer).Count() ==
                    snapshotPaths.Length,
            "Money Revenue crop daily-capacity snapshot set is incomplete or duplicated.");
        var snapshots = snapshotPaths.Select(path =>
        {
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                path,
                "Money Revenue crop daily-capacity snapshot");
            var calendar = ReadCalendarIdentity(snapshot);
            Require(snapshot.StateHash ==
                        SnapshotHash.ComputeStateHash(snapshot.State) &&
                    snapshot.GameVersion == sourceSnapshot.GameVersion &&
                    snapshot.SaveId.Status == FieldStatus.Available &&
                    snapshot.PlayerId.Status == FieldStatus.Available &&
                    snapshot.SaveId.Value == sourceSnapshot.SaveId.Value &&
                    snapshot.PlayerId.Value == sourceSnapshot.PlayerId.Value &&
                    calendar.WorldDateOffset == sourceCalendar.WorldDateOffset,
                "Money Revenue crop daily-capacity snapshot identity drifted.");
            return new SnapshotState(path, snapshot, calendar.TotalDay);
        }).OrderBy(value => value.TotalDay).ToArray();
        Require(snapshots.Select(value => value.TotalDay)
                    .SequenceEqual(requiredDays),
            "Money Revenue crop daily-capacity snapshot dates drifted from the obligation plan.");

        var locationId = layouts[0].Proof.LocationId;
        var days = snapshots.Select(day =>
        {
            var targets = layouts.SelectMany(layout =>
            {
                var plant = day.TotalDay ==
                    layout.Commitment.PlantingTotalDay;
                var water = layout.Commitment.RequiredWateringTotalDays
                    .Contains(day.TotalDay);
                var harvest = layout.Commitment.HarvestWaves.Any(value =>
                    value.HarvestTotalDay == day.TotalDay);
                return plant || water || harvest
                    ? layout.Proof.Tiles.Select(tile =>
                        new CropMaintenanceTileTarget(
                            tile.TileX,
                            tile.TileY,
                            plant,
                            water,
                            harvest,
                            layout.RaisedCrop))
                    : Array.Empty<CropMaintenanceTileTarget>();
            }).ToArray();
            var evaluation = CropMaintenanceCapacityEvaluator.Evaluate(
                day.Snapshot,
                locationId,
                targets);
            Require(evaluation.BlockingReasons.Length == 0 &&
                    evaluation.AllTargetsReachable &&
                    evaluation.TimeBudgetFits &&
                    evaluation.EnergyBudgetFits &&
                    evaluation.WaterBudgetFits,
                "Money Revenue crop daily capacity is blocked on day " +
                day.TotalDay + ": " +
                string.Join(",", evaluation.BlockingReasons));
            return new MoneyRevenueCropDailyCapacityDay(
                day.TotalDay,
                day.Path,
                CurrentTeacherFrontierSupport.HashFile(day.Path),
                day.Snapshot.StateHash,
                evaluation.SnapshotStartTime,
                evaluation.AvailableGameMinutes,
                evaluation.PlantActionCount,
                evaluation.WaterActionCount,
                evaluation.HarvestActionCount,
                evaluation.MovementTileCount,
                evaluation.MovementTicks,
                evaluation.ActionTicks,
                evaluation.TotalTicks,
                evaluation.RequiredGameMinutes,
                evaluation.AvailableEnergy,
                evaluation.RequiredEnergy,
                evaluation.MinimumEnergyReserve,
                evaluation.WateringCanSlotIndex,
                evaluation.WateringCanWaterLeft,
                evaluation.WateringCanCapacity,
                evaluation.WateringCanBottomless,
                evaluation.TimeBudgetFits,
                evaluation.EnergyBudgetFits,
                evaluation.WaterBudgetFits,
                evaluation.AllTargetsReachable);
        }).ToArray();

        return new MoneyRevenueCropDailyCapacityProof
        {
            Status = "verified_aggregate_crop_daily_capacity",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 =
                CurrentTeacherFrontierSupport.HashFile(planPath),
            EvidenceRequestId = "daily-capacity:" + request.CommitmentId,
            CommitmentId = request.CommitmentId,
            SourceStateHash = plan.SourceStateHash,
            LocationId = locationId,
            CoveredCommitmentIds = layouts.Select(value =>
                value.Commitment.CommitmentId).ToArray(),
            PlantingLayoutProofSha256ByCommitment = layouts.Select(value =>
                new MoneyRevenueCropDailyCapacityLayoutBinding(
                    value.Commitment.CommitmentId,
                    value.Path,
                    value.Sha256,
                    value.RaisedCrop)).ToArray(),
            RequiredDayCount = requiredDays.Length,
            Days = days,
            DailyCapacityVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static IEnumerable<int> RequiredDays(
        MoneyRevenueCropProductionCommitmentPlan commitment) =>
        new[] { commitment.PlantingTotalDay }
            .Concat(commitment.RequiredWateringTotalDays)
            .Concat(commitment.HarvestWaves.Select(value =>
                value.HarvestTotalDay));

    private static Dictionary<string, JsonElement> ReadCropCatalog(
        SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("farm", out var farm) ||
            farm.ValueKind != JsonValueKind.Object ||
            !farm.TryGetProperty("crop_catalog", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !ReadableStatus(field) ||
            !field.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Money Revenue crop catalog is missing.");
        }
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var crop in value.EnumerateArray())
        {
            if (crop.ValueKind != JsonValueKind.Object ||
                !crop.TryGetProperty("seed_id", out var seed) ||
                seed.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    "Money Revenue crop catalog row is invalid.");
            }
            Require(result.TryAdd("(O)" + seed.GetString(), crop),
                "Money Revenue crop catalog contains duplicate seeds.");
        }
        return result;
    }

    private static bool ReadRequiredBool(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var property) &&
                property.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Money Revenue crop catalog is missing exact " + name + ".");
        return property.GetBoolean();
    }

    private static CalendarIdentity ReadCalendarIdentity(
        SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("time", out var time) ||
            time.ValueKind != JsonValueKind.Object ||
            !TryReadAvailableInt(time, "total_days", out var totalDay) ||
            !TryReadAvailableInt(time, "year", out var year) ||
            !TryReadAvailableString(time, "season", out var season) ||
            !TryReadAvailableInt(time, "day", out var dayOfMonth) ||
            totalDay < 0 || year < 1 || dayOfMonth is < 1 or > 28)
        {
            throw new InvalidDataException(
                "Money Revenue crop daily-capacity calendar is missing or invalid.");
        }
        var seasonIndex = season switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => -1
        };
        Require(seasonIndex >= 0,
            "Money Revenue crop daily-capacity season is invalid.");
        var ordinal = checked(
            (year - 1) * 112 + seasonIndex * 28 + dayOfMonth - 1);
        return new CalendarIdentity(totalDay, checked(totalDay - ordinal));
    }

    private static bool TryReadAvailableInt(
        JsonElement parent,
        string name,
        out int value)
    {
        value = 0;
        return parent.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.Object &&
            ReadableStatus(field) &&
            field.TryGetProperty("value", out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadAvailableString(
        JsonElement parent,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !ReadableStatus(field) ||
            !field.TryGetProperty("value", out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool ReadableStatus(JsonElement field) =>
        field.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String &&
        (string.Equals(
                status.GetString(),
                "available",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                status.GetString(),
                "derived",
                StringComparison.OrdinalIgnoreCase));

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record LayoutState(
        string Path,
        string Sha256,
        MoneyRevenueCropPlantingLayoutProof Proof,
        MoneyRevenueCropProductionCommitmentPlan Commitment,
        bool RaisedCrop);

    private sealed record SnapshotState(
        string Path,
        SnapshotEnvelope Snapshot,
        int TotalDay);

    private sealed record CalendarIdentity(
        int TotalDay,
        int WorldDateOffset);
}
