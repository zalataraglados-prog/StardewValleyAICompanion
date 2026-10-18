using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SharedSettlementLifecycleSupport
        BuildSharedSettlementLifecycleSupport(
            string root,
            string planPath,
            string plantingSnapshotPath,
            MoneyRevenueCropProductionCommitmentPlan commitment,
            string layoutProofPath,
            string dailyProofPath,
            int[] targetXs,
            int fixtureIndex)
    {
        var fixtureRoot = Path.Combine(
            root,
            "shared-lifecycle-" + fixtureIndex);
        Directory.CreateDirectory(fixtureRoot);
        var current = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            plantingSnapshotPath,
            "Shared crop lifecycle planting fixture");
        var transitions = new List<
            MoneyRevenueCropNativeLifecycleTransitionRequest>();
        var sequence = 0;
        var finalDay = commitment.HarvestWaves.Max(wave =>
            wave.HarvestTotalDay);
        for (var totalDay = commitment.PlantingTotalDay;
             totalDay <= finalDay;
             totalDay++)
        {
            if (totalDay == commitment.PlantingTotalDay)
            {
                current = AddLifecycleTransition(
                    fixtureRoot,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.plant_seed",
                    targetXs);
            }
            if (commitment.RequiredWateringTotalDays.Contains(totalDay))
            {
                current = AddLifecycleTransition(
                    fixtureRoot,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.water_crop",
                    targetXs);
            }
            if (commitment.HarvestWaves.Any(wave =>
                    wave.HarvestTotalDay == totalDay))
            {
                current = AddLifecycleTransition(
                    fixtureRoot,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.harvest_crop",
                    targetXs);
            }
            if (totalDay < finalDay)
            {
                current = AddLifecycleTransition(
                    fixtureRoot,
                    transitions,
                    current,
                    totalDay,
                    ref sequence,
                    "executor.sleep",
                    targetXs);
            }
        }
        var lifecycleRequestPath = Path.Combine(
            fixtureRoot,
            "request.json");
        var lifecycleProofPath = Path.Combine(fixtureRoot, "proof.json");
        Write(lifecycleRequestPath,
            new MoneyRevenueCropNativeLifecycleProofRequest
            {
                CropProductionPlanPath = planPath,
                CommitmentId = commitment.CommitmentId,
                PlantingLayoutProofPath = layoutProofPath,
                DailyCapacityProofPath = dailyProofPath,
                Transitions = transitions.ToArray()
            });
        Write(lifecycleProofPath,
            MoneyRevenueCropNativeLifecycleProofBuilder.Build(
                lifecycleRequestPath));

        var wave = commitment.HarvestWaves[0];
        var retentionRequestPath = Path.Combine(
            fixtureRoot,
            "retention-request.json");
        var retentionProofPath = Path.Combine(
            fixtureRoot,
            "retention-proof.json");
        Write(retentionRequestPath,
            new MoneyRevenueCropOutputRetentionProofRequest
            {
                CropProductionPlanPath = planPath,
                NativeLifecycleProofPath = lifecycleProofPath,
                CommitmentId = commitment.CommitmentId,
                WaveIndex = wave.WaveIndex
            });
        var retention = MoneyRevenueCropOutputRetentionProofBuilder.Build(
            retentionRequestPath);
        Write(retentionProofPath, retention);
        return new SharedSettlementLifecycleSupport(
            lifecycleProofPath,
            retentionProofPath,
            retention.EvidenceRequestId);
    }

    private static SharedShippingDepositGroup BuildSharedShippingDepositGroup(
        string root,
        string prefix,
        SnapshotEnvelope start,
        string qualifiedItemId,
        int unitPrice,
        int quantity,
        ref int globalDepositIndex)
    {
        var current = start;
        var transitions = new List<
            MoneyRevenueTerminalQueueTransitionProof>();
        for (var index = 0; index < quantity; index++)
        {
            globalDepositIndex++;
            var transitionId = prefix + "-deposit-" + (index + 1);
            var transitionRoot = Path.Combine(root, transitionId);
            Directory.CreateDirectory(transitionRoot);
            var before = current;
            var after = MoneyCropShippingDepositAfter(
                before,
                qualifiedItemId,
                unitPrice);
            var candidateId = "candidate." + transitionId;
            var queue = MoneyCropShippingDepositQueue(
                before,
                qualifiedItemId,
                unitPrice,
                transitionId);
            var receipt = MoneyCropQueueReceipt(
                queue,
                before,
                after,
                candidateId,
                "ship_inventory_item_to_bin",
                new[]
                {
                    new
                    {
                        path = "player.inventory." + qualifiedItemId +
                            ".count",
                        before = MoneyCropInventoryCount(
                            before,
                            qualifiedItemId),
                        after = MoneyCropInventoryCount(
                            after,
                            qualifiedItemId)
                    },
                    new
                    {
                        path = "farm.shipping_bin." + qualifiedItemId +
                            ".count",
                        before = globalDepositIndex - 1,
                        after = globalDepositIndex
                    }
                });
            var queuePath = Path.Combine(transitionRoot, "queue.json");
            var beforePath = Path.Combine(transitionRoot, "before.json");
            var receiptPath = Path.Combine(transitionRoot, "receipt.json");
            var afterPath = Path.Combine(transitionRoot, "after.json");
            Write(queuePath, queue);
            Write(beforePath, before);
            Write(receiptPath, receipt);
            Write(afterPath, after);
            transitions.Add(new MoneyRevenueTerminalQueueTransitionProof
            {
                TransitionId = transitionId,
                QueuePath = queuePath,
                BeforeSnapshotPath = beforePath,
                ExecutionReceiptPath = receiptPath,
                AfterSnapshotPath = afterPath,
                RunId = receipt.RunId,
                ExecutorVersion =
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                SelectedCandidateId = candidateId
            });
            current = after;
        }
        return new SharedShippingDepositGroup(
            current,
            transitions.ToArray());
    }

    private sealed record SharedSettlementLifecycleSupport(
        string LifecycleProofPath,
        string RetentionProofPath,
        string EvidenceRequestId);

    private sealed record SharedShippingDepositGroup(
        SnapshotEnvelope After,
        MoneyRevenueTerminalQueueTransitionProof[] Transitions);
}
