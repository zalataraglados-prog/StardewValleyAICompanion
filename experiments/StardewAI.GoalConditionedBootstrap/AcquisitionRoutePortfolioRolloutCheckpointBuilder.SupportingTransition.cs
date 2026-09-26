namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioRolloutCheckpointBuilder
{
    public static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildAfterSupportingTransition(
            AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
            AcquisitionRouteSupportingTransitionSettlementProof proof,
            string replanAdmissionPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            string settlementReceiptPath) => BuildInitialCore(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath,
                settlementReceiptPath,
                AcquisitionRoutePortfolioSettlementBuilder
                    .BuildAfterSupportingTransitionReceipt(
                        priorInputs,
                        proof,
                        replanAdmissionPath,
                        inputs,
                        executionBindingPath,
                        executionReceiptPath,
                        afterSnapshotPath,
                        freshTerminalReceiptPath,
                        runId,
                        executorVersion,
                        settlementRequestPath,
                        settlementResultPath,
                        settledLedgerPath),
                AcquisitionRouteSupportingTransitionPortfolioBuilder
                    .BuildTeacherPreference(
                        priorInputs,
                        proof,
                        AcquisitionRouteExecutionBindingBuilder
                            .PortfolioInputs(inputs),
                        replanAdmissionPath,
                        inputs.PortfolioPreferenceRequestPath));
}
