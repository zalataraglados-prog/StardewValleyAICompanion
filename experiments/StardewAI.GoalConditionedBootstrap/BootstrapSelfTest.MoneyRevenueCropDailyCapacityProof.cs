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
        Write(insufficientWaterPath,
            MoneyCropDailyCapacitySnapshot(
                commitment.PlantingTotalDay,
                99));
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
