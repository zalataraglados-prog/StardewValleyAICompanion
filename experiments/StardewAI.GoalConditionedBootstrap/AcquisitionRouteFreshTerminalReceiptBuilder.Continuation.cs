namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteFreshTerminalReceiptBuilder
{
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
            string executorVersion) => BuildVerifiedBinding(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion,
                AcquisitionRouteExecutionBindingBuilder
                    .BuildInitialContinuation(
                        proof,
                        checkpointPath,
                        continuationRequestPath,
                        inputs));
}
