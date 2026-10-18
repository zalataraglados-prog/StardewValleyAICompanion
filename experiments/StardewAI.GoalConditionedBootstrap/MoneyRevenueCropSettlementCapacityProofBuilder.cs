using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropSettlementCapacityProofBuilder
{
    public static MoneyRevenueCropSettlementCapacityProof Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop settlement-capacity proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSettlementCapacityProofRequest>(
                fullRequestPath,
                "Money Revenue crop settlement-capacity proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_settlement_capacity_proof_request.v1" &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop settlement-capacity proof request is invalid.");

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
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");

        var dailyPath = RequiredFile(
            request.DailyCapacityProofPath,
            "Money Revenue crop daily-capacity proof");
        var daily = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropDailyCapacityProof>(
                dailyPath,
                "Money Revenue crop daily-capacity proof");
        var rebuiltDaily = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            daily.RequestPath);
        Require(daily.SchemaVersion ==
                    "money_revenue_crop_daily_capacity_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(daily, rebuiltDaily) &&
                SamePath(daily.CropProductionPlanPath, planPath) &&
                daily.DailyCapacityVerified &&
                daily.CoveredCommitmentIds.Order(StringComparer.Ordinal)
                    .SequenceEqual(
                        plan.Commitments.Select(row => row.CommitmentId)
                            .Order(StringComparer.Ordinal),
                        StringComparer.Ordinal) &&
                !daily.FormalProductTrainingAuthorized,
            "Money Revenue crop daily-capacity support is not exact.");

        var layouts = daily.PlantingLayoutProofSha256ByCommitment
            .Select(binding =>
            {
                var path = RequiredBoundFile(
                    binding.PlantingLayoutProofPath,
                    binding.PlantingLayoutProofSha256,
                    "Money Revenue crop settlement layout proof");
                var proof = CurrentTeacherFrontierSupport.Read<
                    MoneyRevenueCropPlantingLayoutProof>(
                        path,
                        "Money Revenue crop settlement layout proof");
                var rebuilt = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
                    proof.RequestPath);
                Require(BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                        proof.CommitmentId == binding.CommitmentId &&
                        proof.PlantingLayoutVerified,
                    "Money Revenue crop settlement layout proof is not exact.");
                return new LayoutState(proof, binding.RaisedCrop);
            })
            .ToDictionary(
                row => row.Proof.CommitmentId,
                StringComparer.Ordinal);
        Require(layouts.Count == plan.Commitments.Length,
            "Money Revenue crop settlement layout denominator is incomplete.");

        var dailyByDay = daily.Days.ToDictionary(
            row => row.TotalDay,
            row => row);
        var groups = plan.Commitments
            .SelectMany(commitment => commitment.HarvestWaves.Select(wave =>
                new WaveState(commitment, wave)))
            .GroupBy(row => row.Wave.HarvestTotalDay)
            .OrderBy(group => group.Key)
            .ToArray();
        var days = groups.Select(group =>
        {
            Require(dailyByDay.TryGetValue(group.Key, out var dailyDay),
                "Money Revenue crop settlement day lacks aggregate maintenance capacity: " +
                group.Key);
            var snapshotPath = RequiredBoundFile(
                dailyDay!.SnapshotPath,
                dailyDay.SnapshotSha256,
                "Money Revenue crop settlement-day snapshot");
            var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                snapshotPath,
                "Money Revenue crop settlement-day snapshot");
            Require(snapshot.StateHash == dailyDay.SnapshotStateHash &&
                    snapshot.StateHash ==
                        SnapshotHash.ComputeStateHash(snapshot.State),
                "Money Revenue crop settlement-day snapshot drifted.");
            var targets = plan.Commitments.SelectMany(commitment =>
            {
                var layout = layouts[commitment.CommitmentId];
                var plant = group.Key == commitment.PlantingTotalDay;
                var water = commitment.RequiredWateringTotalDays.Contains(
                    group.Key);
                var harvest = commitment.HarvestWaves.Any(wave =>
                    wave.HarvestTotalDay == group.Key);
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
            var waves = group.OrderBy(
                    row => row.Commitment.CommitmentId,
                    StringComparer.Ordinal)
                .ThenBy(row => row.Wave.WaveIndex)
                .Select(row => new MoneyRevenueCropSettlementCapacityWave(
                    row.Commitment.CommitmentId,
                    row.Wave.WaveIndex,
                    "output-retention-terminal:" +
                        row.Wave.OutputRetentionKey,
                    row.Wave.MinimumUnits,
                    row.Wave.MinimumGrossRevenue))
                .ToArray();
            var units = waves.Sum(row => row.MinimumUnits);
            var revenue = waves.Sum(row => row.MinimumGrossRevenue);
            var evaluation = CropSettlementCapacityEvaluator.Evaluate(
                snapshot,
                daily.LocationId,
                targets,
                dailyDay.TotalTicks,
                units);
            Require(evaluation.BlockingReasons.Length == 0 &&
                    evaluation.TimeBudgetFits &&
                    evaluation.AllTargetsReachable &&
                    evaluation.ShippingBinReachable &&
                    evaluation.RequiredQueueCount == units &&
                    evaluation.QueueItemCountPerTransition == 1,
                "Money Revenue crop settlement capacity is blocked on day " +
                group.Key + ": " +
                string.Join(",", evaluation.BlockingReasons));
            return new MoneyRevenueCropSettlementCapacityDay(
                group.Key,
                snapshotPath,
                Hash(snapshotPath),
                snapshot.StateHash,
                waves,
                units,
                revenue,
                evaluation.ExistingMaintenanceTicks,
                evaluation.TailMovementTileCount,
                evaluation.TailMovementTicks,
                evaluation.ShippingActionTicks,
                evaluation.RequiredQueueCount,
                evaluation.QueueItemCountPerTransition,
                evaluation.TotalTicks,
                evaluation.RequiredGameMinutes,
                evaluation.AvailableGameMinutes,
                evaluation.ShippingBinTileX,
                evaluation.ShippingBinTileY,
                evaluation.ShippingStandTileX,
                evaluation.ShippingStandTileY,
                evaluation.TimeBudgetFits,
                evaluation.AllTargetsReachable,
                evaluation.ShippingBinReachable);
        }).ToArray();
        var covered = days.SelectMany(day => day.Waves)
            .Select(wave => wave.EvidenceRequestId)
            .ToArray();
        Require(covered.Distinct(StringComparer.Ordinal).Count() ==
                    covered.Length &&
                covered.Order(StringComparer.Ordinal).SequenceEqual(
                    plan.PendingEvidenceRequestIds.Where(id => id.StartsWith(
                            "output-retention-terminal:",
                            StringComparison.Ordinal))
                        .Order(StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Money Revenue crop settlement capacity denominator drifted.");

        return new MoneyRevenueCropSettlementCapacityProof
        {
            Status = "verified_aggregate_crop_settlement_capacity",
            RequestPath = fullRequestPath,
            RequestSha256 = Hash(fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = Hash(planPath),
            DailyCapacityProofPath = dailyPath,
            DailyCapacityProofSha256 = Hash(dailyPath),
            SettlementCapacityId =
                "aggregate-settlement-capacity:" + plan.SourceStateHash,
            CoveredTerminalRequestIds = covered,
            SettlementDayCount = days.Length,
            Days = days,
            AggregateSettlementCapacityVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static string RequiredBoundFile(
        string path,
        string expectedSha256,
        string label)
    {
        var fullPath = RequiredFile(path, label);
        Require(Hash(fullPath) == expectedSha256,
            label + " hash drifted.");
        return fullPath;
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record LayoutState(
        MoneyRevenueCropPlantingLayoutProof Proof,
        bool RaisedCrop);

    private sealed record WaveState(
        MoneyRevenueCropProductionCommitmentPlan Commitment,
        MoneyRevenueCropHarvestWaveObligation Wave);
}
