using StardewAI.Contracts.State;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeProductionSegmentBuilder
{
    private const string SummaryV1 =
        "money_crop_native_production_run_summary.v1";
    private const string SummaryV2 =
        "money_crop_native_production_run_summary.v2";

    public static MoneyRevenueCropNativeProductionSegmentReceipt Build(
        string summaryPath)
    {
        var fullSummaryPath = RequiredFile(
            summaryPath,
            "Money Revenue crop native-production run summary");
        var runRoot = Path.GetDirectoryName(fullSummaryPath)!;
        var summary = CurrentTeacherFrontierSupport.Read<
            MoneyCropNativeProductionRunSummary>(
                fullSummaryPath,
                "Money Revenue crop native-production run summary");
        Require(summary.SchemaVersion is SummaryV1 or SummaryV2 &&
                summary.Status == "passed" &&
                !string.IsNullOrWhiteSpace(summary.RunId) &&
                summary.TransitionCount > 0 &&
                summary.ExecutedCropBatchCount > 0 &&
                !summary.FormalProductTrainingAuthorized,
            "Money Revenue crop native-production run summary is invalid.");

        var inputPath = VerifyArtifact(
            summary.ProductionInputManifestPath,
            summary.ProductionInputManifestSha256,
            "crop native-production input manifest");
        var input = CurrentTeacherFrontierSupport.Read<
            MoneyCropNativeProductionInputManifest>(
                inputPath,
                "Money Revenue crop native-production input manifest");
        ValidateInput(input);

        VerifySourceAndRuntimeIsolation(summary, input, runRoot);
        var teacherDecision = VerifyTeacherDecision(input);
        var decisionReceipt = VerifyDecisionReceipt(input, teacherDecision);
        var productionPlan = VerifyProductionPlan(input);
        var runPlan = VerifyRunPlan(summary, input, runRoot);
        Require(decisionReceipt.DecisionId == input.DecisionId &&
                decisionReceipt.AllCommitmentsAccepted &&
                decisionReceipt.FinalLedger is not null &&
                productionPlan.Commitments.Length == input.CommitmentCount &&
                runPlan.CoveredCommitmentIds.SequenceEqual(
                    productionPlan.Commitments.Select(value =>
                        value.CommitmentId),
                    StringComparer.Ordinal),
            "Money Revenue crop production decision, obligation and run-plan lineage drifted.");

        var checkpointPath = RequiredRunFile(
            summary.CheckpointPath,
            Path.Combine(runRoot, "evidence"),
            "crop production proof checkpoint");
        var checkpoint = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionProofCheckpoint>(
                checkpointPath,
                "Money Revenue crop production proof checkpoint");
        var rebuiltCheckpoint =
            MoneyRevenueCropProductionProofCheckpointBuilder.Build(
                checkpoint.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(
                    checkpoint,
                    rebuiltCheckpoint) &&
                checkpoint.RequestCount == checkpoint.VerifiedFragmentCount &&
                checkpoint.PendingRequestCount == 0 &&
                checkpoint.PendingRequestIds.Length == 0 &&
                checkpoint.VerifiedTerminalSettlements.Length == 1 &&
                checkpoint.VerifiedSeedAcquisitions.Length > 0 &&
                checkpoint.VerifiedFragmentCount ==
                    summary.CheckpointVerifiedFragmentCount &&
                summary.CheckpointPendingRequestCount == 0 &&
                checkpoint.VerifiedTerminalSettlements.Length ==
                    summary.TerminalSettlementProofCount &&
                checkpoint.VerifiedSettlementCapacities.Length ==
                    summary.SettlementCapacityProofCount &&
                checkpoint.VerifiedSeedAcquisitions.Length ==
                    summary.SeedAcquisitionProofCount &&
                checkpoint.VerifiedTerminalSettlements.Sum(value =>
                    value.ShippingDeposits.Length) ==
                    summary.ShippingDepositCount &&
                !checkpoint.FutureCropProductionProofReady &&
                !checkpoint.FreshSaveRevenueScheduleReady &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money Revenue crop production checkpoint is incomplete or drifted from its run summary.");

        var terminal = checkpoint.VerifiedTerminalSettlements.Single();
        Require(terminal.TerminalKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement &&
                terminal.NativeTerminalSettlementVerified &&
                !terminal.FormalProductTrainingAuthorized,
            "Money Revenue crop production terminal proof is not one verified native shipping settlement.");
        var recurrenceManifestPath = VerifyArtifact(
            terminal.RevenueRecurrenceManifestPath,
            terminal.RevenueRecurrenceManifestSha256,
            "crop terminal Revenue recurrence manifest");
        var recurrenceReceiptPath = VerifyArtifact(
            terminal.RevenueRecurrenceReceiptPath,
            terminal.RevenueRecurrenceReceiptSha256,
            "crop terminal Revenue recurrence receipt");
        var recurrenceReceipt = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
                recurrenceReceiptPath,
                "Money Revenue crop terminal recurrence receipt");
        var rebuiltRecurrence = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        Require(BootstrapArtifactGuard.EqualJson(
                    recurrenceReceipt,
                    rebuiltRecurrence) &&
                recurrenceReceipt.ProofVerified &&
                recurrenceReceipt.Prefix is not null &&
                recurrenceReceipt.Transitions.Length == 1 &&
                !recurrenceReceipt.FormalProductTrainingAuthorized,
            "Money Revenue crop terminal recurrence did not rebuild exactly.");
        var prefix = recurrenceReceipt.Prefix!;

        var recurrenceManifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofManifest>(
                recurrenceManifestPath,
                "Money Revenue crop terminal recurrence manifest");
        var finalSnapshotPath = RequiredRunFile(
            recurrenceManifest.Transitions.Single().AfterSnapshotPath,
            Path.Combine(runRoot, "transitions"),
            "crop native-production final snapshot");
        var finalSnapshotSha256 =
            CurrentTeacherFrontierSupport.HashFile(finalSnapshotPath);
        var finalSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            finalSnapshotPath,
            "Money Revenue crop native-production final snapshot");
        var finalCheckpoint = MoneyRevenueProgressionVerifier.Project(
            finalSnapshot);
        var sourceSnapshotPath = VerifyArtifact(
            input.SnapshotPath,
            input.SnapshotSha256,
            "crop native-production source snapshot");
        var sourceSnapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sourceSnapshotPath,
            "Money Revenue crop native-production source snapshot");
        var sourceCheckpoint = MoneyRevenueProgressionVerifier.Project(
            sourceSnapshot);
        Require(sourceSnapshot.StateHash == input.SourceStateHash &&
                sourceCheckpoint.TotalDay == input.SourceTotalDay &&
                sourceCheckpoint.SaveId == finalCheckpoint.SaveId &&
                sourceCheckpoint.PlayerId == finalCheckpoint.PlayerId &&
                sourceCheckpoint.TotalMoneyEarned ==
                    prefix.InitialTotalMoneyEarned &&
                finalCheckpoint.StateHash == prefix.FinalStateHash &&
                finalCheckpoint.TotalDay == prefix.FinalTotalDay &&
                finalCheckpoint.Money == prefix.FinalMoney &&
                finalCheckpoint.TotalMoneyEarned ==
                    prefix.FinalTotalMoneyEarned &&
                terminal.TerminalActionAfterStateHash ==
                    finalCheckpoint.StateHash &&
                terminal.SettledRevenue == prefix.TotalRevenue,
            "Money Revenue crop source/final checkpoints drifted from the verified recurrence.");

        var isolatedSavePath = RequiredRunDirectory(
            summary.IsolatedSaveSlotPath,
            Path.Combine(runRoot, "isolated-saves"),
            "crop native-production isolated save slot");
        var finalIsolatedSaveSha256 =
            MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(
                isolatedSavePath);
        if (summary.SchemaVersion == SummaryV2)
        {
            Require(summary.IsolatedSaveSha256After ==
                        finalIsolatedSaveSha256 &&
                    SamePath(summary.FinalSnapshotPath, finalSnapshotPath) &&
                    summary.FinalSnapshotSha256 == finalSnapshotSha256 &&
                    summary.FinalStateHash == finalCheckpoint.StateHash,
                "Money Revenue crop native-production v2 final identities drifted.");
        }

        var selected = teacherDecision.TeacherCandidateEvaluations
            .Single(value => value.Selected);
        var commitment = productionPlan.Commitments.Single();
        Require(teacherDecision.Commitments.Length == 1 &&
                commitment.CommitmentId ==
                    teacherDecision.Commitments[0].CommitmentId &&
                commitment.SeedQualifiedItemId ==
                    selected.SeedQualifiedItemId &&
                commitment.TileCount == selected.TileCount &&
                commitment.MinimumGrossRevenue ==
                    selected.MinimumGrossRevenue &&
                terminal.MinimumGrossRevenue ==
                    selected.MinimumGrossRevenue &&
                terminal.SettledQuantity >= commitment.MinimumTotalUnits,
            "Money Revenue crop Teacher selection drifted from native production evidence.");

        var actualSeedCost = checkpoint.VerifiedSeedAcquisitions.Sum(proof =>
            proof.Transitions.Sum(transition => (long)transition.MoneyDecrease));
        var actualGrossRevenue = (long)prefix.TotalRevenue;
        var actualNetRevenue = checked(actualGrossRevenue - actualSeedCost);
        Require(actualSeedCost == selected.SeedAcquisitionCost &&
                actualGrossRevenue >= selected.MinimumGrossRevenue &&
                actualNetRevenue >= selected.MinimumNetRevenue,
            "Money Revenue crop native outcome missed its Teacher production bounds.");

        var crossedCriterionIds = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .Where(value =>
                sourceCheckpoint.TotalMoneyEarned < value.Target &&
                finalCheckpoint.TotalMoneyEarned >= value.Target)
            .Select(value => value.CriterionId)
            .ToArray();
        var artifacts = BuildArtifacts(
            fullSummaryPath,
            inputPath,
            input,
            checkpointPath,
            checkpoint,
            recurrenceManifestPath,
            recurrenceReceiptPath,
            finalSnapshotPath,
            finalSnapshotSha256,
            isolatedSavePath,
            finalIsolatedSaveSha256);
        var receipt = new MoneyRevenueCropNativeProductionSegmentReceipt
        {
            Status = "verified_native_crop_production_segment",
            RunSummaryPath = fullSummaryPath,
            RunSummarySha256 = CurrentTeacherFrontierSupport.HashFile(
                fullSummaryPath),
            RunId = summary.RunId,
            ProductionInputManifestPath = inputPath,
            ProductionInputManifestSha256 =
                summary.ProductionInputManifestSha256,
            DecisionId = input.DecisionId,
            SelectedSeedId = selected.SeedId,
            SelectedSeedQualifiedItemId = selected.SeedQualifiedItemId,
            SelectedTileCount = selected.TileCount,
            TeacherCandidateCount =
                teacherDecision.TeacherCandidateEvaluations.Length,
            TeacherMinimumGrossRevenue = selected.MinimumGrossRevenue,
            TeacherSeedAcquisitionCost = selected.SeedAcquisitionCost,
            TeacherMinimumNetRevenue = selected.MinimumNetRevenue,
            SourceCheckpoint = sourceCheckpoint,
            FinalCheckpoint = finalCheckpoint,
            FinalSnapshotPath = finalSnapshotPath,
            FinalSnapshotSha256 = finalSnapshotSha256,
            FinalIsolatedSaveSlotPath = isolatedSavePath,
            FinalIsolatedSaveSha256 = finalIsolatedSaveSha256,
            ActualSeedAcquisitionCost = actualSeedCost,
            ActualGrossRevenue = actualGrossRevenue,
            ActualCashNetRevenue = actualNetRevenue,
            CrossedCriterionIds = crossedCriterionIds,
            Artifacts = artifacts,
            TeacherDecisionVerified = true,
            NativeProductionSegmentVerified = true,
            TeacherComparisonVerified = false,
            FormalProductTrainingAuthorized = false
        };
        receipt.ReceiptSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                receipt);
        return receipt;
    }

    public static MoneyRevenueCropNativeProductionSegmentReceipt Verify(
        string receiptPath)
    {
        var fullPath = RequiredFile(
            receiptPath,
            "Money Revenue crop native-production segment receipt");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeProductionSegmentReceipt>(
                fullPath,
                "Money Revenue crop native-production segment receipt");
        var rebuilt = Build(persisted.RunSummaryPath);
        Require(persisted.SchemaVersion ==
                    MoneyRevenueCropNativeProductionSegmentVersions.Receipt &&
                persisted.Status ==
                    "verified_native_crop_production_segment" &&
                persisted.TeacherDecisionVerified &&
                persisted.NativeProductionSegmentVerified &&
                !persisted.TeacherComparisonVerified &&
                !persisted.FormalProductTrainingAuthorized &&
                BootstrapArtifactGuard.EqualJson(persisted, rebuilt),
            "Money Revenue crop native-production segment receipt did not rebuild exactly.");
        return persisted;
    }
}
