using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioContinuationCompletion(
        AcquisitionRoutePortfolioInitialCheckpointProof proof,
        string checkpointPath,
        string requestPath,
        AcquisitionRouteExecutionBindingInputs bindingInputs,
        string bindingPath,
        string outputRoot)
    {
        var checkpoint = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutCheckpoint>(
            checkpointPath,
            "Continuation prior checkpoint");
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioContinuationTeacherRequest>(
            requestPath,
            "Continuation Teacher request");
        var commitReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioCommitReceipt>(
            bindingInputs.PortfolioCommitReceiptPath,
            "Continuation portfolio commit receipt");
        var committedLedger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            bindingInputs.CommittedStrategyLedgerPath,
            "Continuation committed strategy ledger");
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            bindingInputs.TargetDateOpportunityCostPath,
            "Continuation opportunity cost");
        var route = opportunity.Routes.Single(value =>
            value.RouteOccurrenceId == bindingInputs.RouteOccurrenceId);
        var requirement = TargetDateRequirementRoute(route);
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            bindingInputs.ActionQueuePath,
            "Continuation action queue");
        var queueItem = queue.Items.Single();
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            bindingInputs.BeforeSnapshotPath,
            "Continuation before snapshot");
        var candidateId = AcquisitionRouteExecutionBindingBuilder
            .SelectedCandidateId(route.RouteOccurrenceId);

        var afterStateHash = "target-date-continuation-after-state";
        var afterSnapshotPath = Path.Combine(outputRoot, "after-snapshot.json");
        WriteTargetDateInventoryAfterSnapshot(
            bindingInputs.BeforeSnapshotPath,
            afterSnapshotPath,
            afterStateHash,
            requirement.QualifiedItemId,
            requirement.RequiredAmount,
            requirement.MinimumQuality);
        const string runId = "run.target-date-continuation";
        var executionReceiptPath = Path.Combine(
            outputRoot,
            "execution-receipt.json");
        Write(
            executionReceiptPath,
            TargetDateQueueReceipt(
                queue,
                queueItem,
                before,
                afterStateHash,
                before.GameTick + 1,
                candidateId,
                runId));
        var freshPath = Path.Combine(outputRoot, "fresh-terminal-receipt.json");
        var fresh = AcquisitionRouteFreshTerminalReceiptBuilder
            .BuildInitialContinuation(
                proof,
                checkpointPath,
                requestPath,
                bindingInputs,
                bindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                runId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Write(freshPath, fresh);
        Require(fresh.FreshTerminalReceiptVerified &&
                fresh.RouteTrainingEvidenceEligible &&
                !fresh.FormalTrainingAuthorized,
            "Continuation fresh terminal receipt drifted.");

        var settlementRequest = AcquisitionRoutePortfolioSettlementBuilder
            .BuildInitialContinuationRequest(
                proof,
                checkpointPath,
                requestPath,
                bindingInputs,
                bindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshPath,
                runId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        var settlementRequestPath = Path.Combine(
            outputRoot,
            "settlement-request.json");
        Write(settlementRequestPath, settlementRequest);
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterSnapshotPath,
            "Continuation after snapshot");
        var settlement = new ReservationPortfolioLedgerService()
            .SettleCompletedRoute(
                committedLedger,
                after,
                settlementRequest,
                "2026-09-21T00:04:00Z");
        Require(settlement.Accepted && settlement.Ledger is not null,
            "Continuation route reservation settlement was rejected.");
        var settlementResultPath = Path.Combine(
            outputRoot,
            "settlement-result.json");
        var settledLedgerPath = Path.Combine(
            outputRoot,
            "settled-ledger.json");
        Write(settlementResultPath, settlement);
        Write(settledLedgerPath, settlement.Ledger!);
        var settlementReceipt = AcquisitionRoutePortfolioSettlementBuilder
            .BuildInitialContinuationReceipt(
                proof,
                checkpointPath,
                requestPath,
                bindingInputs,
                bindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshPath,
                runId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath);
        var settlementReceiptPath = Path.Combine(
            outputRoot,
            "settlement-receipt.json");
        Write(settlementReceiptPath, settlementReceipt);
        Require(settlementReceipt.ReservationLifecycleVerified &&
                settlementReceipt.CompletedReservationIds.Length == 0 &&
                settlementReceipt.SettledLedgerRevision ==
                    commitReceipt.CommittedLedgerRevision + 1 &&
                !settlementReceipt.FormalTrainingAuthorized,
            "Continuation route settlement receipt drifted.");

        var cumulative = AcquisitionRoutePortfolioRolloutCheckpointBuilder
            .BuildInitialContinuation(
                proof,
                checkpointPath,
                requestPath,
                bindingInputs,
                bindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshPath,
                runId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath,
                settlementReceiptPath);
        Write(Path.Combine(outputRoot, "rollout-checkpoint.json"), cumulative);
        var expectedCompleted = checkpoint.CompletedRouteOccurrenceIds
            .Append(route.RouteOccurrenceId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(cumulative.Status ==
                    "verified_cumulative_portfolio_completion" &&
                cumulative.CheckpointVerified &&
                cumulative.PortfolioCompletionVerified &&
                !cumulative.FreshReplanRequired &&
                !cumulative.FormalTrainingAuthorized &&
                cumulative.TransitionCount == 2 &&
                cumulative.PriorCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                cumulative.CompletedRouteOccurrenceIds.SequenceEqual(
                    expectedCompleted,
                    StringComparer.Ordinal) &&
                cumulative.ScopedProgress.All(row => row.ScopeComplete) &&
                cumulative.PendingSelectedRouteOccurrenceIds.Length == 0 &&
                cumulative.BlockingReasons.Length == 0,
            "Cumulative two-route rollout checkpoint drifted.");
    }
}
