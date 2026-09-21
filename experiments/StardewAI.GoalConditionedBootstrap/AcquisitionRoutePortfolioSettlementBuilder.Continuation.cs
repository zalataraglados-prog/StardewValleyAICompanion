using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    public static ReservationPortfolioRouteSettlementRequest
        BuildInitialContinuationRequest(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion) => BuildRequestCore(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                ContinuationProof(
                    proof,
                    checkpointPath,
                    continuationRequestPath));

    public static AcquisitionRoutePortfolioSettlementReceipt
        BuildInitialContinuationReceipt(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath) => BuildReceiptCore(
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
                ContinuationProof(
                    proof,
                    checkpointPath,
                    continuationRequestPath));

    private static InitialContinuationProof ContinuationProof(
        AcquisitionRoutePortfolioInitialCheckpointProof proof,
        string checkpointPath,
        string continuationRequestPath) => new(
            proof,
            checkpointPath,
            continuationRequestPath);
}
