namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyCropNativeProductionRunPlan(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var requestPath = Path.Combine(root, "native-run-plan-request.json");
        var intents = new[]
        {
            new MoneyRevenueCropNativeProductionLayoutIntent(
                plan.Commitments[0].CommitmentId,
                "Farm",
                new[]
                {
                    new MoneyRevenueCropPlantingTile(5, 6),
                    new MoneyRevenueCropPlantingTile(6, 6)
                },
                new[]
                {
                    new MoneyRevenueCropPlantingTile(5, 7)
                }),
            new MoneyRevenueCropNativeProductionLayoutIntent(
                plan.Commitments[1].CommitmentId,
                "Farm",
                new[]
                {
                    new MoneyRevenueCropPlantingTile(7, 6),
                    new MoneyRevenueCropPlantingTile(8, 6),
                    new MoneyRevenueCropPlantingTile(9, 6)
                })
        };
        Write(requestPath,
            new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = planPath,
                LayoutIntents = intents,
                SeedAcquisitionIntents = plan.Commitments
                    .Where(commitment =>
                        commitment.SeedQuantityShortfall > 0)
                    .Select(commitment =>
                        new MoneyRevenueCropNativeSeedAcquisitionIntent(
                            commitment.CommitmentId,
                            commitment.SeedQualifiedItemId,
                            "FixtureSeedShop",
                            100))
                    .ToArray(),
                MaxQueueItemCount = 2
            });
        var runPlan = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
            requestPath);
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<
            StardewAI.Contracts.State.SnapshotEnvelope>(
                plan.SnapshotPath,
                "Money crop native-production source snapshot");
        var expectedFirstDay = sourceSnapshot.State["time"]
            .GetProperty("total_days")
            .GetProperty("value")
            .GetInt32();
        var expectedLastDay = plan.Commitments.Max(value =>
            value.HarvestWaves.Max(wave => wave.HarvestTotalDay));
        var expectedActionCount = plan.Commitments.Sum(commitment =>
            commitment.TileCount *
            (1 + commitment.RequiredWateringTotalDays.Length +
                commitment.HarvestWaves.Length));
        var expectedSeedAcquisitions = plan.Commitments
            .Where(commitment => commitment.SeedQuantityShortfall > 0)
            .ToArray();
        Require(runPlan.Status == "native_crop_production_run_ready" &&
                runPlan.SchemaVersion ==
                    "money_revenue_crop_native_production_run_plan.v3" &&
                runPlan.FirstTotalDay == expectedFirstDay &&
                runPlan.SourceTotalDay == expectedFirstDay &&
                runPlan.SourceStateHash == sourceSnapshot.StateHash &&
                runPlan.LastTotalDay == expectedLastDay &&
                runPlan.DayCount == expectedLastDay - expectedFirstDay + 1 &&
                runPlan.ActionCount == expectedActionCount &&
                runPlan.Days.SelectMany(value => value.ActionBatches)
                    .SelectMany(value => value.Tiles)
                    .All(value => value !=
                        new MoneyRevenueCropPlantingTile(5, 7)) &&
                runPlan.SeedAcquisitionCount ==
                    expectedSeedAcquisitions.Length &&
                runPlan.SeedAcquisitions.Select(value =>
                    (value.CommitmentId, value.SeedQualifiedItemId,
                        value.ShopId, value.MaxUnitPrice,
                        value.RequiredQuantity, value.DeadlineTotalDay))
                    .SequenceEqual(expectedSeedAcquisitions.Select(value =>
                        (value.CommitmentId, value.SeedQualifiedItemId,
                            "FixtureSeedShop", 100,
                            value.SeedQuantityShortfall,
                            value.SeedSupplyDeadlineTotalDay))) &&
                runPlan.SeedAcquisitions.All(value =>
                    value.ExecutionPolicy ==
                        "economy.buy_supplies_one_unit_fresh_snapshot_slices") &&
                runPlan.Days.Count(day => day.NativeSleepRequired) ==
                    runPlan.DayCount - 1 &&
                runPlan.Days.SelectMany(day => day.ActionBatches)
                    .All(batch => batch.Tiles.Length is >= 1 and <= 2) &&
                runPlan.Days.SelectMany(day => day.ActionBatches)
                    .Where(batch => batch.OptionId == "executor.water_crop")
                    .All(batch => batch.ExecutionCondition ==
                        "crop_needs_watering") &&
                runPlan.Days.SelectMany(day => day.ActionBatches)
                    .Where(batch => batch.OptionId != "executor.water_crop")
                    .All(batch => batch.ExecutionCondition == "always") &&
                runPlan.CoveredCommitmentIds.SequenceEqual(
                    plan.Commitments.Select(value => value.CommitmentId),
                    StringComparer.Ordinal) &&
                runPlan.NativeProductionRunReady &&
                !runPlan.FormalProductTrainingAuthorized,
            "Money crop native-production run plan lost its exact day/action denominator.");
        foreach (var day in runPlan.Days)
        {
            var ranks = day.ActionBatches.Select(batch =>
                batch.OptionId switch
                {
                    "executor.plant_seed" => 0,
                    "executor.water_crop" => 1,
                    "executor.harvest_crop" => 2,
                    _ => -1
                }).ToArray();
            Require(ranks.All(rank => rank >= 0) &&
                    ranks.SequenceEqual(ranks.OrderBy(rank => rank)) &&
                    day.CaptureDailyCapacitySnapshot ==
                        day.ActionBatches.Any(),
                "Money crop native-production run day lost compiler order or capture boundary.");
        }
        Require(runPlan.Days
                    .Where(day => day.TotalDay < plan.Commitments.Min(value =>
                        value.PlantingTotalDay))
                    .All(day => day.ActionBatches.Length == 0 &&
                        day.CaptureLayoutCommitmentIds.Length == 0 &&
                        !day.CaptureDailyCapacitySnapshot &&
                        day.NativeSleepRequired),
            "Money crop native-production pre-plant days are not sleep-only boundaries.");

        var overlappingRequestPath = Path.Combine(
            root,
            "native-run-plan-overlap-request.json");
        var overlappingIntents = intents.Select((intent, index) =>
            index == 1
                ? intent with
                {
                    Tiles = intent.Tiles.Select((tile, tileIndex) =>
                        tileIndex == 0
                            ? new MoneyRevenueCropPlantingTile(5, 6)
                            : tile).ToArray()
                }
                : intent).ToArray();
        Write(overlappingRequestPath,
            new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = planPath,
                LayoutIntents = overlappingIntents,
                MaxQueueItemCount = 2
            });
        RequireRejects(
            () => MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
                overlappingRequestPath),
            "Overlapping Money crop native-production layout intents were admitted.");

        var unboundedRequestPath = Path.Combine(
            root,
            "native-run-plan-unbounded-request.json");
        var unboundedIntents = intents.Select((intent, index) =>
            index == 0
                ? intent with
                {
                    Tiles = new[]
                    {
                        new MoneyRevenueCropPlantingTile(5, 6),
                        new MoneyRevenueCropPlantingTile(37, 6)
                    }
                }
                : intent).ToArray();
        Write(unboundedRequestPath,
            new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = planPath,
                LayoutIntents = unboundedIntents,
                MaxQueueItemCount = 2
            });
        RequireRejects(
            () => MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
                unboundedRequestPath),
            "A layout outside one bounded farm-layout projection was admitted.");
    }
}
