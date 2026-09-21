using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioRolloutCheckpointBuilder
{
    public static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildInitialContinuation(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string priorCheckpointPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs currentInputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            string settlementReceiptPath) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    priorCheckpointPath),
                continuationRequestPath,
                currentInputs,
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

    public static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildContinuation(
            string rolloutProofManifestPath,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs currentInputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            string settlementReceiptPath) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                    rolloutProofManifestPath),
                continuationRequestPath,
                currentInputs,
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

    internal static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildContinuation(
            AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
            string continuationRequestPath,
            AcquisitionRouteExecutionBindingInputs currentInputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            string settlementReceiptPath)
    {
        var checkpointFullPath = verifiedPrior.CheckpointPath;
        var prior = verifiedPrior.Checkpoint;
        Require(!prior.PortfolioCompletionVerified &&
                prior.FreshReplanRequired &&
                !prior.FormalTrainingAuthorized,
            "Prior rollout checkpoint is not an exact incomplete artifact.");
        var portfolioInputs = AcquisitionRouteExecutionBindingBuilder
            .PortfolioInputs(currentInputs);
        var expectedRequest = AcquisitionRoutePortfolioContinuationBuilder
            .BuildRequest(verifiedPrior, portfolioInputs);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioContinuationTeacherRequest>(
            Path.GetFullPath(continuationRequestPath),
            "Acquisition route portfolio continuation Teacher request");
        Require(EqualJson(request, expectedRequest) &&
                request.TransitionCount == prior.TransitionCount + 1,
            "Continuation Teacher request is not verified.");
        var expectedSettlement = AcquisitionRoutePortfolioSettlementBuilder
            .BuildContinuationReceipt(
                verifiedPrior,
                continuationRequestPath,
                currentInputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath);
        var settlement = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSettlementReceipt>(
            Path.GetFullPath(settlementReceiptPath),
            "Continuation route portfolio settlement receipt");
        Require(EqualJson(settlement, expectedSettlement) &&
                settlement.ReservationLifecycleVerified &&
                settlement.FreshReplanRequired &&
                !settlement.FormalTrainingAuthorized,
            "Continuation rollout settlement receipt is not verified.");
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            Path.GetFullPath(currentInputs.PortfolioTeacherPreferencePath),
            "Continuation route portfolio Teacher preference");
        var expectedPreference =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildContinuation(
                    verifiedPrior,
                    portfolioInputs,
                    continuationRequestPath);
        Require(EqualJson(preference, expectedPreference) &&
                preference.TeacherPreferenceLabelEligible &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                !preference.FormalTrainingAuthorized,
            "Continuation rollout Teacher preference is not verified.");

        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            Path.GetFullPath(currentInputs.TargetDateOpportunityCostPath),
            "Continuation target-date opportunity cost");
        var matchingRoutes = opportunity.Routes.Where(candidate =>
                candidate.RouteOccurrenceId == settlement.RouteOccurrenceId)
            .ToArray();
        Require(matchingRoutes.Length == 1,
            "Settled continuation route is not present exactly once.");
        var route = matchingRoutes[0];
        var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(
            route);
        var proposal = preference.SelectedProposal ??
            throw new InvalidDataException(
                "Continuation rollout Teacher proposal is missing.");
        Require(proposal.SelectedRouteOccurrenceIds.Count(value =>
                    value == route.RouteOccurrenceId) == 1 &&
                proposal.PriorRolloutCheckpointSha256 ==
                    request.PriorCheckpointSha256 &&
                settlement.GoalId == prior.GoalId &&
                settlement.GoalId == preference.GoalId &&
                settlement.PortfolioId ==
                    "target-date-acquisition-portfolio:" +
                    proposal.ProposalId &&
                settlement.AfterStateHash.Length > 0,
            "Settled continuation route is not owned by its Teacher portfolio.");

        var progress = AdvanceProgress(prior.ScopedProgress, requirement);
        var selected = proposal.SelectedRouteOccurrenceIds
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(!prior.CompletedRouteOccurrenceIds.Contains(
                route.RouteOccurrenceId,
                StringComparer.Ordinal),
            "Continuation route was already completed.");
        var completed = prior.CompletedRouteOccurrenceIds
            .Append(route.RouteOccurrenceId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var pending = selected.Except(completed, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        using var afterDocument = JsonDocument.Parse(
            File.ReadAllText(Path.GetFullPath(afterSnapshotPath)));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            Path.GetFullPath(settledLedgerPath),
            afterDocument.RootElement).Ledger;
        Require(ledger.Revision == settlement.SettledLedgerRevision &&
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settledLedgerPath)) ==
                settlement.SettledLedgerSha256,
            "Continuation rollout settled ledger drifted.");
        var rolloutRouteIds = prior.SelectedRouteOccurrenceIds
            .Concat(selected)
            .Concat(completed)
            .Distinct(StringComparer.Ordinal)
            .Select(value =>
                AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix + value)
            .ToHashSet(StringComparer.Ordinal);
        var activeClaimCount = ledger.MaterialReservations.Count(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                rolloutRouteIds.Contains(row.SourceDecisionId)) +
            ledger.CurrencyReservations.Count(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                rolloutRouteIds.Contains(row.SourceDecisionId));
        var complete = progress.All(scope => scope.ScopeComplete) &&
            pending.Length == 0 && activeClaimCount == 0;
        return new AcquisitionRoutePortfolioRolloutCheckpoint
        {
            Status = complete
                ? "verified_cumulative_portfolio_completion"
                : "verified_continuation_transition_fresh_replan_required",
            RolloutId = prior.RolloutId,
            GoalId = prior.GoalId,
            RootPreferenceRequestSha256 =
                prior.RootPreferenceRequestSha256,
            CurrentTeacherPreferenceSha256 =
                CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                    currentInputs.PortfolioTeacherPreferencePath)),
            CurrentProposalId = proposal.ProposalId,
            CurrentPortfolioId = settlement.PortfolioId,
            LatestSettlementReceiptSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settlementReceiptPath)),
            LatestStateHash = settlement.AfterStateHash,
            LatestLedgerRevision = settlement.SettledLedgerRevision,
            LatestLedgerSha256 = settlement.SettledLedgerSha256,
            TransitionCount = request.TransitionCount,
            PriorCheckpointSha256 = request.PriorCheckpointSha256,
            ScopedProgress = progress,
            SelectedRouteOccurrenceIds = selected,
            CompletedRouteOccurrenceIds = completed,
            PendingSelectedRouteOccurrenceIds = pending,
            CheckpointVerified = true,
            PortfolioCompletionVerified = complete,
            FreshReplanRequired = !complete,
            FormalTrainingAuthorized = false,
            BlockingReasons = Array.Empty<string>()
        };
    }

    private static AcquisitionRoutePortfolioScopeProgress[] AdvanceProgress(
        AcquisitionRoutePortfolioScopeProgress[] prior,
        AcquisitionRouteTargetDateUnlock completed)
    {
        AcquisitionRoutePortfolioContinuationBuilder.ValidateProgress(prior);
        var matched = false;
        var progress = prior.Select(row =>
        {
            if (row.RequirementSetId != completed.RequirementSetId ||
                row.RequirementId != completed.RequirementId)
            {
                return CloneProgress(row);
            }
            Require(!row.CompletedAlternativeIndices.Contains(
                    completed.AlternativeIndex),
                "Continuation alternative was already completed.");
            matched = true;
            var indices = row.CompletedAlternativeIndices
                .Append(completed.AlternativeIndex)
                .Distinct()
                .Order()
                .ToArray();
            var remaining = Math.Max(
                0,
                row.RequiredAlternativeCount - indices.Length);
            return new AcquisitionRoutePortfolioScopeProgress(
                row.RequirementSetId,
                row.RequirementId,
                row.SelectionRule,
                row.RequiredAlternativeCount,
                row.AlternativeCount,
                indices,
                remaining,
                remaining == 0);
        }).ToArray();
        Require(matched,
            "Continuation route is outside prior rollout scope.");
        AcquisitionRoutePortfolioContinuationBuilder.ValidateProgress(
            progress);
        return progress;
    }

    private static AcquisitionRoutePortfolioScopeProgress CloneProgress(
        AcquisitionRoutePortfolioScopeProgress row) => new(
        row.RequirementSetId,
        row.RequirementId,
        row.SelectionRule,
        row.RequiredAlternativeCount,
        row.AlternativeCount,
        row.CompletedAlternativeIndices.ToArray(),
        row.RemainingRequiredSlots,
        row.ScopeComplete);
}
