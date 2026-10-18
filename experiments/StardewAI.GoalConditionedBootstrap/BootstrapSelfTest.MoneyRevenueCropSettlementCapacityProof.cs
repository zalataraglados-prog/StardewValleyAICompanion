using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyCropSettlementCapacityAggregation()
    {
        var root = TempArtifactRoot(
            "money-crop-settlement-capacity-aggregation");
        var snapshot = MoneyCropProductionSnapshot();
        var service = new CropCommitmentLedgerService();
        var first = service.Upsert(
            null,
            snapshot,
            SettlementCapacityCommitment(
                snapshot,
                expectedRevision: 0,
                "settlement-capacity-a",
                tileCount: 2),
            "2026-10-18T01:00:00Z");
        Require(first.Accepted && first.Ledger is not null,
            "First aggregate settlement commitment was rejected.");
        var second = service.Upsert(
            first.Ledger,
            snapshot,
            SettlementCapacityCommitment(
                snapshot,
                first.Ledger!.Revision,
                "settlement-capacity-b",
                tileCount: 3),
            "2026-10-18T01:01:00Z");
        Require(second.Accepted && second.Ledger is not null,
            "Second aggregate settlement commitment was rejected.");

        var sourcePath = Path.Combine(root, "source.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var planRequestPath = Path.Combine(root, "plan-request.json");
        var planPath = Path.Combine(root, "plan.json");
        Write(sourcePath, snapshot);
        Write(ledgerPath, second.Ledger!);
        Write(planRequestPath,
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = sourcePath,
                StrategyLedgerPath = ledgerPath
            });
        var plan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            planRequestPath);
        Write(planPath, plan);
        Require(plan.Commitments.Length == 2,
            "Aggregate settlement fixture lost a commitment.");

        var plantingSnapshotPath = Path.Combine(
            root,
            "planting-snapshot.json");
        Write(plantingSnapshotPath, MoneyCropPlantingDaySnapshot());
        var layoutPaths = plan.Commitments.Select((commitment, index) =>
        {
            var requestPath = Path.Combine(
                root,
                "layout-request-" + index + ".json");
            var proofPath = Path.Combine(
                root,
                "layout-proof-" + index + ".json");
            var startX = index == 0 ? 5 : 7;
            Write(requestPath,
                new MoneyRevenueCropPlantingLayoutProofRequest
                {
                    CropProductionPlanPath = planPath,
                    CommitmentId = commitment.CommitmentId,
                    PlantingSnapshotPath = plantingSnapshotPath,
                    LocationId = "Farm",
                    Tiles = Enumerable.Range(0, commitment.TileCount)
                        .Select(offset =>
                            new MoneyRevenueCropPlantingTile(
                                startX + offset,
                                6))
                        .ToArray()
                });
            Write(proofPath,
                MoneyRevenueCropPlantingLayoutProofBuilder.Build(requestPath));
            return proofPath;
        }).ToArray();

        var requiredDays = plan.Commitments.SelectMany(commitment =>
                new[] { commitment.PlantingTotalDay }
                    .Concat(commitment.RequiredWateringTotalDays)
                    .Concat(commitment.HarvestWaves.Select(wave =>
                        wave.HarvestTotalDay)))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var dailySnapshotPaths = requiredDays.Select(totalDay =>
        {
            var path = Path.Combine(root, "day-" + totalDay + ".json");
            Write(path, MoneyCropDailyCapacitySnapshot(totalDay, 100));
            return path;
        }).ToArray();
        var dailyRequestPath = Path.Combine(root, "daily-request.json");
        var dailyProofPath = Path.Combine(root, "daily-proof.json");
        Write(dailyRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = plan.Commitments[0].CommitmentId,
                PlantingLayoutProofPaths = layoutPaths,
                DailySnapshotPaths = dailySnapshotPaths
            });
        var dailyProof = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            dailyRequestPath);
        Write(dailyProofPath, dailyProof);
        var requestPath = Path.Combine(root, "request.json");
        Write(requestPath,
            new MoneyRevenueCropSettlementCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                DailyCapacityProofPath = dailyProofPath
            });
        var proof = MoneyRevenueCropSettlementCapacityProofBuilder.Build(
            requestPath);
        Require(proof.SettlementDayCount == 5 &&
                proof.Days.All(day =>
                    day.Waves.Length == 2 &&
                    day.MinimumShippingUnits == 5 &&
                    day.MinimumGrossRevenue == 600 &&
                    day.RequiredQueueCount == 5 &&
                    day.QueueItemCountPerTransition == 1 &&
                    day.TimeBudgetFits) &&
                proof.CoveredTerminalRequestIds.Length == 10 &&
                proof.AggregateSettlementCapacityVerified,
            "Same-day crop settlement waves were not aggregated exactly.");

        var lateSnapshot = MoneyCropDailyCapacitySnapshot(120, 100);
        var lateTime = JsonNode.Parse(
            lateSnapshot.State["time"].GetRawText())!.AsObject();
        lateTime["time"]!["value"] = 1850;
        lateSnapshot.State["time"] = JsonSerializer.SerializeToElement(
            lateTime,
            JsonDefaults.Options);
        lateSnapshot.StateHash = SnapshotHash.ComputeStateHash(
            lateSnapshot.State);
        var lateTargets = new[]
        {
            new CropMaintenanceTileTarget(5, 6, false, true, true, false),
            new CropMaintenanceTileTarget(6, 6, false, true, true, false),
            new CropMaintenanceTileTarget(7, 6, false, true, true, false),
            new CropMaintenanceTileTarget(8, 6, false, true, true, false),
            new CropMaintenanceTileTarget(9, 6, false, true, true, false)
        };
        var lateEvaluation = CropSettlementCapacityEvaluator.Evaluate(
            lateSnapshot,
            "Farm",
            lateTargets,
            dailyProof.Days.Single(day => day.TotalDay == 120).TotalTicks,
            shippingActionCount: 5);
        Require(!lateEvaluation.TimeBudgetFits &&
                lateEvaluation.BlockingReasons.Contains(
                    "crop_settlement_time_budget_exceeded",
                    StringComparer.Ordinal),
            "A crop shipping tail beyond the recovery cutoff was admitted.");
    }

    private static CropPlantingCommitmentUpsertRequest
        SettlementCapacityCommitment(
            SnapshotEnvelope snapshot,
            int expectedRevision,
            string commitmentId,
            int tileCount) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = expectedRevision,
            CommitmentId = commitmentId,
            SourceDecisionId = "teacher." + commitmentId,
            SeedId = "745",
            TileCount = tileCount,
            PlantingYear = 2,
            PlantingSeason = "spring",
            PlantingDayOfMonth = 1,
            LocationContext = "outdoor_seasonal"
        };
}
