using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRoutePortfolioRolloutProofBuilder
{
    public static AcquisitionRoutePortfolioRolloutProofReceipt BuildReceipt(
        string manifestPath)
        => BuildVerifiedReceipt(manifestPath).Receipt;

    internal static (
        AcquisitionRoutePortfolioRolloutProofReceipt Receipt,
        AcquisitionRoutePortfolioVerifiedCheckpoint Latest)
        BuildVerifiedReceipt(string manifestPath)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        var manifest = ReadManifest(manifestFullPath);
        var identity = BuildIdentity(manifestFullPath, manifest);
        const string producer =
            "AcquisitionRoutePortfolioRolloutProofBuilder.BuildVerifiedReceipt";
        const string producerVersion = "v2";
        var certificate = InvocationDeterministicBuildSession.IsReuseEnabled
            ? InvocationDeterministicBuildSession.GetOrBuild(
                producer,
                producerVersion,
                identity,
                () => BuildCertificate(manifest, manifestFullPath),
                "rollout_proof_certificate")
            : InvocationDeterministicBuildSession.Observe(
                "rollout_proof_certificate",
                producer,
                producerVersion,
                identity,
                () => BuildCertificate(manifest, manifestFullPath),
                result => result.Receipt);
        var latestPath = Path.GetFullPath(certificate.LatestCheckpointPath);
        var latestSha256 = CurrentTeacherFrontierSupport.HashFile(latestPath);
        Require(latestSha256 == certificate.LatestCheckpointSha256 &&
                latestSha256 == certificate.Receipt.LatestCheckpointSha256,
            "Verified rollout proof certificate checkpoint identity drifted.");
        var latest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutCheckpoint>(
            latestPath,
            "Verified rollout proof latest checkpoint");
        Require(latest.CheckpointVerified &&
                !latest.FormalTrainingAuthorized &&
                latest.RolloutId == certificate.Receipt.RolloutId &&
                latest.GoalId == certificate.Receipt.GoalId &&
                latest.TransitionCount == certificate.Receipt.TransitionCount &&
                latest.LatestStateHash ==
                    certificate.Receipt.LatestStateHash &&
                latest.LatestLedgerRevision ==
                    certificate.Receipt.LatestLedgerRevision &&
                latest.LatestLedgerSha256 ==
                    certificate.Receipt.LatestLedgerSha256 &&
                latest.PortfolioCompletionVerified ==
                    certificate.Receipt.PortfolioCompletionVerified,
            "Verified rollout proof certificate terminal checkpoint drifted.");
        return (
            certificate.Receipt,
            new AcquisitionRoutePortfolioVerifiedCheckpoint(
                latestPath,
                latest));
    }

    private static AcquisitionRoutePortfolioVerifiedRolloutProofCertificate
        BuildCertificate(
            AcquisitionRoutePortfolioRolloutProofManifest manifest,
            string manifestPath)
    {
        var latest = Verify(manifest, manifestPath);
        var latestSha256 = CurrentTeacherFrontierSupport.HashFile(
            latest.CheckpointPath);
        var receipt = new AcquisitionRoutePortfolioRolloutProofReceipt
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
                manifestPath),
            LatestCheckpointSha256 = latestSha256,
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
        return new AcquisitionRoutePortfolioVerifiedRolloutProofCertificate
        {
            Receipt = receipt,
            LatestCheckpointPath = latest.CheckpointPath,
            LatestCheckpointSha256 = latestSha256
        };
    }

    private static DeterministicBuildIdentityPart[] BuildIdentity(
        string manifestPath,
        AcquisitionRoutePortfolioRolloutProofManifest manifest)
    {
        var identity = new List<DeterministicBuildIdentityPart>
        {
            InvocationDeterministicBuildSession.File(
                "RolloutProofManifestPath",
                manifestPath)
        };
        var initial = manifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Rollout proof manifest has no initial proof: " +
                manifestPath);
        AddIdentity(
            identity,
            "Initial.ExecutionInputs",
            InvocationDeterministicBuildSession.Describe(
                initial.ExecutionInputs));
        AddIdentity(
            identity,
            "Initial",
            InvocationDeterministicBuildSession.Describe(new
            {
                initial.ExecutionBindingPath,
                initial.ExecutionReceiptPath,
                initial.AfterSnapshotPath,
                initial.FreshTerminalReceiptPath,
                initial.RunId,
                initial.ExecutorVersion,
                initial.SettlementRequestPath,
                initial.SettlementResultPath,
                initial.SettledLedgerPath,
                initial.SettlementReceiptPath,
                manifest.InitialCheckpointPath
            }));
        if (initial.SupportingTransition is { } supporting)
        {
            AddIdentity(
                identity,
                "Initial.Supporting.RequestInputs",
                InvocationDeterministicBuildSession.Describe(
                    supporting.RequestInputs));
            AddIdentity(
                identity,
                "Initial.Supporting.SettlementProof",
                InvocationDeterministicBuildSession.Describe(
                    supporting.SettlementProof));
            identity.Add(InvocationDeterministicBuildSession.File(
                "Initial.Supporting.ReplanAdmissionPath",
                supporting.ReplanAdmissionPath));
        }

        var transitions = manifest.ContinuationTransitions ??
            throw new InvalidDataException(
                "Rollout proof manifest has no transition list: " +
                manifestPath);
        for (var index = 0; index < transitions.Length; index++)
        {
            var transition = transitions[index] ??
                throw new InvalidDataException(
                    "Rollout proof manifest has a null transition: " +
                    manifestPath);
            var prefix = "Continuation[" + index + "]";
            AddIdentity(
                identity,
                prefix + ".ExecutionInputs",
                InvocationDeterministicBuildSession.Describe(
                    transition.ExecutionInputs));
            AddIdentity(
                identity,
                prefix,
                InvocationDeterministicBuildSession.Describe(new
                {
                    transition.ContinuationRequestPath,
                    transition.ExecutionBindingPath,
                    transition.ExecutionReceiptPath,
                    transition.AfterSnapshotPath,
                    transition.FreshTerminalReceiptPath,
                    transition.RunId,
                    transition.ExecutorVersion,
                    transition.SettlementRequestPath,
                    transition.SettlementResultPath,
                    transition.SettledLedgerPath,
                    transition.SettlementReceiptPath,
                    transition.CheckpointPath
                }));
        }
        return identity.ToArray();
    }

    private static void AddIdentity(
        ICollection<DeterministicBuildIdentityPart> destination,
        string prefix,
        IEnumerable<DeterministicBuildIdentityPart> source)
    {
        foreach (var part in source)
            destination.Add(part with { Name = prefix + "." + part.Name });
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
        var support = proof.SupportingTransition;
        var expected = support is null
            ? AcquisitionRoutePortfolioRolloutCheckpointBuilder.BuildInitial(
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
                proof.SettlementReceiptPath)
            : AcquisitionRoutePortfolioRolloutCheckpointBuilder
                .BuildAfterSupportingTransition(
                    support.RequestInputs,
                    support.SettlementProof,
                    support.ReplanAdmissionPath,
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
                !actual.FormalTrainingAuthorized &&
                (support is null
                    ? string.IsNullOrEmpty(
                        actual.PriorSupportingTransitionReplanSha256)
                    : actual.PriorSupportingTransitionReplanSha256 ==
                        CurrentTeacherFrontierSupport.HashFile(
                            Path.GetFullPath(
                                support.ReplanAdmissionPath))),
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

internal sealed class
    AcquisitionRoutePortfolioVerifiedRolloutProofCertificate
{
    public AcquisitionRoutePortfolioRolloutProofReceipt Receipt
    { get; set; } = new();

    public string LatestCheckpointPath { get; set; } = string.Empty;

    public string LatestCheckpointSha256 { get; set; } = string.Empty;
}
