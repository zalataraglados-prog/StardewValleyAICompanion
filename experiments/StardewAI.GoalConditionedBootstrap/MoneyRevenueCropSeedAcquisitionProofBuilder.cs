using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    public static MoneyRevenueCropSeedAcquisitionProof Build(
        string requestPath)
    {
        var requestFile = RequiredFile(
            requestPath,
            "Money Revenue crop seed-acquisition proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionProofRequest>(
                requestFile,
                "Money Revenue crop seed-acquisition proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_seed_acquisition_proof_request.v1" &&
                !string.IsNullOrWhiteSpace(request.CommitmentId) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-acquisition proof request is invalid.");

        var planFile = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planFile,
                "Money Revenue crop-production plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder
            .Build(plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan),
            "Money Revenue crop-production plan is not exact.");
        var commitment = plan.Commitments.SingleOrDefault(value =>
            value.CommitmentId == request.CommitmentId);
        Require(commitment is not null &&
                commitment.SeedQuantityShortfall > 0,
            "Money Revenue crop commitment has no seed shortfall.");
        var exactCommitment = commitment ?? throw new InvalidDataException(
            "Money Revenue crop seed-acquisition commitment is missing.");
        var evidenceRequestId = EvidenceRequestId(exactCommitment);
        Require(plan.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal) &&
                exactCommitment.PendingEvidenceRequestIds.Contains(
                    evidenceRequestId,
                    StringComparer.Ordinal),
            "Money Revenue crop seed acquisition is outside the plan denominator.");

        var manifestFile = RequiredFile(
            request.AcquisitionRolloutProofManifestPath,
            "Money Revenue seed acquisition rollout proof manifest");
        var receiptFile = RequiredFile(
            request.AcquisitionRolloutProofReceiptPath,
            "Money Revenue seed acquisition rollout proof receipt");
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
                manifestFile,
                "Money Revenue seed acquisition rollout proof manifest");
        var persistedReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
                receiptFile,
                "Money Revenue seed acquisition rollout proof receipt");
        var rebuiltReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestFile);
        Require(BootstrapArtifactGuard.EqualJson(
                    persistedReceipt,
                    rebuiltReceipt) &&
                persistedReceipt.GoalId == "earn_money" &&
                persistedReceipt.ProofChainVerified &&
                persistedReceipt.PortfolioCompletionVerified &&
                persistedReceipt.TransitionCount == 1 &&
                persistedReceipt.ContinuationTransitionCount == 0 &&
                persistedReceipt.ManifestSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(manifestFile) &&
                !persistedReceipt.FormalTrainingAuthorized &&
                manifest.ContinuationTransitions?.Length == 0 &&
                !manifest.FormalTrainingAuthorized,
            "Money Revenue seed acquisition rollout proof is not one exact complete transition.");

        var initial = manifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Money Revenue seed acquisition rollout has no initial transition.");
        var freshReceiptFile = RequiredFile(
            initial.FreshTerminalReceiptPath,
            "Money Revenue seed acquisition fresh terminal receipt");
        var freshReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteFreshTerminalReceiptAdmission>(
                freshReceiptFile,
                "Money Revenue seed acquisition fresh terminal receipt");
        var beforeFile = RequiredFile(
            initial.ExecutionInputs.BeforeSnapshotPath,
            "Money Revenue seed acquisition before snapshot");
        var afterFile = RequiredFile(
            initial.AfterSnapshotPath,
            "Money Revenue seed acquisition after snapshot");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforeFile,
            "Money Revenue seed acquisition before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterFile,
            "Money Revenue seed acquisition after snapshot");

        return BuildProof(
            requestFile,
            CurrentTeacherFrontierSupport.HashFile(requestFile),
            planFile,
            CurrentTeacherFrontierSupport.HashFile(planFile),
            plan,
            exactCommitment,
            manifestFile,
            CurrentTeacherFrontierSupport.HashFile(manifestFile),
            receiptFile,
            CurrentTeacherFrontierSupport.HashFile(receiptFile),
            beforeFile,
            CurrentTeacherFrontierSupport.HashFile(beforeFile),
            before,
            afterFile,
            CurrentTeacherFrontierSupport.HashFile(afterFile),
            after,
            freshReceiptFile,
            CurrentTeacherFrontierSupport.HashFile(freshReceiptFile),
            freshReceipt,
            persistedReceipt);
    }

    internal static MoneyRevenueCropSeedAcquisitionProof BuildProof(
        string requestPath,
        string requestSha256,
        string planPath,
        string planSha256,
        MoneyRevenueCropProductionObligationPlan plan,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        string rolloutManifestPath,
        string rolloutManifestSha256,
        string rolloutReceiptPath,
        string rolloutReceiptSha256,
        string beforeSnapshotPath,
        string beforeSnapshotSha256,
        SnapshotEnvelope before,
        string afterSnapshotPath,
        string afterSnapshotSha256,
        SnapshotEnvelope after,
        string freshTerminalReceiptPath,
        string freshTerminalReceiptSha256,
        AcquisitionRouteFreshTerminalReceiptAdmission freshReceipt,
        AcquisitionRoutePortfolioRolloutProofReceipt rolloutReceipt)
    {
        ValidateSnapshot(before, "before");
        ValidateSnapshot(after, "after");
        var transition = freshReceipt.TerminalTransition ??
            throw new InvalidDataException(
                "Money Revenue crop seed acquisition has no terminal transition.");
        var beforeQuantity = transition.BeforeQuantity ??
            throw new InvalidDataException(
                "Money Revenue crop seed acquisition has no before quantity.");
        var afterQuantity = transition.AfterQuantity ??
            throw new InvalidDataException(
                "Money Revenue crop seed acquisition has no after quantity.");
        var quantityIncrease = transition.QuantityIncrease ??
            throw new InvalidDataException(
                "Money Revenue crop seed acquisition has no quantity increase.");
        var beforeTotalDay = ReadInt(before, "time", "total_days");
        var afterTotalDay = ReadInt(after, "time", "total_days");
        Require(SamePath(beforeSnapshotPath, plan.SnapshotPath) &&
                beforeSnapshotSha256 == plan.SnapshotSha256 &&
                before.StateHash == plan.SourceStateHash &&
                before.SaveId.Status == FieldStatus.Available &&
                before.PlayerId.Status == FieldStatus.Available &&
                after.SaveId.Status == FieldStatus.Available &&
                after.PlayerId.Status == FieldStatus.Available &&
                before.SaveId.Value == after.SaveId.Value &&
                before.PlayerId.Value == after.PlayerId.Value &&
                afterTotalDay >= beforeTotalDay &&
                afterTotalDay <= commitment.SeedSupplyDeadlineTotalDay &&
                (afterTotalDay != beforeTotalDay ||
                    after.GameTick >= before.GameTick) &&
                freshReceipt.Status == "verified_fresh_terminal_receipt" &&
                freshReceipt.GoalId == "earn_money" &&
                freshReceipt.QueueExecutionVerified &&
                freshReceipt.FreshTerminalReceiptVerified &&
                freshReceipt.RouteTrainingEvidenceEligible &&
                freshReceipt.BlockingReasons.Length == 0 &&
                !freshReceipt.FormalTrainingAuthorized &&
                freshReceipt.AfterSnapshotSha256 == afterSnapshotSha256 &&
                transition.Resolved &&
                transition.Verified &&
                transition.QualifiedItemId ==
                    commitment.SeedQualifiedItemId &&
                transition.RequiredAmount ==
                    commitment.SeedQuantityShortfall &&
                quantityIncrease ==
                    commitment.SeedQuantityShortfall &&
                (long)afterQuantity - beforeQuantity ==
                    commitment.SeedQuantityShortfall &&
                rolloutReceipt.GoalId == "earn_money" &&
                rolloutReceipt.TransitionCount == 1 &&
                rolloutReceipt.ContinuationTransitionCount == 0 &&
                rolloutReceipt.LatestStateHash == after.StateHash &&
                rolloutReceipt.ProofChainVerified &&
                rolloutReceipt.PortfolioCompletionVerified &&
                !rolloutReceipt.FormalTrainingAuthorized,
            "Money Revenue crop seed acquisition does not prove the exact shortfall before its deadline.");

        return new MoneyRevenueCropSeedAcquisitionProof
        {
            Status = "verified_exact_seed_shortfall_acquisition",
            RequestPath = requestPath,
            RequestSha256 = requestSha256,
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = planSha256,
            EvidenceRequestId = EvidenceRequestId(commitment),
            CommitmentId = commitment.CommitmentId,
            AcquisitionRolloutProofManifestPath = rolloutManifestPath,
            AcquisitionRolloutProofManifestSha256 = rolloutManifestSha256,
            AcquisitionRolloutProofReceiptPath = rolloutReceiptPath,
            AcquisitionRolloutProofReceiptSha256 = rolloutReceiptSha256,
            BeforeSnapshotPath = beforeSnapshotPath,
            BeforeSnapshotSha256 = beforeSnapshotSha256,
            BeforeStateHash = before.StateHash,
            AfterSnapshotPath = afterSnapshotPath,
            AfterSnapshotSha256 = afterSnapshotSha256,
            AfterStateHash = after.StateHash,
            FreshTerminalReceiptPath = freshTerminalReceiptPath,
            FreshTerminalReceiptSha256 = freshTerminalReceiptSha256,
            RouteOccurrenceId = freshReceipt.RouteOccurrenceId,
            SeedQualifiedItemId = commitment.SeedQualifiedItemId,
            RequiredShortfallQuantity = commitment.SeedQuantityShortfall,
            VerifiedQuantityIncrease = quantityIncrease,
            SeedSupplyDeadlineTotalDay =
                commitment.SeedSupplyDeadlineTotalDay,
            AcquisitionTotalDay = afterTotalDay,
            SeedAcquisitionVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static string EvidenceRequestId(
        MoneyRevenueCropProductionCommitmentPlan commitment) =>
        "seed-acquisition:" + commitment.CommitmentId + ":" +
        commitment.SeedQuantityShortfall;

    private static void ValidateSnapshot(
        SnapshotEnvelope snapshot,
        string boundary)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop seed acquisition " + boundary +
            " snapshot is invalid.");
    }

    private static int ReadInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !envelope.TryGetProperty("value", out var value) ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                "Money Revenue crop seed acquisition field is unavailable: " +
                section + "." + field);
        }
        return result;
    }

    private static bool SamePath(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
