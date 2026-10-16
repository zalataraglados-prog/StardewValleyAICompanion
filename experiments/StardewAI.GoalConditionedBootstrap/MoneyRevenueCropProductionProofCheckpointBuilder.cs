namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropProductionProofCheckpointBuilder
{
    public static MoneyRevenueCropProductionProofCheckpoint Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop-production proof checkpoint request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionProofCheckpointRequest>(
                fullRequestPath,
                "Money Revenue crop-production proof checkpoint request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_production_proof_checkpoint_request.v1" &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop-production proof checkpoint request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                plan.PendingEvidenceRequestIds.Length > 0 &&
                plan.PendingEvidenceRequestIds.Distinct(StringComparer.Ordinal)
                    .Count() == plan.PendingEvidenceRequestIds.Length &&
                !plan.FutureCropProductionProofReady &&
                !plan.FreshSaveRevenueScheduleReady &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");

        var fragmentIndexPath = RequiredFile(
            request.FragmentIndexPath,
            "Money Revenue crop-production proof fragment index");
        var fragmentIndex = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionProofFragmentIndex>(
                fragmentIndexPath,
                "Money Revenue crop-production proof fragment index");
        Require(fragmentIndex.SchemaVersion ==
                    "money_revenue_crop_production_proof_fragment_index.v1" &&
                string.Equals(
                    Path.GetFullPath(fragmentIndex.CropProductionPlanPath),
                    planPath,
                    StringComparison.OrdinalIgnoreCase) &&
                fragmentIndex.PlantingLayoutProofPaths is not null &&
                !fragmentIndex.FormalProductTrainingAuthorized,
            "Money Revenue crop-production proof fragment index is invalid.");

        var proofPaths = (fragmentIndex.PlantingLayoutProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop planting-layout proof paths are missing."))
            .Select(path =>
                RequiredFile(
                    path,
                    "Money Revenue crop planting-layout proof fragment"))
            .ToArray();
        Require(proofPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                    proofPaths.Length,
            "Money Revenue crop planting-layout proof paths are duplicated.");
        var proofs = proofPaths.Select(path => VerifyProof(path, planPath, plan))
            .ToArray();
        Require(proofs.Select(value => value.EvidenceRequestId)
                    .Distinct(StringComparer.Ordinal).Count() == proofs.Length,
            "Money Revenue crop planting-layout evidence request IDs are duplicated.");

        var proofByRequest = proofs.ToDictionary(
            value => value.EvidenceRequestId,
            StringComparer.Ordinal);
        var verifiedIds = plan.PendingEvidenceRequestIds
            .Where(proofByRequest.ContainsKey)
            .ToArray();
        var pendingIds = plan.PendingEvidenceRequestIds
            .Where(requestId => !proofByRequest.ContainsKey(requestId))
            .ToArray();
        return new MoneyRevenueCropProductionProofCheckpoint
        {
            Status = "crop_production_proof_collection_in_progress",
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = CurrentTeacherFrontierSupport.HashFile(
                planPath),
            FragmentIndexPath = fragmentIndexPath,
            FragmentIndexSha256 = CurrentTeacherFrontierSupport.HashFile(
                fragmentIndexPath),
            RequestCount = plan.PendingEvidenceRequestIds.Length,
            VerifiedFragmentCount = verifiedIds.Length,
            PendingRequestCount = pendingIds.Length,
            VerifiedRequestIds = verifiedIds,
            PendingRequestIds = pendingIds,
            VerifiedPlantingLayouts = verifiedIds
                .Select(requestId => proofByRequest[requestId])
                .ToArray(),
            FutureCropProductionProofReady = false,
            FreshSaveRevenueScheduleReady = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueCropPlantingLayoutProof VerifyProof(
        string proofPath,
        string planPath,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropPlantingLayoutProof>(
                proofPath,
                "Money Revenue crop planting-layout proof");
        var rebuilt = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_planting_layout_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(
                    Path.GetFullPath(proof.CropProductionPlanPath),
                    planPath,
                    StringComparison.OrdinalIgnoreCase) &&
                plan.PendingEvidenceRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                proof.EvidenceRequestId ==
                    "planting-layout:" + proof.CommitmentId &&
                proof.SourceStateHash == plan.SourceStateHash &&
                proof.PlantingLayoutVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop planting-layout proof is not exact or is outside the denominator.");
        return proof;
    }

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
