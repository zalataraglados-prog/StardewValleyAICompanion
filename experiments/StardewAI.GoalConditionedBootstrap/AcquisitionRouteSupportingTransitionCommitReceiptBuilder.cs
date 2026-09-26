using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRouteSupportingTransitionCommitReceiptBuilder
{
    public static AcquisitionRouteSupportingTransitionCommitReceipt Build(
        AcquisitionRouteSupportingTransitionRequestInputs inputs,
        string supportRequestPath,
        string committedLedgerPath,
        string commitResultPath)
    {
        var requestPath = Path.GetFullPath(supportRequestPath);
        var baseLedgerPath = Path.GetFullPath(inputs.StrategyLedgerPath);
        var committedPath = Path.GetFullPath(committedLedgerPath);
        var resultPath = Path.GetFullPath(commitResultPath);
        var snapshotPath = Path.GetFullPath(inputs.SnapshotPath);
        var expected = AcquisitionRouteSupportingTransitionRequestBuilder
            .Build(inputs);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionRequest>(
            requestPath,
            "Acquisition supporting-transition request");
        Require(EqualJson(request, expected),
            "Supporting-transition request drifted from deterministic source compilation.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition support commit snapshot");
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var baseLedger = AcquisitionStrategyLedgerReader.Read(
            baseLedgerPath,
            snapshotDocument.RootElement).Ledger;
        var committed = AcquisitionStrategyLedgerReader.Read(
            committedPath,
            snapshotDocument.RootElement).Ledger;
        var result = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioCommitResult>(
            resultPath,
            "Acquisition support reservation commit result");
        return BuildCore(
            request,
            baseLedger,
            committed,
            snapshot,
            result,
            CurrentTeacherFrontierSupport.HashFile(requestPath),
            CurrentTeacherFrontierSupport.HashFile(baseLedgerPath),
            CurrentTeacherFrontierSupport.HashFile(committedPath),
            CurrentTeacherFrontierSupport.HashFile(resultPath));
    }

    internal static AcquisitionRouteSupportingTransitionCommitReceipt BuildCore(
        AcquisitionRouteSupportingTransitionRequest request,
        StrategyCommitmentLedger baseLedger,
        StrategyCommitmentLedger committed,
        SnapshotEnvelope snapshot,
        ReservationPortfolioCommitResult result,
        string requestSha256 = "",
        string baseLedgerSha256 = "",
        string committedLedgerSha256 = "",
        string commitResultSha256 = "")
    {
        var reasons = ValidateRequest(request, baseLedger, snapshot);
        var exactClaims = ValidateExactClaims(request, committed, reasons);
        var singleRevision = false;
        if (request.AtomicCommitRequest is not null)
        {
            singleRevision =
                ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                    request.AtomicCommitRequest,
                    baseLedger,
                    committed,
                    snapshot,
                    result,
                    reasons);
            ReservationPortfolioCommitEvidenceVerifier.ReleasesCancelled(
                request.AtomicCommitRequest,
                committed,
                reasons);
        }
        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var verified = blocking.Length == 0 && exactClaims && singleRevision;
        return new AcquisitionRouteSupportingTransitionCommitReceipt
        {
            Status = verified
                ? "verified_atomic_support_reservation_commit"
                : "blocked_support_reservation_commit_receipt",
            SupportRequestId = request.SupportRequestId,
            GoalId = request.GoalId,
            RouteOccurrenceId = request.RouteOccurrenceId,
            SourceStateHash = request.SourceStateHash,
            SelectedCandidateId = request.SelectedCandidateId,
            BaseLedgerRevision = baseLedger.Revision,
            CommittedLedgerRevision = committed.Revision,
            SupportRequestSha256 = requestSha256,
            BaseLedgerSha256 = baseLedgerSha256,
            CommittedLedgerSha256 = committedLedgerSha256,
            CommitResultSha256 = commitResultSha256,
            ReservationClaimIds = request.ReservationClaimIds.ToArray(),
            ExactActiveClaimSetVerified = exactClaims,
            SingleRevisionCommitVerified = singleRevision,
            SupportReservationCommitVerified = verified,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

    private static List<string> ValidateRequest(
        AcquisitionRouteSupportingTransitionRequest request,
        StrategyCommitmentLedger baseLedger,
        SnapshotEnvelope snapshot)
    {
        var reasons = new List<string>();
        if (request.SchemaVersion !=
                "acquisition_route_supporting_transition_request.v1" ||
            request.Status != "ready_for_atomic_support_reservation_commit" ||
            !request.SupportRequestReady ||
            !request.DeadlineProofVerified ||
            !request.ReservationClaimBoundToCandidate ||
            !request.AtomicCommitPreflightPassed ||
            request.AtomicCommitRequest is null ||
            request.FormalTrainingAuthorized ||
            request.BlockingReasons.Length != 0)
        {
            reasons.Add("support_request_not_commit_receipt_eligible");
        }
        if (request.SourceStateHash != snapshot.StateHash ||
            request.BaseLedgerRevision != baseLedger.Revision ||
            request.AtomicCommitRequest?.StateHash != snapshot.StateHash ||
            request.AtomicCommitRequest?.ExpectedLedgerRevision !=
                baseLedger.Revision ||
            request.AtomicCommitRequest?.PortfolioId != request.SupportRequestId)
        {
            reasons.Add("support_commit_source_identity_mismatch");
        }
        var claimIds = request.ReservationMaterialClaims
            .Select(value => value.ReservationId)
            .Concat(request.ReservationCurrencyClaims.Select(value =>
                value.ReservationId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (claimIds.Length == 0 ||
            claimIds.Distinct(StringComparer.Ordinal).Count() !=
                claimIds.Length ||
            !claimIds.SequenceEqual(
                request.ReservationClaimIds,
                StringComparer.Ordinal))
        {
            reasons.Add("support_reservation_claim_inventory_invalid");
        }
        return reasons;
    }

    private static bool ValidateExactClaims(
        AcquisitionRouteSupportingTransitionRequest request,
        StrategyCommitmentLedger committed,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        var activeMaterial = committed.MaterialReservations.Where(value =>
                value.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        var activeCurrency = committed.CurrencyReservations.Where(value =>
                value.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        var expectedIds = request.ReservationClaimIds
            .Order(StringComparer.Ordinal)
            .ToArray();
        var decisionIds = request.ReservationMaterialClaims
            .Select(value => value.SourceDecisionId)
            .Concat(request.ReservationCurrencyClaims.Select(value =>
                value.SourceDecisionId))
            .ToHashSet(StringComparer.Ordinal);
        var actualIds = activeMaterial.Where(value =>
                decisionIds.Contains(value.SourceDecisionId))
            .Select(value => value.ReservationId)
            .Concat(activeCurrency.Where(value =>
                    decisionIds.Contains(value.SourceDecisionId))
                .Select(value => value.ReservationId))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
            reasons.Add("support_route_active_claim_set_mismatch");
        foreach (var claim in request.ReservationMaterialClaims)
        {
            var row = activeMaterial.SingleOrDefault(value =>
                value.ReservationId == claim.ReservationId);
            if (row is null ||
                !ReservationPortfolioCommitEvidenceVerifier.Exact(
                    row,
                    claim,
                    committed.PlayerId))
            {
                reasons.Add("support_material_claim_not_exactly_committed:" +
                    claim.ReservationId);
            }
        }
        foreach (var claim in request.ReservationCurrencyClaims)
        {
            var row = activeCurrency.SingleOrDefault(value =>
                value.ReservationId == claim.ReservationId);
            if (row is null ||
                !ReservationPortfolioCommitEvidenceVerifier.Exact(
                    row,
                    claim,
                    committed.PlayerId))
            {
                reasons.Add("support_currency_claim_not_exactly_committed:" +
                    claim.ReservationId);
            }
        }
        return reasons.Count == initialCount;
    }


}
