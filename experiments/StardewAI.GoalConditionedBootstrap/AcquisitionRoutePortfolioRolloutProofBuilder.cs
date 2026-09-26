using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioRolloutProofBuilder
{
    public static AcquisitionRoutePortfolioRolloutProofReceipt BuildReceipt(
        string manifestPath)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        var manifest = ReadManifest(manifestFullPath);
        var latest = Verify(manifest, manifestFullPath);
        return new AcquisitionRoutePortfolioRolloutProofReceipt
        {
            Status = latest.Checkpoint.PortfolioCompletionVerified
                ? "verified_complete_rollout_proof_chain"
                : "verified_incomplete_rollout_proof_chain",
            RolloutId = latest.Checkpoint.RolloutId,
            GoalId = latest.Checkpoint.GoalId,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    latest.Checkpoint.CommunityCenterProvenance),
            ManifestSha256 = CurrentTeacherFrontierSupport.HashFile(
                manifestFullPath),
            LatestCheckpointSha256 = CurrentTeacherFrontierSupport.HashFile(
                latest.CheckpointPath),
            TransitionCount = latest.Checkpoint.TransitionCount,
            ContinuationTransitionCount =
                manifest.ContinuationTransitions.Length,
            LatestStateHash = latest.Checkpoint.LatestStateHash,
            LatestLedgerRevision = latest.Checkpoint.LatestLedgerRevision,
            LatestLedgerSha256 = latest.Checkpoint.LatestLedgerSha256,
            PortfolioCompletionVerified =
                latest.Checkpoint.PortfolioCompletionVerified,
            ProofChainVerified = true,
            FormalTrainingAuthorized = false
        };
    }

    internal static AcquisitionRoutePortfolioVerifiedCheckpoint
        VerifyInitial(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath)
    {
        var checkpointFullPath = Path.GetFullPath(checkpointPath);
        var actual = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutCheckpoint>(
            checkpointFullPath,
            "Acquisition route portfolio rollout checkpoint");
        var expected = AcquisitionRoutePortfolioRolloutCheckpointBuilder
            .BuildInitial(
                proof.ExecutionInputs,
                proof.ExecutionBindingPath,
                proof.ExecutionReceiptPath,
                proof.AfterSnapshotPath,
                proof.FreshTerminalReceiptPath,
                proof.RunId,
                proof.ExecutorVersion,
                proof.SettlementRequestPath,
                proof.SettlementResultPath,
                proof.SettledLedgerPath,
                proof.SettlementReceiptPath);
        Require(EqualJson(actual, expected) &&
                actual.CheckpointVerified &&
                !actual.FormalTrainingAuthorized,
            "Initial rollout checkpoint is not an exact verified artifact.");
        return new AcquisitionRoutePortfolioVerifiedCheckpoint(
            checkpointFullPath,
            actual);
    }

    internal static AcquisitionRoutePortfolioVerifiedCheckpoint Verify(
        string manifestPath)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        return Verify(ReadManifest(manifestFullPath), manifestFullPath);
    }

    private static AcquisitionRoutePortfolioVerifiedCheckpoint Verify(
        AcquisitionRoutePortfolioRolloutProofManifest manifest,
        string manifestPath)
    {
        var initialProof = manifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Rollout proof manifest has no initial proof: " +
                manifestPath);
        var transitions = manifest.ContinuationTransitions ??
            throw new InvalidDataException(
                "Rollout proof manifest has no transition list: " +
                manifestPath);
        Require(manifest.SchemaVersion ==
                "acquisition_route_portfolio_rollout_proof_manifest.v1" &&
                !manifest.FormalTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(manifest.InitialCheckpointPath) &&
                transitions.All(row => row is not null),
            "Rollout proof manifest is invalid: " + manifestPath);
        var current = VerifyInitial(
            initialProof,
            manifest.InitialCheckpointPath);
        foreach (var transition in transitions)
        {
            Require(!current.Checkpoint.PortfolioCompletionVerified &&
                    current.Checkpoint.FreshReplanRequired,
                "Completed rollout checkpoint has a continuation transition.");
            var expected = AcquisitionRoutePortfolioRolloutCheckpointBuilder
                .BuildContinuation(
                    current,
                    transition.ContinuationRequestPath,
                    transition.ExecutionInputs,
                    transition.ExecutionBindingPath,
                    transition.ExecutionReceiptPath,
                    transition.AfterSnapshotPath,
                    transition.FreshTerminalReceiptPath,
                    transition.RunId,
                    transition.ExecutorVersion,
                    transition.SettlementRequestPath,
                    transition.SettlementResultPath,
                    transition.SettledLedgerPath,
                    transition.SettlementReceiptPath);
            var checkpointPath = Path.GetFullPath(transition.CheckpointPath);
            var actual = CurrentTeacherFrontierSupport.Read<
                AcquisitionRoutePortfolioRolloutCheckpoint>(
                checkpointPath,
                "Continuation rollout checkpoint");
            Require(EqualJson(actual, expected) &&
                    actual.CheckpointVerified &&
                    !actual.FormalTrainingAuthorized &&
                    actual.TransitionCount ==
                        current.Checkpoint.TransitionCount + 1 &&
                    actual.PriorCheckpointSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(
                            current.CheckpointPath) &&
                    actual.RolloutId == current.Checkpoint.RolloutId &&
                    actual.RootPreferenceRequestSha256 ==
                        current.Checkpoint.RootPreferenceRequestSha256,
                "Continuation rollout checkpoint is not an exact next transition.");
            current = new AcquisitionRoutePortfolioVerifiedCheckpoint(
                checkpointPath,
                actual);
        }
        return current;
    }

    private static AcquisitionRoutePortfolioRolloutProofManifest ReadManifest(
        string path) => CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            path,
            "Acquisition route portfolio rollout proof manifest");


}
