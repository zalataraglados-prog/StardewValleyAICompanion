using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyCropDailyCapacityProof(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var commitment = plan.Commitments.Single();
        var layoutSnapshotPath = Path.Combine(
            root,
            "daily-capacity-layout-snapshot.json");
        var layoutRequestPath = Path.Combine(
            root,
            "daily-capacity-layout-request.json");
        var layoutProofPath = Path.Combine(
            root,
            "daily-capacity-layout-proof.json");
        var tiles = Enumerable.Range(0, 100)
            .Select(index => new MoneyRevenueCropPlantingTile(
                5 + index % 10,
                6 + index / 10))
            .ToArray();
        Write(layoutSnapshotPath, MoneyCropPlantingDaySnapshot());
        Write(layoutRequestPath,
            new MoneyRevenueCropPlantingLayoutProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingSnapshotPath = layoutSnapshotPath,
                LocationId = "Farm",
                Tiles = tiles
            });
        var layoutProof = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            layoutRequestPath);
        Write(layoutProofPath, layoutProof);

        var requiredDays = new[] { commitment.PlantingTotalDay }
            .Concat(commitment.RequiredWateringTotalDays)
            .Concat(commitment.HarvestWaves.Select(value =>
                value.HarvestTotalDay))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var snapshotPaths = requiredDays.Select(totalDay =>
        {
            var path = Path.Combine(
                root,
                "daily-capacity-day-" + totalDay + ".json");
            Write(path, MoneyCropDailyCapacitySnapshot(totalDay, 100));
            return path;
        }).ToArray();
        var requestPath = Path.Combine(root, "daily-capacity-request.json");
        Write(requestPath, new MoneyRevenueCropDailyCapacityProofRequest
        {
            CropProductionPlanPath = planPath,
            CommitmentId = commitment.CommitmentId,
            PlantingLayoutProofPaths = new[] { layoutProofPath },
            DailySnapshotPaths = snapshotPaths
        });

        var proof = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            requestPath);
        Require(proof.Status ==
                    "verified_aggregate_crop_daily_capacity" &&
                proof.EvidenceRequestId ==
                    "daily-capacity:" + commitment.CommitmentId &&
                proof.RequiredDayCount == 25 &&
                proof.Days.Length == 25 &&
                proof.DailyCapacityVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop daily-capacity proof lost its exact denominator.");
        var plantingDay = proof.Days.Single(value =>
            value.TotalDay == commitment.PlantingTotalDay);
        var combinedDay = proof.Days.Single(value =>
            value.TotalDay == 120);
        var finalDay = proof.Days.Single(value =>
            value.TotalDay == 136);
        Require(plantingDay.PlantActionCount == 100 &&
                plantingDay.WaterActionCount == 100 &&
                plantingDay.HarvestActionCount == 0 &&
                plantingDay.RequiredEnergy == 100d &&
                plantingDay.TimeBudgetFits &&
                plantingDay.EnergyBudgetFits &&
                plantingDay.WaterBudgetFits &&
                plantingDay.AllTargetsReachable &&
                combinedDay.PlantActionCount == 0 &&
                combinedDay.WaterActionCount == 100 &&
                combinedDay.HarvestActionCount == 100 &&
                finalDay.WaterActionCount == 0 &&
                finalDay.HarvestActionCount == 100,
            "Money crop daily-capacity proof lost its aggregate daily operations.");

        var rainSatisfiedDay = commitment.RequiredWateringTotalDays
            .First(value => value != commitment.PlantingTotalDay);
        var rainSatisfiedPath = Path.Combine(
            root,
            "daily-capacity-rain-satisfied-day.json");
        var rainSatisfied = MoneyCropDailyCapacitySnapshot(
            rainSatisfiedDay,
            0);
        var rainFarm = JsonNode.Parse(
            rainSatisfied.State["farm"].GetRawText())!.AsObject();
        rainFarm["crops"] = JsonSerializer.SerializeToNode(
            MoneyCropRuntimeField(tiles.Select(tile => new
            {
                tile_x = tile.TileX,
                tile_y = tile.TileY,
                needs_watering = false
            }).ToArray()),
            JsonDefaults.Options);
        rainSatisfied.State["farm"] = JsonSerializer.SerializeToElement(
            rainFarm,
            JsonDefaults.Options);
        rainSatisfied.StateHash = SnapshotHash.ComputeStateHash(
            rainSatisfied.State);
        Write(rainSatisfiedPath, rainSatisfied);
        var rainRequestPath = Path.Combine(
            root,
            "daily-capacity-rain-satisfied-request.json");
        Write(rainRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = snapshotPaths.Select(path =>
                    path.EndsWith(
                        "daily-capacity-day-" + rainSatisfiedDay + ".json",
                        StringComparison.Ordinal)
                        ? rainSatisfiedPath
                        : path).ToArray()
            });
        var rainProof = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            rainRequestPath);
        var rainDay = rainProof.Days.Single(value =>
            value.TotalDay == rainSatisfiedDay);
        Require(rainDay.WaterActionCount == 0 &&
                rainDay.WaterBudgetFits &&
                !rainDay.WateringCanRefillRouteVerified,
            "A rain-satisfied crop day still invented a native watering action.");

        var proofPath = Path.Combine(root, "daily-capacity-proof.json");
        var fragmentIndexPath = Path.Combine(
            root,
            "daily-capacity-fragment-index.json");
        var checkpointRequestPath = Path.Combine(
            root,
            "daily-capacity-checkpoint-request.json");
        Write(proofPath, proof);
        Write(fragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailyCapacityProofPaths = new[] { proofPath }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.Build(
            checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 2 &&
                checkpoint.VerifiedDailyCapacities.Length == 1 &&
                checkpoint.VerifiedRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                !checkpoint.FutureCropProductionProofReady &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money crop daily-capacity proof did not enter the checkpoint denominator.");

        var missingDayRequestPath = Path.Combine(
            root,
            "daily-capacity-missing-day-request.json");
        Write(missingDayRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = snapshotPaths.Skip(1).ToArray()
            });
        RequireRejects(
            () => MoneyRevenueCropDailyCapacityProofBuilder.Build(
                missingDayRequestPath),
            "An incomplete Money crop daily-capacity day set was admitted.");

        var calendarDriftPath = Path.Combine(
            root,
            "daily-capacity-calendar-drift-day.json");
        var calendarDrift = MoneyCropDailyCapacitySnapshot(
            commitment.PlantingTotalDay,
            100);
        var driftedTime = JsonNode.Parse(
            calendarDrift.State["time"].GetRawText())!.AsObject();
        driftedTime["day"]!["value"] = 2;
        calendarDrift.State["time"] = JsonSerializer.SerializeToElement(
            driftedTime,
            JsonDefaults.Options);
        calendarDrift.StateHash = SnapshotHash.ComputeStateHash(
            calendarDrift.State);
        Write(calendarDriftPath, calendarDrift);
        var calendarDriftRequestPath = Path.Combine(
            root,
            "daily-capacity-calendar-drift-request.json");
        Write(calendarDriftRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = snapshotPaths
                    .Select((path, index) => index == 0
                        ? calendarDriftPath
                        : path)
                    .ToArray()
            });
        RequireRejects(
            () => MoneyRevenueCropDailyCapacityProofBuilder.Build(
                calendarDriftRequestPath),
            "A Money crop snapshot with a drifted calendar was admitted.");

        var insufficientWaterPath = Path.Combine(
            root,
            "daily-capacity-insufficient-water-day.json");
        var insufficientWater = MoneyCropDailyCapacitySnapshot(
            commitment.PlantingTotalDay,
            99);
        Write(insufficientWaterPath, insufficientWater);
        var insufficientWaterRequestPath = Path.Combine(
            root,
            "daily-capacity-insufficient-water-request.json");
        Write(insufficientWaterRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = snapshotPaths
                    .Select((path, index) => index == 0
                        ? insufficientWaterPath
                        : path)
                    .ToArray()
            });
        RequireRejects(
            () => MoneyRevenueCropDailyCapacityProofBuilder.Build(
                insufficientWaterRequestPath),
            "A Money crop day requiring an unmodeled watering-can refill was admitted.");

        var refillReceiptPath = Path.Combine(
            root,
            "daily-capacity-refill-receipt.json");
        Write(refillReceiptPath, new
        {
            schema_version = "queue_execution_receipt.v1",
            status = "applied",
            success = true,
            after_snapshot_fresh = true,
            source_state_hash = insufficientWater.StateHash,
            step_results = new[]
            {
                new
                {
                    option_id = "executor.water_crop",
                    status = "applied",
                    primitive_kind = "water_crop",
                    primitive_verification_status = "verified",
                    target_location = "Farm",
                    water_before = 0,
                    water_after = 99,
                    actual_ticks = 733
                }
            }
        });
        var refillRequestPath = Path.Combine(
            root,
            "daily-capacity-refill-request.json");
        Write(refillRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPaths = new[] { layoutProofPath },
                DailySnapshotPaths = snapshotPaths
                    .Select((path, index) => index == 0
                        ? insufficientWaterPath
                        : path)
                    .ToArray(),
                WateringCanRefillReceipts = new[]
                {
                    new MoneyRevenueCropDailyCapacityRefillReceiptRequest(
                        commitment.PlantingTotalDay,
                        refillReceiptPath)
                }
            });
        var refillProof = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            refillRequestPath);
        var refillDay = refillProof.Days.Single(value =>
            value.TotalDay == commitment.PlantingTotalDay);
        Require(refillDay.WateringCanRefillRouteVerified &&
                refillDay.WateringCanRefillActualTicks == 733 &&
                refillDay.WaterBudgetFits &&
                refillProof.VerifiedWateringCanRefillReceipts.Length == 1 &&
                refillProof.VerifiedWateringCanRefillReceipts[0]
                    .RefillActualTicks == 733,
            "An exact native watering-can refill receipt did not enter the day budget.");
    }

    private static SnapshotEnvelope MoneyCropDailyCapacitySnapshot(
        int totalDay,
        int waterLeft)
    {
        var snapshot = MoneyCropProductionSnapshot();
        var state = snapshot.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        state["time"] = JsonSerializer.SerializeToElement(new
        {
            total_days = MoneyCropRuntimeField(totalDay),
            year = MoneyCropRuntimeField(2),
            season = MoneyCropRuntimeField("spring"),
            day = MoneyCropRuntimeField(totalDay - 111),
            time = MoneyCropRuntimeField(600)
        }, JsonDefaults.Options);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = MoneyCropRuntimeField("Farm"),
            tile_x = MoneyCropRuntimeField(4),
            tile_y = MoneyCropRuntimeField(6),
            energy = MoneyCropRuntimeField(270d),
            max_energy = MoneyCropRuntimeField(270d),
            watering_can_state = MoneyCropRuntimeField(new
            {
                projection_status = "exact_vanilla_power_zero",
                slot_index = 2,
                runtime_type = "StardewValley.Tools.WateringCan",
                upgrade_level = 4,
                water_left = waterLeft,
                capacity = 100,
                is_bottomless = false,
                farmer_has_watering_can_enchantment = false,
                is_efficient = false,
                farming_level = 10,
                energy_cost_per_power_zero_action = 1d
            })
        }, JsonDefaults.Options);
        state["locations"] = JsonSerializer.SerializeToElement(new
        {
            collision_grid = MoneyCropRuntimeField(new
            {
                location_id = "Farm",
                width = 100,
                height = 100,
                notable_tiles = Array.Empty<object>()
            }),
            route_action_branch_coverage = MoneyCropRuntimeField(new
            {
                rows = Array.Empty<object>()
            })
        }, JsonDefaults.Options);
        var farm = JsonNode.Parse(state["farm"].GetRawText())!.AsObject();
        farm["shipping_bins"] = JsonSerializer.SerializeToNode(
            MoneyCropRuntimeField(new[]
            {
                new
                {
                    tile_x = 71,
                    tile_y = 14,
                    tile_width = 2,
                    tile_height = 1,
                    interaction_stand_tile_x = 70,
                    interaction_stand_tile_y = 14,
                    days_of_construction_left = 0,
                    completed = true,
                    bin_scope = "personal",
                    player_id = "123",
                    contents = Array.Empty<object>(),
                    pending_settlement_value = 0,
                    pending_settlement_value_complete = true,
                    pending_settlement_timing =
                        "next_native_day_transition",
                    contents_signature = "empty-shipping-scope",
                    contents_truncated = false
                }
            }),
            JsonDefaults.Options);
        state["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        snapshot.GameTick = 300 + totalDay;
        snapshot.State = state;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static void RequireRejects(Action action, string message)
    {
        var rejected = false;
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }
}
