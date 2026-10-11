namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentSapEvidenceExportBuilder
{
    private const string SapRequirementId = "full_shipment:item:92";
    private const string SapQualifiedItemId = "(O)92";

    public static FullShipmentSapEvidenceExport Build(
        string recurrenceManifestPath,
        string prefixCheckpointPath,
        FullShipmentRecurrencePrefixCheckpoint checkpoint)
    {
        var manifestPath = Path.GetFullPath(recurrenceManifestPath);
        var checkpointPath = Path.GetFullPath(prefixCheckpointPath);
        var manifest = CurrentTeacherFrontierSupport.Read<
            FullShipmentRecurrenceProofManifest>(
            manifestPath,
            "Full Shipment recurrence proof manifest");
        var iteration = SingleSapIteration(manifest);
        var rolloutManifestPath = Path.GetFullPath(
            iteration.AcquisitionRolloutProofManifestPath);
        var rolloutReceiptPath = Path.GetFullPath(
            iteration.AcquisitionRolloutProofReceiptPath);
        var rolloutManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            rolloutManifestPath,
            "Sap acquisition rollout proof manifest");
        var rolloutReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            rolloutReceiptPath,
            "Sap acquisition rollout proof receipt");

        return BuildReport(
            manifestPath,
            checkpointPath,
            rolloutManifestPath,
            rolloutReceiptPath,
            manifest,
            checkpoint,
            rolloutManifest,
            rolloutReceipt,
            CurrentTeacherFrontierSupport.HashFile(manifestPath),
            CurrentTeacherFrontierSupport.HashFile(checkpointPath),
            CurrentTeacherFrontierSupport.HashFile(rolloutManifestPath),
            CurrentTeacherFrontierSupport.HashFile(rolloutReceiptPath));
    }

    internal static FullShipmentSapEvidenceExport BuildReport(
        string recurrenceManifestPath,
        string prefixCheckpointPath,
        string rolloutManifestPath,
        string rolloutReceiptPath,
        FullShipmentRecurrenceProofManifest manifest,
        FullShipmentRecurrencePrefixCheckpoint checkpoint,
        AcquisitionRoutePortfolioRolloutProofManifest rolloutManifest,
        AcquisitionRoutePortfolioRolloutProofReceipt rolloutReceipt,
        string recurrenceManifestSha256,
        string prefixCheckpointSha256,
        string rolloutManifestSha256,
        string rolloutReceiptSha256)
    {
        Require(
            manifest.SchemaVersion ==
                "full_shipment_recurrence_proof_manifest.v1" &&
            !manifest.FormalTrainingAuthorized,
            "Sap recurrence manifest is invalid.");
        var iteration = SingleSapIteration(manifest);
        Require(
            checkpoint.SchemaVersion ==
                "full_shipment_recurrence_prefix_checkpoint.v1" &&
            checkpoint.Status == "verified_recurrence_prefix" &&
            checkpoint.PrefixProofVerified &&
            checkpoint.RequiredItemCount == 154 &&
            checkpoint.VerifiedIterationCount == 1 &&
            checkpoint.RemainingItemCount == 153 &&
            checkpoint.CompletedRequirementIds.SequenceEqual(
                new[] { SapRequirementId },
                StringComparer.Ordinal) &&
            checkpoint.ReadyForNextIteration &&
            !checkpoint.Complete &&
            !checkpoint.Achievement34Verified &&
            !checkpoint.FormalTrainingAuthorized &&
            checkpoint.BlockingReasons.Length == 0 &&
            string.Equals(
                checkpoint.ManifestSha256,
                recurrenceManifestSha256,
                StringComparison.OrdinalIgnoreCase),
            "Sap recurrence prefix checkpoint is not the exact verified 1/154 prefix.");
        Require(
            rolloutManifest.SchemaVersion ==
                "acquisition_route_portfolio_rollout_proof_manifest.v1" &&
            !rolloutManifest.FormalTrainingAuthorized,
            "Sap acquisition rollout proof manifest is invalid.");
        Require(
            rolloutReceipt.SchemaVersion ==
                "acquisition_route_portfolio_rollout_proof_receipt.v1" &&
            rolloutReceipt.Status == "verified_complete_rollout_proof_chain" &&
            rolloutReceipt.ProofChainVerified &&
            rolloutReceipt.PortfolioCompletionVerified &&
            !rolloutReceipt.FormalTrainingAuthorized &&
            string.Equals(
                rolloutReceipt.ManifestSha256,
                rolloutManifestSha256,
                StringComparison.OrdinalIgnoreCase),
            "Sap acquisition rollout receipt does not bind a verified proof manifest.");

        var routeOccurrenceId =
            rolloutManifest.InitialCheckpointProof.ExecutionInputs
                .RouteOccurrenceId;
        Require(
            !string.IsNullOrWhiteSpace(routeOccurrenceId),
            "Sap acquisition rollout proof has no route occurrence identity.");
        Require(
            string.Equals(
                Path.GetFullPath(iteration.AcquisitionRolloutProofManifestPath),
                Path.GetFullPath(rolloutManifestPath),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFullPath(iteration.AcquisitionRolloutProofReceiptPath),
                Path.GetFullPath(rolloutReceiptPath),
                StringComparison.OrdinalIgnoreCase),
            "Sap recurrence iteration does not bind the exported rollout proof.");

        return new FullShipmentSapEvidenceExport
        {
            RunId = rolloutManifest.InitialCheckpointProof.RunId,
            RequirementId = SapRequirementId,
            QualifiedItemId = SapQualifiedItemId,
            AcquisitionRouteOccurrenceId = routeOccurrenceId,
            VerifiedIterationCount = checkpoint.VerifiedIterationCount,
            RemainingItemCount = checkpoint.RemainingItemCount,
            FinalTotalDay = checkpoint.FinalTotalDay,
            ReadyForNextIteration = checkpoint.ReadyForNextIteration,
            RecurrenceManifestPath = Path.GetFullPath(recurrenceManifestPath),
            RecurrenceManifestSha256 = recurrenceManifestSha256,
            PrefixCheckpointPath = Path.GetFullPath(prefixCheckpointPath),
            PrefixCheckpointSha256 = prefixCheckpointSha256,
            AcquisitionRolloutProofManifestPath =
                Path.GetFullPath(rolloutManifestPath),
            AcquisitionRolloutProofManifestSha256 = rolloutManifestSha256,
            AcquisitionRolloutReceiptPath =
                Path.GetFullPath(rolloutReceiptPath),
            AcquisitionRolloutReceiptSha256 = rolloutReceiptSha256,
            EvidenceVerified = true,
            FormalTrainingAuthorized = false
        };
    }

    private static FullShipmentRecurrenceIterationProof SingleSapIteration(
        FullShipmentRecurrenceProofManifest manifest)
    {
        var iterations = manifest.Iterations ??
            Array.Empty<FullShipmentRecurrenceIterationProof>();
        Require(
            iterations.Length == 1 &&
            iterations[0] is not null &&
            iterations[0].RequirementId == SapRequirementId &&
            iterations[0].QualifiedItemId == SapQualifiedItemId,
            "Sap evidence export requires exactly the Sap 1/154 recurrence iteration.");
        return iterations[0];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
