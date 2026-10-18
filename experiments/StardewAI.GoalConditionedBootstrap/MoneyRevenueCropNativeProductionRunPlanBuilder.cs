using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropNativeProductionRunPlanBuilder
{
    public static MoneyRevenueCropNativeProductionRunPlan Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop native-production run-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeProductionRunPlanRequest>(
                fullRequestPath,
                "Money Revenue crop native-production run-plan request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_native_production_run_plan_request.v1" &&
                request.LayoutIntents is not null &&
                request.MaxQueueItemCount is >= 1 and <= 8 &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop native-production run-plan request is invalid.");

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
                !plan.FutureCropProductionProofReady &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            RequiredFile(
                plan.SnapshotPath,
                "Money Revenue crop-production source snapshot"),
            "Money Revenue crop-production source snapshot");
        var sourceTotalDay = ReadAvailableStateInt(
            sourceSnapshot,
            "time",
            "total_days");
        Require(sourceSnapshot.StateHash == plan.SourceStateHash &&
                sourceTotalDay >= 0,
            "Money Revenue crop-production source boundary is invalid.");

        var requestedIntents = request.LayoutIntents ??
            throw new InvalidDataException(
                "Money Revenue crop native-production layout intents are missing.");
        Require(requestedIntents.Length == plan.Commitments.Length &&
                requestedIntents.Select(value => value.CommitmentId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    requestedIntents.Length,
            "Money Revenue crop native-production layout denominator is incomplete or duplicated.");
        var intents = plan.Commitments.Select(commitment =>
        {
            var matches = requestedIntents.Where(value =>
                    value.CommitmentId == commitment.CommitmentId)
                .ToArray();
            Require(matches.Length == 1 &&
                    string.Equals(
                        matches[0].LocationId,
                        "Farm",
                        StringComparison.Ordinal) &&
                    matches[0].Tiles is not null &&
                    matches[0].Tiles.Length == commitment.TileCount &&
                    matches[0].Tiles.All(tile =>
                        tile.TileX >= 0 && tile.TileY >= 0) &&
                    matches[0].Tiles.Distinct().Count() ==
                        matches[0].Tiles.Length,
                "Money Revenue crop native-production layout intent is invalid for " +
                commitment.CommitmentId + ".");
            return new LayoutState(
                commitment,
                matches[0].LocationId,
                matches[0].Tiles.OrderBy(tile => tile.TileY)
                    .ThenBy(tile => tile.TileX).ToArray());
        }).ToArray();
        Require(intents.SelectMany(value => value.Tiles)
                    .Distinct().Count() ==
                intents.Sum(value => value.Tiles.Length),
            "Money Revenue crop native-production layout intents overlap.");

        var firstPlantingDay = plan.Commitments.Min(value =>
            value.PlantingTotalDay);
        Require(sourceTotalDay <= firstPlantingDay,
            "Money Revenue crop native-production source day follows its first planting day.");
        var firstDay = sourceTotalDay;
        var lastDay = plan.Commitments.Max(value =>
            value.HarvestWaves.Max(wave => wave.HarvestTotalDay));
        Require(firstDay >= 0 && lastDay >= firstDay &&
                lastDay < plan.DeadlineTotalDayExclusive,
            "Money Revenue crop native-production day denominator is invalid.");

        var days = Enumerable.Range(firstDay, checked(lastDay - firstDay + 1))
            .Select(totalDay => BuildDay(
                totalDay,
                lastDay,
                intents,
                request.MaxQueueItemCount))
            .ToArray();
        return new MoneyRevenueCropNativeProductionRunPlan
        {
            Status = "native_crop_production_run_ready",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 =
                CurrentTeacherFrontierSupport.HashFile(planPath),
            SourceStateHash = plan.SourceStateHash,
            SourceTotalDay = sourceTotalDay,
            LocationId = intents[0].LocationId,
            CoveredCommitmentIds = plan.Commitments.Select(value =>
                value.CommitmentId).ToArray(),
            FirstTotalDay = firstDay,
            LastTotalDay = lastDay,
            DayCount = days.Length,
            ActionBatchCount = days.Sum(value => value.ActionBatches.Length),
            ActionCount = days.Sum(value => value.ActionBatches.Sum(batch =>
                batch.Tiles.Length)),
            MaxQueueItemCount = request.MaxQueueItemCount,
            Days = days,
            NativeProductionRunReady = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueCropNativeProductionRunDay BuildDay(
        int totalDay,
        int lastDay,
        LayoutState[] intents,
        int maxQueueItemCount)
    {
        var batches = new List<MoneyRevenueCropNativeProductionActionBatch>();
        foreach (var optionId in new[]
                 {
                     "executor.plant_seed",
                     "executor.water_crop",
                     "executor.harvest_crop"
                 })
        {
            foreach (var intent in intents.Where(value =>
                         ActionRequired(value.Commitment, optionId, totalDay)))
            {
                foreach (var tileBatch in intent.Tiles.Chunk(
                             maxQueueItemCount))
                {
                    batches.Add(new MoneyRevenueCropNativeProductionActionBatch(
                        batches.Count + 1,
                        intent.Commitment.CommitmentId,
                        optionId,
                        optionId == "executor.water_crop"
                            ? "crop_needs_watering"
                            : "always",
                        intent.LocationId,
                        optionId == "executor.plant_seed"
                            ? intent.Commitment.SeedQualifiedItemId
                            : string.Empty,
                        tileBatch));
                }
            }
        }
        return new MoneyRevenueCropNativeProductionRunDay(
            totalDay,
            intents.Where(value =>
                    value.Commitment.PlantingTotalDay == totalDay)
                .Select(value => value.Commitment.CommitmentId).ToArray(),
            batches.Count > 0,
            batches.ToArray(),
            totalDay < lastDay);
    }

    private static bool ActionRequired(
        MoneyRevenueCropProductionCommitmentPlan commitment,
        string optionId,
        int totalDay) => optionId switch
        {
            "executor.plant_seed" =>
                commitment.PlantingTotalDay == totalDay,
            "executor.water_crop" =>
                commitment.RequiredWateringTotalDays.Contains(totalDay),
            "executor.harvest_crop" => commitment.HarvestWaves.Any(value =>
                value.HarvestTotalDay == totalDay),
            _ => false
        };

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static int ReadAvailableStateInt(
        SnapshotEnvelope snapshot,
        string domain,
        string field)
    {
        var result = 0;
        Require(snapshot.State.TryGetValue(domain, out var domainValue) &&
                domainValue.ValueKind == System.Text.Json.JsonValueKind.Object &&
                domainValue.TryGetProperty(field, out var fieldValue) &&
                fieldValue.ValueKind == System.Text.Json.JsonValueKind.Object &&
                fieldValue.TryGetProperty("status", out var status) &&
                status.GetString() == "available" &&
                fieldValue.TryGetProperty("value", out var value) &&
                value.TryGetInt32(out result),
            "Money Revenue crop native-production source day is unavailable.");
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record LayoutState(
        MoneyRevenueCropProductionCommitmentPlan Commitment,
        string LocationId,
        MoneyRevenueCropPlantingTile[] Tiles);
}
