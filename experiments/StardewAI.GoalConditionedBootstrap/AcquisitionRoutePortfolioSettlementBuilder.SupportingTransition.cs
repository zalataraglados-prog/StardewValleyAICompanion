using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    public static ReservationPortfolioRouteSettlementRequest
        BuildAfterSupportingTransitionRequest(
            AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
            AcquisitionRouteSupportingTransitionSettlementProof proof,
            string replanAdmissionPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion)
    {
        var expectedBinding = AcquisitionRouteExecutionBindingBuilder
            .BuildAfterSupportingTransition(
                priorInputs,
                proof,
                replanAdmissionPath,
                inputs);
        var expectedFresh = AcquisitionRouteFreshTerminalReceiptBuilder
            .BuildAfterSupportingTransition(
                priorInputs,
                proof,
                replanAdmissionPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion);
        return BuildRequestCore(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            null,
            string.Empty,
            expectedBinding,
            expectedFresh);
    }

    public static AcquisitionRoutePortfolioSettlementReceipt
        BuildAfterSupportingTransitionReceipt(
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
            string settledLedgerPath)
    {
        var expectedBinding = AcquisitionRouteExecutionBindingBuilder
            .BuildAfterSupportingTransition(
                priorInputs,
                proof,
                replanAdmissionPath,
                inputs);
        var expectedFresh = AcquisitionRouteFreshTerminalReceiptBuilder
            .BuildAfterSupportingTransition(
                priorInputs,
                proof,
                replanAdmissionPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                executorVersion);
        return BuildReceiptCore(
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
            null,
            string.Empty,
            expectedBinding,
            expectedFresh);
    }
}
