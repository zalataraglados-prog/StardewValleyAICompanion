namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteFreshTerminalReceiptBuilder
{
    public static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildAfterSupportingTransition(
            AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
            AcquisitionRouteSupportingTransitionSettlementProof proof,
            string replanAdmissionPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion) => BuildVerifiedBinding(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion,
                AcquisitionRouteExecutionBindingBuilder
                    .BuildAfterSupportingTransition(
                        priorInputs,
                        proof,
                        replanAdmissionPath,
                        inputs));

    public static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildInitialContinuation(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                continuationRequestPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion);

    public static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildContinuation(
            string rolloutProofManifestPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                continuationRequestPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion);

    internal static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildContinuation(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion) => BuildVerifiedBinding(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion,
                AcquisitionRouteExecutionBindingBuilder
                    .BuildContinuation(
                        verifiedPrior,
                        continuationRequestPath,
                        inputs));
}
