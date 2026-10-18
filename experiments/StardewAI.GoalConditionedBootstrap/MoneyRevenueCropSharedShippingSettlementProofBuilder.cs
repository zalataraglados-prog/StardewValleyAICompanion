using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSharedShippingSettlementProofBuilder
{
    public static MoneyRevenueCropSharedShippingSettlementProof Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue shared crop shipping proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSharedShippingSettlementProofRequest>(
                fullRequestPath,
                "Money Revenue shared crop shipping proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_shared_shipping_settlement_proof_request.v1" &&
                request.RouteTransitions is not null &&
                request.Waves is { Length: > 1 } &&
                request.Waves.All(wave => wave is not null &&
                    wave.ShippingDepositTransitions is not null) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue shared crop shipping proof request is invalid.");
        var routeTransitions = request.RouteTransitions ??
            throw new InvalidDataException(
                "Money Revenue shared crop shipping route transitions are missing.");
        var waveRequests = request.Waves ??
            throw new InvalidDataException(
                "Money Revenue shared crop shipping waves are missing.");

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
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");

        var capacityPath = RequiredFile(
            request.SettlementCapacityProofPath,
            "Money Revenue crop settlement-capacity proof");
        var capacity = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSettlementCapacityProof>(
                capacityPath,
                "Money Revenue crop settlement-capacity proof");
        var rebuiltCapacity =
            MoneyRevenueCropSettlementCapacityProofBuilder.Build(
                capacity.RequestPath);
        Require(capacity.SchemaVersion ==
                    "money_revenue_crop_settlement_capacity_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(capacity, rebuiltCapacity) &&
                SamePath(capacity.CropProductionPlanPath, planPath) &&
                capacity.CropProductionPlanSha256 == Hash(planPath) &&
                capacity.AggregateSettlementCapacityVerified &&
                !capacity.FormalProductTrainingAuthorized,
            "Money Revenue crop settlement-capacity support is not exact.");

        var waves = waveRequests.Select(LoadWave).ToArray();
        Require(waves.Select(wave => wave.Retention.EvidenceRequestId)
                    .Distinct(StringComparer.Ordinal).Count() == waves.Length &&
                waves.Select(wave => wave.RetentionPath)
                    .Distinct(PathComparer).Count() == waves.Length,
            "Money Revenue shared crop shipping wave identities are duplicated.");
        foreach (var wave in waves)
        {
            var rebuilt = MoneyRevenueCropOutputRetentionProofBuilder.Build(
                wave.Retention.RequestPath);
            Require(wave.Retention.SchemaVersion ==
                        "money_revenue_crop_output_retention_proof.v1" &&
                    BootstrapArtifactGuard.EqualJson(wave.Retention, rebuilt) &&
                    SamePath(wave.Retention.CropProductionPlanPath, planPath) &&
                    wave.Retention.CropProductionPlanSha256 == Hash(planPath) &&
                    wave.Retention.NativeOutputRetentionVerified &&
                    plan.PendingEvidenceRequestIds.Contains(
                        wave.Retention.EvidenceRequestId,
                        StringComparer.Ordinal) &&
                    !wave.Retention.FormalProductTrainingAuthorized,
                "Money Revenue shared crop shipping retention support is not exact.");
        }

        var harvestDays = waves.Select(wave => wave.Retention.HarvestTotalDay)
            .Distinct().ToArray();
        Require(harvestDays.Length == 1,
            "Money Revenue shared crop shipping waves cross harvest days.");
        var harvestDay = harvestDays[0];
        var capacityDay = capacity.Days.SingleOrDefault(day =>
            day.TotalDay == harvestDay);
        Require(capacityDay is not null &&
                capacityDay.Waves.Length == waves.Length,
            "Money Revenue shared crop shipping day lacks exact aggregate capacity.");
        var exactCapacityDay = capacityDay ?? throw new InvalidDataException(
            "Money Revenue shared crop shipping day is missing.");
        for (var index = 0; index < waves.Length; index++)
        {
            var retained = waves[index].Retention;
            var planned = exactCapacityDay.Waves[index];
            Require(retained.EvidenceRequestId == planned.EvidenceRequestId &&
                    retained.CommitmentId == planned.CommitmentId &&
                    retained.WaveIndex == planned.WaveIndex &&
                    retained.MinimumUnits == planned.MinimumUnits &&
                    retained.MinimumGrossRevenue == planned.MinimumGrossRevenue,
                "Money Revenue shared crop shipping wave order drifted from capacity support.");
        }

        var terminalStartPath = RequiredFile(
            request.TerminalStartSnapshotPath,
            "Money Revenue shared crop shipping terminal start snapshot");
        var terminalStart = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            terminalStartPath,
            "Money Revenue shared crop shipping terminal start snapshot");
        Require(terminalStart.StateHash ==
                    SnapshotHash.ComputeStateHash(terminalStart.State) &&
                ReadAvailableInt(terminalStart, "time", "total_days") ==
                    harvestDay,
            "Money Revenue shared crop shipping terminal start is not an exact harvest-day snapshot.");
        foreach (var retention in waves.Select(wave => wave.Retention))
        {
            var retainedAfter = CurrentTeacherFrontierSupport.Read<
                SnapshotEnvelope>(
                    RequiredFile(
                        retention.AfterSnapshotPath,
                        "Money Revenue crop retention after snapshot"),
                    "Money Revenue crop retention after snapshot");
            Require(terminalStart.SaveId.Value == retainedAfter.SaveId.Value &&
                    terminalStart.PlayerId.Value == retainedAfter.PlayerId.Value &&
                    terminalStart.GameTick >= retainedAfter.GameTick,
                "Money Revenue shared crop shipping terminal snapshot crossed retention identity or time.");
        }
        foreach (var group in waves.GroupBy(wave =>
                     wave.Retention.HarvestItemQualifiedId,
                     StringComparer.Ordinal))
        {
            Require(InventoryCount(terminalStart, group.Key) >=
                    group.Sum(wave => wave.Retention.MinimumUnits),
                "Money Revenue shared crop shipping terminal inventory does not cover all retained waves for " +
                group.Key + ".");
        }

        var allTransitions = routeTransitions.Concat(
                waves.SelectMany(wave =>
                    wave.Request.ShippingDepositTransitions))
            .ToArray();
        Require(allTransitions.All(transition => transition is not null &&
                    !string.IsNullOrWhiteSpace(transition.TransitionId)) &&
                allTransitions.Select(transition => transition.TransitionId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    allTransitions.Length &&
                allTransitions.Select(transition =>
                        Path.GetFullPath(transition.QueuePath))
                    .Distinct(PathComparer).Count() == allTransitions.Length &&
                allTransitions.Select(transition =>
                        Path.GetFullPath(transition.ExecutionReceiptPath))
                    .Distinct(PathComparer).Count() == allTransitions.Length,
            "Money Revenue shared crop shipping transition identity is duplicated.");

        var current = terminalStart;
        long pendingValue = 0;
        var transitionOffset = 0;
        var waveEvidence = new List<
            MoneyRevenueCropSharedShippingWaveEvidence>();
        MoneyRevenueTerminalQueueTransitionEvidence[] routeEvidence =
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>();
        foreach (var (wave, index) in waves.Select((value, index) =>
                     (value, index)))
        {
            var batch = MoneyRevenueCropTerminalSettlementModeVerifier
                .VerifyShippingDepositBatch(
                    current,
                    index == 0
                        ? routeTransitions
                        : Array.Empty<
                            MoneyRevenueTerminalQueueTransitionProof>(),
                    wave.Request.ShippingDepositTransitions,
                    wave.Retention.HarvestItemQualifiedId,
                    wave.Retention.MinimumUnits,
                    wave.Retention.MinimumGrossRevenue,
                    pendingValue,
                    transitionOffset);
            if (index == 0)
                routeEvidence = batch.RouteTransitions;
            current = batch.After;
            pendingValue = batch.PendingValueAfter;
            transitionOffset += batch.Deposits.Length;
            waveEvidence.Add(new MoneyRevenueCropSharedShippingWaveEvidence(
                wave.Retention.EvidenceRequestId,
                wave.Retention.CommitmentId,
                wave.Retention.WaveIndex,
                wave.RetentionPath,
                Hash(wave.RetentionPath),
                wave.Retention.HarvestItemQualifiedId,
                wave.Retention.MinimumUnits,
                wave.Retention.MinimumGrossRevenue,
                batch.SlotIndex,
                batch.UnitPrice,
                batch.PendingValueBefore,
                batch.PendingValueAfter,
                batch.Deposits));
        }
        Require(transitionOffset == exactCapacityDay.MinimumShippingUnits &&
                pendingValue >= exactCapacityDay.MinimumGrossRevenue,
            "Money Revenue shared crop shipping deposits do not cover the aggregate capacity denominator.");
        var lastTransition = waves[^1].Request.ShippingDepositTransitions[^1];
        var recurrence = MoneyRevenueCropTerminalSettlementModeVerifier
            .VerifyShippingRecurrence(
                request.RevenueRecurrenceManifestPath,
                request.RevenueRecurrenceReceiptPath,
                lastTransition.AfterSnapshotPath,
                current.StateHash,
                pendingValue);

        return new MoneyRevenueCropSharedShippingSettlementProof
        {
            Status = "verified_shared_native_crop_shipping_settlement",
            RequestPath = fullRequestPath,
            RequestSha256 = Hash(fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = Hash(planPath),
            SettlementCapacityProofPath = capacityPath,
            SettlementCapacityProofSha256 = Hash(capacityPath),
            HarvestTotalDay = harvestDay,
            TerminalStartSnapshotPath = terminalStartPath,
            TerminalStartSnapshotSha256 = Hash(terminalStartPath),
            TerminalStartStateHash = terminalStart.StateHash,
            CoveredTerminalRequestIds = waveEvidence.Select(wave =>
                wave.EvidenceRequestId).ToArray(),
            RouteTransitions = routeEvidence,
            Waves = waveEvidence.ToArray(),
            SettledQuantity = transitionOffset,
            SettledRevenue = pendingValue,
            TerminalActionAfterStateHash = recurrence.FinalStateHash,
            RevenueRecurrenceManifestPath = recurrence.ManifestPath,
            RevenueRecurrenceManifestSha256 = recurrence.ManifestSha256,
            RevenueRecurrenceReceiptPath = recurrence.ReceiptPath,
            RevenueRecurrenceReceiptSha256 = recurrence.ReceiptSha256,
            SharedNativeShippingSettlementVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

}
