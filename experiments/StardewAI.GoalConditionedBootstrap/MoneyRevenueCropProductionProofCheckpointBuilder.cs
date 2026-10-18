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
                    "money_revenue_crop_production_proof_checkpoint_request.v12" &&
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
                    "money_revenue_crop_production_proof_fragment_index.v13" &&
                string.Equals(
                    Path.GetFullPath(fragmentIndex.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                fragmentIndex.CropProductionPlanSha256 == planSha256 &&
                fragmentIndex.PlantingLayoutProofPaths is not null &&
                fragmentIndex.SeedReservationProofPaths is not null &&
                fragmentIndex.SeedAcquisitionProofPaths is not null &&
                fragmentIndex.DailyCapacityProofPaths is not null &&
                fragmentIndex.NativeLifecycleProofPaths is not null &&
                fragmentIndex.OutputRetentionProofPaths is not null &&
                fragmentIndex.TerminalSettlementProofPaths is not null &&
                fragmentIndex.SettlementCapacityProofPaths is not null &&
                fragmentIndex.SharedShippingSettlementProofPaths is not null &&
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
        var dailyCapacityProofPaths =
            (fragmentIndex.DailyCapacityProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop daily-capacity proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop daily-capacity proof fragment"))
            .ToArray();
        Require(dailyCapacityProofPaths.Distinct(PathComparer).Count() ==
                    dailyCapacityProofPaths.Length,
            "Money Revenue crop daily-capacity proof paths are duplicated.");
        var dailyCapacityProofs = dailyCapacityProofPaths.Select(path =>
            VerifyDailyCapacityProof(path, planPath, planSha256, plan)).ToArray();
        var nativeLifecycleProofPaths =
            (fragmentIndex.NativeLifecycleProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop native-lifecycle proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop native-lifecycle proof fragment"))
            .ToArray();
        Require(nativeLifecycleProofPaths.Distinct(PathComparer).Count() ==
                    nativeLifecycleProofPaths.Length,
            "Money Revenue crop native-lifecycle proof paths are duplicated.");
        var nativeLifecycleProofs = nativeLifecycleProofPaths.Select(path =>
            VerifyNativeLifecycleProof(path, planPath, planSha256, plan)).ToArray();
        var outputRetentionProofPaths =
            (fragmentIndex.OutputRetentionProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop output-retention proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop output-retention proof fragment"))
            .ToArray();
        Require(outputRetentionProofPaths.Distinct(PathComparer).Count() ==
                    outputRetentionProofPaths.Length,
            "Money Revenue crop output-retention proof paths are duplicated.");
        var outputRetentionProofs = outputRetentionProofPaths.Select(path =>
            VerifyOutputRetentionProof(path, planPath, planSha256, plan)).ToArray();
        Require(outputRetentionProofs.Select(proof => proof.EvidenceRequestId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    outputRetentionProofs.Length,
            "Money Revenue crop output-retention evidence request IDs are duplicated.");
        var terminalSettlementProofPaths =
            (fragmentIndex.TerminalSettlementProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop terminal-settlement proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop terminal-settlement proof fragment"))
            .ToArray();
        Require(terminalSettlementProofPaths.Distinct(PathComparer).Count() ==
                    terminalSettlementProofPaths.Length,
            "Money Revenue crop terminal-settlement proof paths are duplicated.");
        var terminalSettlementProofs = terminalSettlementProofPaths.Select(path =>
            VerifyTerminalSettlementProof(path, planPath, planSha256, plan)).ToArray();
        Require(terminalSettlementProofs.All(proof =>
                outputRetentionProofPaths.Contains(
                    Path.GetFullPath(proof.OutputRetentionProofPath),
                    PathComparer)),
            "Money Revenue crop terminal settlement lacks its indexed retention support.");
        var settlementCapacityProofPaths =
            (fragmentIndex.SettlementCapacityProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop settlement-capacity proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop settlement-capacity proof fragment"))
            .ToArray();
        Require(settlementCapacityProofPaths.Length <= 1 &&
                settlementCapacityProofPaths.Distinct(PathComparer).Count() ==
                    settlementCapacityProofPaths.Length,
            "Money Revenue crop settlement-capacity proof paths are duplicated or ambiguous.");
        var settlementCapacityProofs = settlementCapacityProofPaths.Select(
                path => VerifySettlementCapacityProof(
                    path,
                    planPath,
                    planSha256))
            .ToArray();
        var sharedShippingSettlementProofPaths =
            (fragmentIndex.SharedShippingSettlementProofPaths ??
                throw new InvalidDataException(
                    "Money Revenue crop shared shipping settlement proof paths are missing."))
            .Select(path => RequiredFile(
                path,
                "Money Revenue crop shared shipping settlement proof fragment"))
            .ToArray();
        Require(sharedShippingSettlementProofPaths
                    .Distinct(PathComparer).Count() ==
                    sharedShippingSettlementProofPaths.Length,
            "Money Revenue crop shared shipping settlement proof paths are duplicated.");
        var sharedShippingSettlementProofs =
            sharedShippingSettlementProofPaths.Select(path =>
                VerifySharedShippingSettlementProof(
                    path,
                    planPath,
                    planSha256,
                    plan))
            .ToArray();
        Require(sharedShippingSettlementProofs.All(proof =>
                proof.Waves.All(wave =>
                    outputRetentionProofPaths.Contains(
                        Path.GetFullPath(wave.OutputRetentionProofPath),
                        PathComparer))) &&
                sharedShippingSettlementProofs.All(proof =>
                    settlementCapacityProofPaths.Contains(
                        Path.GetFullPath(proof.SettlementCapacityProofPath),
                        PathComparer)),
            "Money Revenue shared crop shipping settlement lacks indexed retention or capacity support.");
        var shippingSettlements = terminalSettlementProofs.Where(proof =>
                proof.TerminalKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement)
            .ToArray();
        Require(shippingSettlements.Length == 0 ||
                (settlementCapacityProofs.Length == 1 &&
                 shippingSettlements.All(proof =>
                     settlementCapacityProofs[0].CoveredTerminalRequestIds
                         .Contains(
                             proof.EvidenceRequestId,
                             StringComparer.Ordinal))),
            "Money Revenue crop shipping settlement lacks aggregate day capacity support.");

        var allEvidenceIds = proofs.Select(value => value.EvidenceRequestId)
            .Concat(seedProofs.Select(value => value.EvidenceRequestId))
            .Concat(acquisitionProofs.Select(value => value.EvidenceRequestId))
            .Concat(dailyCapacityProofs.Select(value => value.EvidenceRequestId))
            .Concat(nativeLifecycleProofs.Select(value => value.EvidenceRequestId))
            .Concat(terminalSettlementProofs.Select(value =>
                value.EvidenceRequestId))
            .Concat(sharedShippingSettlementProofs.SelectMany(value =>
                value.CoveredTerminalRequestIds))
            .ToArray();
        Require(allEvidenceIds.Distinct(StringComparer.Ordinal).Count() ==
                    allEvidenceIds.Length,
            "Money Revenue crop evidence request IDs are duplicated.");

        var proofByRequest = proofs.Select(value => (value.EvidenceRequestId, Value: (object)value))
            .Concat(seedProofs.Select(value => (value.EvidenceRequestId, Value: (object)value)))
            .Concat(acquisitionProofs.Select(value =>
                (value.EvidenceRequestId, Value: (object)value)))
            .Concat(dailyCapacityProofs.Select(value =>
                (value.EvidenceRequestId, Value: (object)value)))
            .Concat(nativeLifecycleProofs.Select(value =>
                (value.EvidenceRequestId, Value: (object)value)))
            .Concat(terminalSettlementProofs.Select(value =>
                (value.EvidenceRequestId, Value: (object)value)))
            .Concat(sharedShippingSettlementProofs.SelectMany(value =>
                value.CoveredTerminalRequestIds.Select(requestId =>
                    (EvidenceRequestId: requestId, Value: (object)value))))
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
            VerifiedDailyCapacities = dailyCapacityProofs,
            VerifiedNativeLifecycles = nativeLifecycleProofs,
            VerifiedOutputRetentions = outputRetentionProofs,
            VerifiedTerminalSettlements = terminalSettlementProofs,
            VerifiedSettlementCapacities = settlementCapacityProofs,
            VerifiedSharedShippingSettlements =
                sharedShippingSettlementProofs,
            FutureCropProductionProofReady = false,
            FreshSaveRevenueScheduleReady = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueCropSharedShippingSettlementProof
        VerifySharedShippingSettlementProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSharedShippingSettlementProof>(
                proofPath,
                "Money Revenue crop shared shipping settlement proof");
        var rebuilt = MoneyRevenueCropSharedShippingSettlementProofBuilder
            .Build(proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_shared_shipping_settlement_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(
                    Path.GetFullPath(proof.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
                proof.CoveredTerminalRequestIds.Length > 1 &&
                proof.CoveredTerminalRequestIds.All(requestId =>
                    plan.PendingEvidenceRequestIds.Contains(
                        requestId,
                        StringComparer.Ordinal)) &&
                proof.SharedNativeShippingSettlementVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue shared crop shipping settlement proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropSettlementCapacityProof
        VerifySettlementCapacityProof(
            string proofPath,
            string planPath,
            string planSha256)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropSettlementCapacityProof>(
                proofPath,
                "Money Revenue crop settlement-capacity proof");
        var rebuilt = MoneyRevenueCropSettlementCapacityProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_settlement_capacity_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(
                    Path.GetFullPath(proof.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
                proof.AggregateSettlementCapacityVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop settlement-capacity proof is not exact.");
        return proof;
    }

    private static MoneyRevenueCropTerminalSettlementProof
        VerifyTerminalSettlementProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropTerminalSettlementProof>(
                proofPath,
                "Money Revenue crop terminal-settlement proof");
        var rebuilt = MoneyRevenueCropTerminalSettlementProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_terminal_settlement_proof.v2" &&
                BootstrapArtifactGuard.EqualJson(proof, rebuilt) &&
                string.Equals(
                    Path.GetFullPath(proof.CropProductionPlanPath),
                    planPath,
                    PathComparison) &&
                proof.CropProductionPlanSha256 == planSha256 &&
                plan.PendingEvidenceRequestIds.Contains(
                    proof.EvidenceRequestId,
                    StringComparer.Ordinal) &&
                proof.NativeTerminalSettlementVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop terminal-settlement proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropOutputRetentionProof
        VerifyOutputRetentionProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropOutputRetentionProof>(
                proofPath,
                "Money Revenue crop output-retention proof");
        var rebuilt = MoneyRevenueCropOutputRetentionProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_output_retention_proof.v1" &&
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
                    "output-retention-terminal:" +
                    proof.OutputRetentionKey &&
                proof.NativeOutputRetentionVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop output-retention proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropNativeLifecycleProof
        VerifyNativeLifecycleProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeLifecycleProof>(
                proofPath,
                "Money Revenue crop native-lifecycle proof");
        var rebuilt = MoneyRevenueCropNativeLifecycleProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_native_lifecycle_proof.v3" &&
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
                    "native-crop-lifecycle:" + proof.CommitmentId &&
                proof.SharedNativeTrajectoryVerified &&
                proof.SharedTransitionDenominatorCommitmentIds
                    .SequenceEqual(
                        plan.Commitments.Select(value => value.CommitmentId)
                            .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal) &&
                proof.NativeCropLifecycleVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop native-lifecycle proof is not exact or is outside the denominator.");
        return proof;
    }

    private static MoneyRevenueCropDailyCapacityProof
        VerifyDailyCapacityProof(
            string proofPath,
            string planPath,
            string planSha256,
            MoneyRevenueCropProductionObligationPlan plan)
    {
        var proof = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropDailyCapacityProof>(
                proofPath,
                "Money Revenue crop daily-capacity proof");
        var rebuilt = MoneyRevenueCropDailyCapacityProofBuilder.Build(
            proof.RequestPath);
        Require(proof.SchemaVersion ==
                    "money_revenue_crop_daily_capacity_proof.v1" &&
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
                    "daily-capacity:" + proof.CommitmentId &&
                proof.CoveredCommitmentIds.SequenceEqual(
                    plan.Commitments.Select(value => value.CommitmentId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal) &&
                proof.DailyCapacityVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money Revenue crop daily-capacity proof is not exact or is outside the denominator.");
        return proof;
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
                    "money_revenue_crop_seed_acquisition_proof.v2" &&
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
