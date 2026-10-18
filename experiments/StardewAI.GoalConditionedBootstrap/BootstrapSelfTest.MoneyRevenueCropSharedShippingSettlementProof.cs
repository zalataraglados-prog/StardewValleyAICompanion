using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifySharedMoneyCropShippingSettlement(
        string root,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan,
        string plantingSnapshotPath,
        string[] layoutPaths,
        string[] dailySnapshotPaths,
        string firstDailyProofPath,
        MoneyRevenueCropSettlementCapacityProof settlementCapacity)
    {
        var capacityProofPath = Path.Combine(
            root,
            "settlement-capacity-proof.json");
        Write(capacityProofPath, settlementCapacity);
        var dailyProofPaths = new[]
        {
            firstDailyProofPath,
            Path.Combine(root, "daily-proof-1.json")
        };
        var secondDailyRequestPath = Path.Combine(
            root,
            "daily-request-1.json");
        Write(secondDailyRequestPath,
            new MoneyRevenueCropDailyCapacityProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = plan.Commitments[1].CommitmentId,
                PlantingLayoutProofPaths = layoutPaths,
                DailySnapshotPaths = dailySnapshotPaths
            });
        Write(dailyProofPaths[1],
            MoneyRevenueCropDailyCapacityProofBuilder.Build(
                secondDailyRequestPath));

        var supports = BuildSharedSettlementLifecycleSupport(
            root,
            planPath,
            plantingSnapshotPath,
            plan,
            layoutPaths,
            dailyProofPaths,
            new[]
            {
                new[] { 5, 6 },
                new[] { 7, 8, 9 }
            });
        var retentionByRequestId = supports.ToDictionary(
            support => support.EvidenceRequestId,
            support => support.RetentionProofPath,
            StringComparer.Ordinal);
        var lifecyclePaths = supports.Select(support =>
            support.LifecycleProofPath).ToArray();
        var lifecycleProofs = lifecyclePaths.Select(path =>
            CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropNativeLifecycleProof>(
                    path,
                    "Shared crop lifecycle fixture"))
            .ToArray();
        Require(lifecycleProofs.All(proof =>
                    proof.SharedNativeTrajectoryVerified &&
                    proof.SharedTransitionDenominatorCommitmentIds
                        .SequenceEqual(
                            plan.Commitments.Select(value =>
                                    value.CommitmentId)
                                .OrderBy(value => value, StringComparer.Ordinal),
                            StringComparer.Ordinal)) &&
                lifecycleProofs.Select(proof =>
                        string.Join(",", proof.Transitions.Select(value =>
                            value.QueueSha256)))
                    .Distinct(StringComparer.Ordinal).Count() == 1 &&
                lifecycleProofs[0].Transitions
                    .Where(value => value.ActionCommitmentId.Length > 0)
                    .Select(value => value.ActionCommitmentId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        plan.Commitments.Select(value => value.CommitmentId)
                            .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal),
            "Shared crop lifecycle proofs did not retain one global trajectory with exact commitment attribution.");

        var sharedDay = settlementCapacity.Days.First(day =>
            day.Waves.Length == 2);
        var retentionPaths = sharedDay.Waves.Select(wave =>
                retentionByRequestId[wave.EvidenceRequestId])
            .ToArray();
        var retentions = retentionPaths.Select(path =>
            CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropOutputRetentionProof>(
                    path,
                    "Shared crop shipping retention fixture"))
            .ToArray();
        var terminalStart = retentions.Select(retention =>
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                retention.AfterSnapshotPath,
                "Shared crop shipping retention after fixture"))
            .MaxBy(snapshot => snapshot.GameTick) ??
            throw new InvalidDataException(
                "Shared crop shipping terminal start fixture is missing.");
        var terminalStartPath = Path.Combine(root, "shared-terminal-start.json");
        Write(terminalStartPath, terminalStart);

        var current = terminalStart;
        var globalDepositIndex = 0;
        var waveRequests = new List<
            MoneyRevenueCropSharedShippingWaveRequest>();
        foreach (var (retention, index) in retentions.Select((value, index) =>
                     (value, index)))
        {
            var group = BuildSharedShippingDepositGroup(
                root,
                "shared-wave-" + index,
                current,
                retention.HarvestItemQualifiedId,
                checked((int)(retention.MinimumGrossRevenue /
                    retention.MinimumUnits)),
                retention.MinimumUnits,
                ref globalDepositIndex);
            current = group.After;
            waveRequests.Add(new MoneyRevenueCropSharedShippingWaveRequest
            {
                OutputRetentionProofPath = retentionPaths[index],
                ShippingDepositTransitions = group.Transitions
            });
        }

        var recurrenceRoot = Path.Combine(root, "shared-shipping-recurrence");
        Directory.CreateDirectory(recurrenceRoot);
        var settlementAfter = MoneyCropShippingSettlementAfter(
            current,
            checked((int)sharedDay.MinimumGrossRevenue));
        var settlementQueue = MoneyCropShippingSettlementQueue(current);
        const string settlementCandidateId =
            "candidate.shared-crop-shipping-settlement";
        var settlementReceipt = MoneyCropQueueReceipt(
            settlementQueue,
            current,
            settlementAfter,
            settlementCandidateId,
            "sleep",
            new[]
            {
                new
                {
                    path = "player.money",
                    before = MoneyCropPlayerInt(current, "money"),
                    after = MoneyCropPlayerInt(settlementAfter, "money")
                }
            });
        var settlementQueuePath = Path.Combine(recurrenceRoot, "queue.json");
        var settlementReceiptPath = Path.Combine(
            recurrenceRoot,
            "receipt.json");
        var settlementAfterPath = Path.Combine(recurrenceRoot, "after.json");
        Write(settlementQueuePath, settlementQueue);
        Write(settlementReceiptPath, settlementReceipt);
        Write(settlementAfterPath, settlementAfter);
        var recurrenceManifestPath = Path.Combine(
            recurrenceRoot,
            "manifest.json");
        var recurrenceReceiptPath = Path.Combine(
            recurrenceRoot,
            "recurrence-receipt.json");
        var lastDeposit = waveRequests[^1].ShippingDepositTransitions[^1];
        Write(recurrenceManifestPath,
            new MoneyRevenueRecurrenceProofManifest
            {
                Transitions = new[]
                {
                    new MoneyRevenueRecurrenceTransitionProof
                    {
                        TransitionId = "shared-crop-shipping-day-settlement",
                        ProofKind = MoneyRevenueRecurrenceProofKinds
                            .NativeShippingSettlement,
                        QueuePath = settlementQueuePath,
                        BeforeSnapshotPath = lastDeposit.AfterSnapshotPath,
                        ExecutionReceiptPath = settlementReceiptPath,
                        AfterSnapshotPath = settlementAfterPath,
                        RunId = settlementReceipt.RunId,
                        ExecutorVersion = PolicyTrajectoryVersionPins
                            .RuntimeTestHarnessExecutor,
                        SelectedCandidateId = settlementCandidateId
                    }
                }
            });
        Write(recurrenceReceiptPath,
            MoneyRevenueRecurrenceProofBuilder.Build(recurrenceManifestPath));

        var sharedRequestPath = Path.Combine(root, "shared-request.json");
        var sharedProofPath = Path.Combine(root, "shared-proof.json");
        Write(sharedRequestPath,
            new MoneyRevenueCropSharedShippingSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                SettlementCapacityProofPath = capacityProofPath,
                TerminalStartSnapshotPath = terminalStartPath,
                Waves = waveRequests.ToArray(),
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        var sharedProof =
            MoneyRevenueCropSharedShippingSettlementProofBuilder.Build(
                sharedRequestPath);
        Require(sharedProof.HarvestTotalDay == sharedDay.TotalDay &&
                sharedProof.CoveredTerminalRequestIds.SequenceEqual(
                    sharedDay.Waves.Select(wave => wave.EvidenceRequestId),
                    StringComparer.Ordinal) &&
                sharedProof.Waves.Length == 2 &&
                sharedProof.SettledQuantity == 5 &&
                sharedProof.SettledRevenue == 600 &&
                sharedProof.Waves[0].PendingValueBefore == 0 &&
                sharedProof.Waves[0].PendingValueAfter == 240 &&
                sharedProof.Waves[1].PendingValueBefore == 240 &&
                sharedProof.Waves[1].PendingValueAfter == 600 &&
                sharedProof.SharedNativeShippingSettlementVerified &&
                !sharedProof.FormalProductTrainingAuthorized,
            "Shared crop shipping settlement lost its aggregate native chain.");
        Write(sharedProofPath, sharedProof);

        var fragmentIndexPath = Path.Combine(
            root,
            "shared-fragment-index.json");
        var checkpointRequestPath = Path.Combine(
            root,
            "shared-checkpoint-request.json");
        Write(fragmentIndexPath,
            new MoneyRevenueCropProductionProofFragmentIndex
            {
                CropProductionPlanPath = planPath,
                CropProductionPlanSha256 =
                    CurrentTeacherFrontierSupport.HashFile(planPath),
                PlantingLayoutProofPaths = layoutPaths,
                DailyCapacityProofPaths = dailyProofPaths,
                NativeLifecycleProofPaths = lifecyclePaths,
                OutputRetentionProofPaths = retentionPaths,
                SettlementCapacityProofPaths = new[] { capacityProofPath },
                SharedShippingSettlementProofPaths = new[] { sharedProofPath }
            });
        Write(checkpointRequestPath,
            new MoneyRevenueCropProductionProofCheckpointRequest
            {
                CropProductionPlanPath = planPath,
                FragmentIndexPath = fragmentIndexPath
            });
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder
            .Build(checkpointRequestPath);
        Require(checkpoint.VerifiedFragmentCount == 8 &&
                checkpoint.VerifiedSharedShippingSettlements.Length == 1 &&
                sharedDay.Waves.All(wave =>
                    checkpoint.VerifiedRequestIds.Contains(
                        wave.EvidenceRequestId,
                        StringComparer.Ordinal)),
            "Shared crop shipping settlement did not close both terminal requests.");

        var duplicateRequestPath = Path.Combine(
            root,
            "shared-duplicate-request.json");
        Write(duplicateRequestPath,
            new MoneyRevenueCropSharedShippingSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                SettlementCapacityProofPath = capacityProofPath,
                TerminalStartSnapshotPath = terminalStartPath,
                Waves = new[] { waveRequests[0], waveRequests[0] },
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        RequireRejects(
            () => MoneyRevenueCropSharedShippingSettlementProofBuilder.Build(
                duplicateRequestPath),
            "A duplicate shared crop shipping wave was admitted.");
        var reversedRequestPath = Path.Combine(
            root,
            "shared-reversed-request.json");
        Write(reversedRequestPath,
            new MoneyRevenueCropSharedShippingSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                SettlementCapacityProofPath = capacityProofPath,
                TerminalStartSnapshotPath = terminalStartPath,
                Waves = waveRequests.AsEnumerable().Reverse().ToArray(),
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        RequireRejects(
            () => MoneyRevenueCropSharedShippingSettlementProofBuilder.Build(
                reversedRequestPath),
            "A shared crop shipping wave order that drifted from capacity support was admitted.");
    }

}
