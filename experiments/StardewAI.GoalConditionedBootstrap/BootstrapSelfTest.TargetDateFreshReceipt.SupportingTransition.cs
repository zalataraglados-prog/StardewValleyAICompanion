namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static AcquisitionRouteExecutionBinding
        BuildTargetDateExecutionBinding(
            AcquisitionRouteExecutionBindingInputs inputs,
            AcquisitionRoutePortfolioSupportingTransitionInitialProof?
                supportingTransition) => supportingTransition is null
            ? AcquisitionRouteExecutionBindingBuilder.Build(inputs)
            : AcquisitionRouteExecutionBindingBuilder
                .BuildAfterSupportingTransition(
                    supportingTransition.RequestInputs,
                    supportingTransition.SettlementProof,
                    supportingTransition.ReplanAdmissionPath,
                    inputs);

    private static AcquisitionRouteFreshTerminalReceiptAdmission
        BuildTargetDateFreshTerminalReceipt(
            AcquisitionRoutePortfolioSupportingTransitionInitialProof?
                supportingTransition,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string runId,
            string executorVersion) => supportingTransition is null
            ? AcquisitionRouteFreshTerminalReceiptBuilder.Build(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion)
            : AcquisitionRouteFreshTerminalReceiptBuilder
                .BuildAfterSupportingTransition(
                    supportingTransition.RequestInputs,
                    supportingTransition.SettlementProof,
                    supportingTransition.ReplanAdmissionPath,
                    inputs,
                    executionBindingPath,
                    executionReceiptPath,
                    afterSnapshotPath,
                    runId,
                    executorVersion);

    private static StardewAI.Contracts.Strategy
        .ReservationPortfolioRouteSettlementRequest
        BuildTargetDateSettlementRequest(
            AcquisitionRoutePortfolioSupportingTransitionInitialProof?
                supportingTransition,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion) => supportingTransition is null
            ? AcquisitionRoutePortfolioSettlementBuilder.BuildRequest(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion)
            : AcquisitionRoutePortfolioSettlementBuilder
                .BuildAfterSupportingTransitionRequest(
                    supportingTransition.RequestInputs,
                    supportingTransition.SettlementProof,
                    supportingTransition.ReplanAdmissionPath,
                    inputs,
                    executionBindingPath,
                    executionReceiptPath,
                    afterSnapshotPath,
                    freshTerminalReceiptPath,
                    runId,
                    executorVersion);

    private static AcquisitionRoutePortfolioSettlementReceipt
        BuildTargetDateSettlementReceipt(
            AcquisitionRoutePortfolioSupportingTransitionInitialProof?
                supportingTransition,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath) => supportingTransition is null
            ? AcquisitionRoutePortfolioSettlementBuilder.BuildReceipt(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath)
            : AcquisitionRoutePortfolioSettlementBuilder
                .BuildAfterSupportingTransitionReceipt(
                    supportingTransition.RequestInputs,
                    supportingTransition.SettlementProof,
                    supportingTransition.ReplanAdmissionPath,
                    inputs,
                    executionBindingPath,
                    executionReceiptPath,
                    afterSnapshotPath,
                    freshTerminalReceiptPath,
                    runId,
                    executorVersion,
                    settlementRequestPath,
                    settlementResultPath,
                    settledLedgerPath);

    private static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildTargetDateRolloutCheckpoint(
            AcquisitionRoutePortfolioSupportingTransitionInitialProof?
                supportingTransition,
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
            string settlementReceiptPath) => supportingTransition is null
            ? AcquisitionRoutePortfolioRolloutCheckpointBuilder.BuildInitial(
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
                settlementReceiptPath)
            : AcquisitionRoutePortfolioRolloutCheckpointBuilder
                .BuildAfterSupportingTransition(
                    supportingTransition.RequestInputs,
                    supportingTransition.SettlementProof,
                    supportingTransition.ReplanAdmissionPath,
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
                    settlementReceiptPath);

    private sealed record TargetDateTerminalProofArtifacts(
        string ExecutionBindingPath,
        string ExecutionReceiptPath,
        string AfterSnapshotPath,
        string FreshTerminalReceiptPath,
        string RunId,
        string SettlementRequestPath,
        string SettlementResultPath,
        string SettledLedgerPath,
        string SettlementReceiptPath,
        string RolloutCheckpointPath);
}
