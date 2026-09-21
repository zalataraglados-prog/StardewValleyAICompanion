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
            string executorVersion) => BuildContinuationRequest(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                continuationRequestPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion);

    public static ReservationPortfolioRouteSettlementRequest
        BuildContinuationRequest(
            string rolloutProofManifestPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion) => BuildContinuationRequest(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                continuationRequestPath,
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion);

    internal static ReservationPortfolioRouteSettlementRequest
        BuildContinuationRequest(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
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
                verifiedPrior,
                continuationRequestPath);

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
            string settledLedgerPath) => BuildContinuationReceipt(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                continuationRequestPath,
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

    public static AcquisitionRoutePortfolioSettlementReceipt
        BuildContinuationReceipt(
            string rolloutProofManifestPath,
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
            string settledLedgerPath) => BuildContinuationReceipt(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                continuationRequestPath,
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

    internal static AcquisitionRoutePortfolioSettlementReceipt
        BuildContinuationReceipt(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
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
                verifiedPrior,
                continuationRequestPath);
}
