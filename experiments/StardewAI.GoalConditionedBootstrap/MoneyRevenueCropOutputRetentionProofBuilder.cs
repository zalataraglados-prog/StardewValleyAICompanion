using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropOutputRetentionProofBuilder
{
    public static MoneyRevenueCropOutputRetentionProof Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop output-retention proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropOutputRetentionProofRequest>(
                fullRequestPath,
                "Money Revenue crop output-retention proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_output_retention_proof_request.v1" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                request.WaveIndex > 0 &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop output-retention proof request is invalid.");

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
        var commitment = plan.Commitments.SingleOrDefault(value =>
            value.CommitmentId == request.CommitmentId);
        Require(commitment is not null,
            "Money Revenue crop output-retention commitment is absent.");
        var verifiedCommitment = commitment ?? throw new InvalidDataException(
            "Money Revenue crop output-retention commitment is absent.");
        var waves = verifiedCommitment.HarvestWaves.Where(value =>
            value.WaveIndex == request.WaveIndex).ToArray();
        Require(waves.Length == 1,
            "Money Revenue crop output-retention wave is not unique.");
        var wave = waves[0];
        var evidenceRequestId =
            "output-retention-terminal:" + wave.OutputRetentionKey;
        Require(plan.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal) &&
                verifiedCommitment.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal) &&
                wave.MinimumUnits > 0 &&
                wave.MinimumGrossRevenue > 0,
            "Money Revenue crop output-retention wave is outside the plan denominator.");

        var lifecyclePath = RequiredFile(
            request.NativeLifecycleProofPath,
            "Money Revenue crop native-lifecycle proof");
        var lifecycle = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeLifecycleProof>(
                lifecyclePath,
                "Money Revenue crop native-lifecycle proof");
        var rebuiltLifecycle = MoneyRevenueCropNativeLifecycleProofBuilder.Build(
            lifecycle.RequestPath);
        Require(lifecycle.SchemaVersion ==
                    "money_revenue_crop_native_lifecycle_proof.v2" &&
                BootstrapArtifactGuard.EqualJson(
                    lifecycle,
                    rebuiltLifecycle) &&
                SamePath(lifecycle.CropProductionPlanPath, planPath) &&
                lifecycle.CropProductionPlanSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(planPath) &&
                lifecycle.CommitmentId == verifiedCommitment.CommitmentId &&
                lifecycle.SharedNativeTrajectoryVerified &&
                lifecycle.SharedTransitionDenominatorCommitmentIds.Contains(
                    verifiedCommitment.CommitmentId,
                    StringComparer.Ordinal) &&
                lifecycle.NativeCropLifecycleVerified &&
                !lifecycle.FormalProductTrainingAuthorized,
            "Money Revenue crop native-lifecycle proof is not exact.");

        var harvestTransitions = lifecycle.Transitions
            .Where(value => value.TotalDay == wave.HarvestTotalDay &&
                value.TransitionKind == "crop_actions" &&
                value.OptionId == "executor.harvest_crop" &&
                value.ActionCommitmentId ==
                    verifiedCommitment.CommitmentId)
            .ToArray();
        Require(harvestTransitions.Length > 0 &&
                harvestTransitions.All(value => value.Verified) &&
                harvestTransitions.Sum(value => value.ItemCount) ==
                    verifiedCommitment.TileCount,
            "Money Revenue crop output-retention harvest denominator is incomplete.");

        var firstIndex = Array.IndexOf(
            lifecycle.Transitions,
            harvestTransitions[0]);
        var lastIndex = Array.IndexOf(
            lifecycle.Transitions,
            harvestTransitions[^1]);
        Require(firstIndex >= 0 && lastIndex >= firstIndex &&
                lifecycle.Transitions[firstIndex..(lastIndex + 1)]
                    .SequenceEqual(harvestTransitions),
            "Money Revenue crop output-retention harvest queues are not contiguous.");

        var beforePath = RequiredBoundFile(
            harvestTransitions[0].BeforeSnapshotPath,
            harvestTransitions[0].BeforeSnapshotSha256,
            "Money Revenue crop output-retention before snapshot");
        var afterPath = RequiredBoundFile(
            harvestTransitions[^1].AfterSnapshotPath,
            harvestTransitions[^1].AfterSnapshotSha256,
            "Money Revenue crop output-retention after snapshot");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Money Revenue crop output-retention before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Money Revenue crop output-retention after snapshot");
        Require(before.StateHash == harvestTransitions[0].BeforeStateHash &&
                after.StateHash == harvestTransitions[^1].AfterStateHash,
            "Money Revenue crop output-retention snapshot hashes drifted.");
        var receipt = ExactInventoryReceiptVerifier.Verify(
            before,
            after,
            verifiedCommitment.HarvestItemQualifiedId,
            wave.MinimumUnits,
            minimumQuality: 0);
        Require(receipt.Resolved && receipt.Verified &&
                receipt.BeforeQuantity.HasValue &&
                receipt.AfterQuantity.HasValue &&
                receipt.QuantityIncrease.HasValue,
            "Money Revenue crop output was not retained in exact player inventory: " +
            string.Join(",", receipt.BlockingReasons));
        var beforeQuantity = receipt.BeforeQuantity ??
            throw new InvalidDataException(
                "Money Revenue crop output-retention before quantity is unavailable.");
        var afterQuantity = receipt.AfterQuantity ??
            throw new InvalidDataException(
                "Money Revenue crop output-retention after quantity is unavailable.");
        var quantityIncrease = receipt.QuantityIncrease ??
            throw new InvalidDataException(
                "Money Revenue crop output-retention quantity increase is unavailable.");

        var queueEvidence = harvestTransitions.Select(value =>
        {
            var queuePath = RequiredBoundFile(
                value.QueuePath,
                value.QueueSha256,
                "Money Revenue crop output-retention harvest queue");
            var receiptPath = RequiredBoundFile(
                value.ExecutionReceiptPath,
                value.ExecutionReceiptSha256,
                "Money Revenue crop output-retention harvest receipt");
            return new MoneyRevenueCropOutputRetentionHarvestQueueEvidence(
                value.QueueId,
                queuePath,
                value.QueueSha256,
                receiptPath,
                value.ExecutionReceiptSha256,
                value.BeforeStateHash,
                value.AfterStateHash,
                value.ItemCount);
        }).ToArray();

        return new MoneyRevenueCropOutputRetentionProof
        {
            Status = "verified_native_crop_output_retention",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 =
                CurrentTeacherFrontierSupport.HashFile(planPath),
            NativeLifecycleProofPath = lifecyclePath,
            NativeLifecycleProofSha256 =
                CurrentTeacherFrontierSupport.HashFile(lifecyclePath),
            EvidenceRequestId = evidenceRequestId,
            CommitmentId = verifiedCommitment.CommitmentId,
            WaveIndex = wave.WaveIndex,
            HarvestTotalDay = wave.HarvestTotalDay,
            OutputRetentionKey = wave.OutputRetentionKey,
            HarvestItemQualifiedId =
                verifiedCommitment.HarvestItemQualifiedId,
            MinimumUnits = wave.MinimumUnits,
            MinimumGrossRevenue = wave.MinimumGrossRevenue,
            BeforeSnapshotPath = beforePath,
            BeforeSnapshotSha256 = harvestTransitions[0].BeforeSnapshotSha256,
            BeforeStateHash = before.StateHash,
            AfterSnapshotPath = afterPath,
            AfterSnapshotSha256 = harvestTransitions[^1].AfterSnapshotSha256,
            AfterStateHash = after.StateHash,
            InventoryQuantityBefore = beforeQuantity,
            InventoryQuantityAfter = afterQuantity,
            InventoryQuantityIncrease = quantityIncrease,
            HarvestQueueCount = queueEvidence.Length,
            HarvestActionCount = queueEvidence.Sum(value =>
                value.HarvestActionCount),
            HarvestQueues = queueEvidence,
            NativeOutputRetentionVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static string RequiredBoundFile(
        string path,
        string expectedSha256,
        string label)
    {
        var fullPath = RequiredFile(path, label);
        Require(!string.IsNullOrWhiteSpace(expectedSha256) &&
                CurrentTeacherFrontierSupport.HashFile(fullPath) ==
                    expectedSha256,
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
}
