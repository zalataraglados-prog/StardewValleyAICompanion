using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioContinuationCompletion(
        string priorManifestPath,
        string requestPath,
        AcquisitionRouteExecutionBindingInputs bindingInputs,
        string bindingPath,
        string outputRoot)
    {
        var priorVerified = AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
            priorManifestPath);
        var checkpoint = priorVerified.Checkpoint;
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

        var transitionId = request.TransitionCount.ToString(
            "D2",
            System.Globalization.CultureInfo.InvariantCulture);
        var afterStateHash =
            "target-date-continuation-after-state-" + transitionId;
        var afterSnapshotPath = Path.Combine(outputRoot, "after-snapshot.json");
        WriteTargetDateInventoryAfterSnapshot(
            bindingInputs.BeforeSnapshotPath,
            afterSnapshotPath,
            afterStateHash,
            requirement.QualifiedItemId,
            requirement.RequiredAmount,
            requirement.MinimumQuality);
        var runId = "run.target-date-continuation." + transitionId;
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
            .BuildContinuation(
                priorManifestPath,
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
            .BuildContinuationRequest(
                priorManifestPath,
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
            .BuildContinuationReceipt(
                priorManifestPath,
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
            .BuildContinuation(
                priorManifestPath,
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
        var cumulativePath = Path.Combine(
            outputRoot,
            "rollout-checkpoint.json");
        Write(cumulativePath, cumulative);
        var expectedCompleted = checkpoint.CompletedRouteOccurrenceIds
            .Append(route.RouteOccurrenceId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(cumulative.CheckpointVerified &&
                !cumulative.FormalTrainingAuthorized &&
                cumulative.TransitionCount == checkpoint.TransitionCount + 1 &&
                cumulative.PriorCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                cumulative.CompletedRouteOccurrenceIds.SequenceEqual(
                    expectedCompleted,
                    StringComparer.Ordinal) &&
                cumulative.BlockingReasons.Length == 0,
            "Cumulative rollout checkpoint drifted.");
        Require(cumulative.PortfolioCompletionVerified
                ? cumulative.Status ==
                    "verified_cumulative_portfolio_completion" &&
                  !cumulative.FreshReplanRequired &&
                  cumulative.ScopedProgress.All(row => row.ScopeComplete) &&
                  cumulative.PendingSelectedRouteOccurrenceIds.Length == 0
                : cumulative.Status ==
                    "verified_continuation_transition_fresh_replan_required" &&
                  cumulative.FreshReplanRequired &&
                  cumulative.ScopedProgress.Any(row => !row.ScopeComplete) &&
                  cumulative.PendingSelectedRouteOccurrenceIds.Length > 0,
            "Cumulative rollout completion disposition drifted.");

        var transition = new
            AcquisitionRoutePortfolioContinuationTransitionProof
            {
                ContinuationRequestPath = requestPath,
                ExecutionInputs = bindingInputs,
                ExecutionBindingPath = bindingPath,
                ExecutionReceiptPath = executionReceiptPath,
                AfterSnapshotPath = afterSnapshotPath,
                FreshTerminalReceiptPath = freshPath,
                RunId = runId,
                ExecutorVersion =
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                SettlementRequestPath = settlementRequestPath,
                SettlementResultPath = settlementResultPath,
                SettledLedgerPath = settledLedgerPath,
                SettlementReceiptPath = settlementReceiptPath,
                CheckpointPath = cumulativePath
            };
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            priorManifestPath,
            "Prior rollout proof manifest");
        manifest.ContinuationTransitions = manifest.ContinuationTransitions
            .Append(transition)
            .ToArray();
        var manifestPath = Path.Combine(
            outputRoot,
            "rollout-proof-manifest.json");
        Write(manifestPath, manifest);
        var proofReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestPath);
        var proofReceiptPath = Path.Combine(
            outputRoot,
            "rollout-proof-receipt.json");
        Write(proofReceiptPath, proofReceipt);
        Require(proofReceipt.ProofChainVerified &&
                proofReceipt.PortfolioCompletionVerified ==
                    cumulative.PortfolioCompletionVerified &&
                proofReceipt.TransitionCount == cumulative.TransitionCount &&
                proofReceipt.ContinuationTransitionCount ==
                    cumulative.TransitionCount - 1 &&
                proofReceipt.LatestCheckpointSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(cumulativePath) &&
                !proofReceipt.FormalTrainingAuthorized,
            "Cumulative rollout proof-chain receipt drifted.");
        if (!proofReceipt.PortfolioCompletionVerified)
        {
            VerifyTargetDatePortfolioContinuation(
                bindingInputs,
                manifestPath,
                afterSnapshotPath,
                settledLedgerPath);
            return;
        }
        Require(proofReceipt.TransitionCount >= 3,
            "Terminal rollout proof did not exercise three transitions.");
        var admission = AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
            manifestPath,
            proofReceiptPath);
        var admissionPath = Path.Combine(
            outputRoot,
            "rollout-admission-receipt.json");
        Write(admissionPath, admission);
        Require(admission.ControllerAdmissionGranted &&
                admission.TeacherTrainingEvidenceEligible &&
                !admission.FormalProductTrainingAuthorized &&
                admission.RolloutId == proofReceipt.RolloutId &&
                admission.GoalId == proofReceipt.GoalId &&
                admission.TransitionCount == proofReceipt.TransitionCount &&
                admission.ProofManifestSha256 ==
                    proofReceipt.ManifestSha256 &&
                admission.LatestCheckpointSha256 ==
                    proofReceipt.LatestCheckpointSha256 &&
                admission.BlockingReasons.Length == 0,
            "Completed rollout proof did not cross scoped controller admission.");

        VerifyTargetDatePortfolioSupervision(
            manifestPath,
            proofReceiptPath,
            admissionPath,
            outputRoot);

        var forgedProofReceiptPath = Path.Combine(
            outputRoot,
            "forged-rollout-proof-receipt.json");
        proofReceipt.TransitionCount++;
        Write(forgedProofReceiptPath, proofReceipt);
        proofReceipt.TransitionCount--;
        var forgedReceiptRejected = false;
        try
        {
            AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
                manifestPath,
                forgedProofReceiptPath);
        }
        catch (InvalidDataException)
        {
            forgedReceiptRejected = true;
        }
        Require(forgedReceiptRejected,
            "Caller-authored rollout proof receipt crossed controller admission.");

        var tamperedCheckpointPath = Path.Combine(
            outputRoot,
            "tampered-rollout-checkpoint.json");
        cumulative.TransitionCount++;
        Write(tamperedCheckpointPath, cumulative);
        cumulative.TransitionCount--;
        transition.CheckpointPath = tamperedCheckpointPath;
        var tamperedManifestPath = Path.Combine(
            outputRoot,
            "tampered-rollout-proof-manifest.json");
        Write(tamperedManifestPath, manifest);
        var rejected = false;
        try
        {
            AcquisitionRoutePortfolioRolloutProofBuilder.BuildReceipt(
                tamperedManifestPath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Tampered rollout checkpoint unexpectedly verified.");
    }
}
