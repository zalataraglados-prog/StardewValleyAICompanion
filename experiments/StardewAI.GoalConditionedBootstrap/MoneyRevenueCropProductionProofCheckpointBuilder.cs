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
                    "money_revenue_crop_production_proof_checkpoint_request.v3" &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop-production proof checkpoint request is invalid.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var planSha256 = CurrentTeacherFrontierSupport.HashFile(planPath);
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
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
                    "money_revenue_crop_production_proof_fragment_index.v4" &&
                string.Equals(
                    Path.GetFullPath(fragmentIndex.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                fragmentIndex.CropProductionPlanSha256 == planSha256 &&
                fragmentIndex.PlantingLayoutProofPaths is not null &&
                fragmentIndex.SeedReservationProofPaths is not null &&
                fragmentIndex.SeedAcquisitionProofPaths is not null &&
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
        Require(proofPaths.Distinct(PathComparer).Count() ==
                    proofPaths.Length,
            "Money Revenue crop planting-layout proof paths are duplicated.");
        var proofs = proofPaths.Select(path =>
                VerifyProof(path, planPath, planSha256, plan))
            .ToArray();
        var seedProofPaths = (fragmentIndex.SeedReservationProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop seed-reservation proof paths are missing."))
            .Select(path => RequiredFile(path, "Money Revenue crop seed-reservation proof fragment"))
            .ToArray();
        Require(seedProofPaths.Distinct(PathComparer).Count() == seedProofPaths.Length,
            "Money Revenue crop seed-reservation proof paths are duplicated.");
        var seedProofs = seedProofPaths.Select(path =>
            VerifySeedProof(path, planPath, planSha256, plan)).ToArray();
        var acquisitionProofPaths =
            (fragmentIndex.SeedAcquisitionProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop seed-acquisition proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop seed-acquisition proof fragment"))
            .ToArray();
        Require(acquisitionProofPaths.Distinct(PathComparer).Count() ==
                    acquisitionProofPaths.Length,
            "Money Revenue crop seed-acquisition proof paths are duplicated.");
        var acquisitionProofs = acquisitionProofPaths.Select(path =>
            VerifyAcquisitionProof(path, planPath, planSha256, plan)).ToArray();

        var allEvidenceIds = proofs.Select(value => value.EvidenceRequestId)
            .Concat(seedProofs.Select(value => value.EvidenceRequestId))
            .Concat(acquisitionProofs.Select(value => value.EvidenceRequestId))
            .ToArray();
        Require(allEvidenceIds.Distinct(StringComparer.Ordinal).Count() ==
                    allEvidenceIds.Length,
            "Money Revenue crop evidence request IDs are duplicated.");

        var proofByRequest = proofs.Select(value => (value.EvidenceRequestId, Value: (object)value))
            .Concat(seedProofs.Select(value => (value.EvidenceRequestId, Value: (object)value)))
            .Concat(acquisitionProofs.Select(value =>
                (value.EvidenceRequestId, Value: (object)value)))
            .ToDictionary(value => value.EvidenceRequestId, value => value.Value, StringComparer.Ordinal);
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
            CropProductionPlanSha256 = planSha256,
            FragmentIndexPath = fragmentIndexPath,
            FragmentIndexSha256 = CurrentTeacherFrontierSupport.HashFile(
                fragmentIndexPath),
            RequestCount = plan.PendingEvidenceRequestIds.Length,
            VerifiedFragmentCount = verifiedIds.Length,
            PendingRequestCount = pendingIds.Length,
            VerifiedRequestIds = verifiedIds,
            PendingRequestIds = pendingIds,
            VerifiedPlantingLayouts = proofs,
            VerifiedSeedReservations = seedProofs,
            VerifiedSeedAcquisitions = acquisitionProofs,
            FutureCropProductionProofReady = false,
            FreshSaveRevenueScheduleReady = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueCropSeedAcquisitionProof
        VerifyAcquisitionProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSeedAcquisitionProof>(
                proofPath,
                "Money Revenue crop seed-acquisition proof");
        var rebuilt = MoneyRevenueCropSeedAcquisitionProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_seed_acquisition_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(
                    Path.GetFullPath(proof.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
                plan.PendingEvidenceRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                proof.EvidenceRequestId ==
                    "seed-acquisition:" + proof.CommitmentId + ":" +
                    proof.RequiredShortfallQuantity &&
                proof.SeedAcquisitionVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-acquisition proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropSeedReservationProof VerifySeedProof(
        string proofPath,
        string planPath,
        string planSha256,
        MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropSeedReservationProof>(proofPath, "Money Revenue crop seed-reservation proof");
        var rebuilt = MoneyRevenueCropSeedReservationProofBuilder.Build(proof.RequestPath);
        Require(proof.SchemaVersion == "money_revenue_crop_seed_reservation_proof.v2" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(Path.GetFullPath(proof.CropProductionPlanPath), planPath, PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
                plan.PendingEvidenceRequestIds.Contains(proof.EvidenceRequestId, StringComparer.Ordinal) &&
                proof.EvidenceRequestId == "seed-reservation:" + proof.CommitmentId &&
                proof.SeedReservationVerified && !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop seed-reservation proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropPlantingLayoutProof VerifyProof(
        string proofPath,
        string planPath,
        string planSha256,
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
                    PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
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

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

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
