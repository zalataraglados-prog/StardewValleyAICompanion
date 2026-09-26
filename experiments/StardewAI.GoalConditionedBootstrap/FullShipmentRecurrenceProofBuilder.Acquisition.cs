using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private static VerifiedAcquisition VerifyAcquisition(
        FullShipmentRecurrenceIterationProof proof,
        SnapshotEnvelope anchorSnapshot,
        string anchorSnapshotPath,
        string inventorySha,
        string loweringSha,
        IReadOnlyCollection<string> requiredIds)
    {
        var manifestPath = Path.GetFullPath(
            proof.AcquisitionRolloutProofManifestPath);
        var actualPath = Path.GetFullPath(
            proof.AcquisitionRolloutProofReceiptPath);
        var acquisitionManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            manifestPath,
            "Full Shipment acquisition rollout proof manifest");
        var initialProof = acquisitionManifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Full Shipment acquisition rollout has no initial proof.");
        var transitions = acquisitionManifest.ContinuationTransitions ??
            throw new InvalidDataException(
                "Full Shipment acquisition rollout has no transition list.");
        var initialInputs = initialProof.ExecutionInputs ??
            throw new InvalidDataException(
                "Full Shipment acquisition rollout has null initial inputs.");
        var continuationInputs = transitions.Select(transition =>
                transition?.ExecutionInputs ??
                throw new InvalidDataException(
                    "Full Shipment acquisition rollout has null continuation inputs."))
            .ToArray();
        var allInputs = new[]
            {
                initialInputs
            }
            .Concat(continuationInputs)
            .ToArray();
        Require(
            allInputs.Length > 0 &&
            allInputs.All(inputs =>
                CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                    inputs.RequirementInventoryPath)) == inventorySha &&
                CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                    inputs.AcquisitionLoweringPath)) == loweringSha),
            "Full Shipment acquisition rollout uses different authority inputs.");
        var rootBeforePath = Path.GetFullPath(
            initialInputs.BeforeSnapshotPath);
        Require(
            CurrentTeacherFrontierSupport.HashFile(rootBeforePath) ==
                CurrentTeacherFrontierSupport.HashFile(anchorSnapshotPath),
            "Full Shipment acquisition rollout does not start at the prior settlement artifact.");

        var expected = AcquisitionRoutePortfolioRolloutProofBuilder.BuildReceipt(
            manifestPath);
        var actual = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            actualPath,
            "Full Shipment acquisition rollout proof receipt");
        Require(
            EqualJson(actual, expected) &&
            actual.ProofChainVerified &&
            actual.PortfolioCompletionVerified &&
            !actual.FormalTrainingAuthorized,
            "Full Shipment acquisition rollout proof is not complete and exact.");
        var latest = AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
            manifestPath);
        Require(
            latest.Checkpoint.ScopedProgress.Length == 1 &&
            latest.Checkpoint.ScopedProgress[0].RequirementSetId ==
                "full_shipment" &&
            latest.Checkpoint.ScopedProgress[0].RequirementId ==
                proof.RequirementId &&
            latest.Checkpoint.ScopedProgress[0].ScopeComplete &&
            latest.Checkpoint.ScopedProgress[0].CompletedAlternativeIndices
                .SequenceEqual(new[] { 0 }),
            "Full Shipment acquisition rollout completed the wrong requirement scope.");

        var afterPath = Path.GetFullPath(proof.AcquisitionAfterSnapshotPath);
        var after = ReadSnapshot(afterPath, "acquisition after snapshot");
        RequireSameActor(anchorSnapshot, after);
        Require(
            after.StateHash == actual.LatestStateHash &&
            EquivalentProgress(
                FullShipmentSettlementVerifier.Project(
                    requiredIds,
                    anchorSnapshot),
                FullShipmentSettlementVerifier.Project(requiredIds, after)) &&
            ExactInventoryReceiptVerifier.InventoryCount(
                after,
                proof.QualifiedItemId,
                0) >= 1,
            "Full Shipment acquisition did not preserve progress and produce one exact item.");
        return new VerifiedAcquisition(
            afterPath,
            after,
            CurrentTeacherFrontierSupport.HashFile(actualPath));
    }
}
