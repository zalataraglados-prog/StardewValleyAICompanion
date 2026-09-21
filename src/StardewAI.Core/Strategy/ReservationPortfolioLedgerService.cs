using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed class ReservationPortfolioLedgerService
{
    private readonly MaterialReservationLedgerService materialService = new();
    private readonly CurrencyReservationLedgerService currencyService = new();

    public ReservationPortfolioCommitResult Commit(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        ReservationPortfolioCommitRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateRequest(current, request, errors);
        if (errors.Count > 0)
            return Rejected(current, request, errors);

        var originalRevision = current?.Revision ?? 0;
        var originalHistoryCount = current?.History.Length ?? 0;
        StrategyCommitmentLedger? staged = current;

        foreach (var reservationId in request.ReleaseReservationIds
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var release = Release(
                staged,
                snapshot,
                request,
                reservationId,
                updatedAt);
            if (!release.Accepted)
                return Rejected(current, request, release.Errors);
            staged = release.Ledger;
        }

        foreach (var claim in request.MaterialClaims
                     .OrderBy(value => value.ReservationId, StringComparer.Ordinal))
        {
            var result = materialService.Upsert(
                staged,
                snapshot,
                Copy(claim, staged?.Revision ?? 0),
                updatedAt);
            if (!result.Accepted)
                return Rejected(current, request, result.Errors);
            staged = result.Ledger;
        }

        foreach (var claim in request.CurrencyClaims
                     .OrderBy(value => value.ReservationId, StringComparer.Ordinal))
        {
            var result = currencyService.Upsert(
                staged,
                snapshot,
                Copy(claim, staged?.Revision ?? 0),
                updatedAt);
            if (!result.Accepted)
                return Rejected(current, request, result.Errors);
            staged = result.Ledger;
        }

        var committed = staged!;
        committed.Revision = originalRevision + 1;
        foreach (var history in committed.History.Skip(originalHistoryCount))
            history.LedgerRevision = committed.Revision;
        StrategyCommitmentLedgerSupport.AppendHistory(
            committed,
            request.PortfolioId,
            committed.Revision,
            request.SourceDecisionId,
            "reservation_portfolio_commit",
            updatedAt,
            string.Empty);

        return new ReservationPortfolioCommitResult
        {
            Accepted = true,
            PortfolioId = request.PortfolioId,
            CommittedLedgerRevision = committed.Revision,
            MaterialClaimCount = request.MaterialClaims.Length,
            CurrencyClaimCount = request.CurrencyClaims.Length,
            ReleasedReservationIds = request.ReleaseReservationIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            Ledger = committed
        };
    }

    private StrategyCommitmentMutationResult Release(
        StrategyCommitmentLedger? staged,
        SnapshotEnvelope snapshot,
        ReservationPortfolioCommitRequest request,
        string reservationId,
        string updatedAt)
    {
        var cancel = new StrategyCommitmentCancelRequest
        {
            StateHash = request.StateHash,
            ExpectedLedgerRevision = staged?.Revision ?? 0,
            Reason = "reservation_portfolio_replaced:" + request.PortfolioId
        };
        if (staged!.MaterialReservations.Any(row =>
                row.ReservationId == reservationId))
        {
            return materialService.Cancel(
                staged,
                snapshot,
                reservationId,
                cancel,
                updatedAt);
        }
        return currencyService.Cancel(
            staged,
            snapshot,
            reservationId,
            cancel,
            updatedAt);
    }

    private static void ValidateRequest(
        StrategyCommitmentLedger? current,
        ReservationPortfolioCommitRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.PortfolioId))
            errors.Add("reservation_portfolio_id_required");
        if (string.IsNullOrWhiteSpace(request.GoalId))
            errors.Add("goal_id_required");
        if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
            errors.Add("source_decision_id_required");
        if (request.ReleaseReservationIds is null ||
            request.MaterialClaims is null ||
            request.CurrencyClaims is null)
        {
            errors.Add("reservation_portfolio_arrays_required");
            return;
        }
        if (request.MaterialClaims.Any(row => row is null) ||
            request.CurrencyClaims.Any(row => row is null))
        {
            errors.Add("reservation_portfolio_claim_is_null");
            return;
        }
        if (request.ReleaseReservationIds.Length == 0 &&
            request.MaterialClaims.Length == 0 &&
            request.CurrencyClaims.Length == 0)
        {
            errors.Add("reservation_portfolio_empty");
        }

        var releaseIds = request.ReleaseReservationIds;
        if (releaseIds.Any(string.IsNullOrWhiteSpace) ||
            releaseIds.Distinct(StringComparer.Ordinal).Count() != releaseIds.Length)
        {
            errors.Add("release_reservation_ids_invalid");
        }
        var claimIds = request.MaterialClaims.Select(row => row.ReservationId)
            .Concat(request.CurrencyClaims.Select(row => row.ReservationId))
            .ToArray();
        if (claimIds.Any(string.IsNullOrWhiteSpace) ||
            claimIds.Distinct(StringComparer.Ordinal).Count() != claimIds.Length)
        {
            errors.Add("reservation_portfolio_claim_ids_not_globally_unique");
        }
        if (claimIds.Intersect(releaseIds, StringComparer.Ordinal).Any())
            errors.Add("reservation_portfolio_claim_release_overlap");

        foreach (var claim in request.MaterialClaims)
            ValidateClaimIdentity(request, claim.StateHash,
                claim.ExpectedLedgerRevision, claim.GoalId, errors);
        foreach (var claim in request.CurrencyClaims)
            ValidateClaimIdentity(request, claim.StateHash,
                claim.ExpectedLedgerRevision, claim.GoalId, errors);

        var allIds = (current?.MaterialReservations.Select(row =>
                row.ReservationId) ?? Enumerable.Empty<string>())
            .Concat(current?.CurrencyReservations.Select(row =>
                row.ReservationId) ?? Enumerable.Empty<string>())
            .ToArray();
        if (allIds.Distinct(StringComparer.Ordinal).Count() != allIds.Length)
        {
            errors.Add("ledger_reservation_ids_not_globally_unique");
            return;
        }
        var activeRows = (current?.MaterialReservations
                .Where(row => row.Status == StrategyCommitmentStatuses.Active)
                .Select(row => (row.ReservationId, row.GoalId)) ??
            Enumerable.Empty<(string ReservationId, string GoalId)>())
            .Concat(current?.CurrencyReservations
                .Where(row => row.Status == StrategyCommitmentStatuses.Active)
                .Select(row => (row.ReservationId, row.GoalId)) ??
                Enumerable.Empty<(string ReservationId, string GoalId)>())
            .ToArray();
        if (activeRows.Select(row => row.ReservationId)
            .Distinct(StringComparer.Ordinal).Count() != activeRows.Length)
        {
            errors.Add("active_reservation_ids_not_globally_unique");
            return;
        }
        var active = activeRows.ToDictionary(
            row => row.ReservationId,
            row => row.GoalId,
            StringComparer.Ordinal);
        foreach (var releaseId in releaseIds)
        {
            if (!active.TryGetValue(releaseId, out var goalId))
                errors.Add("active_reservation_to_release_not_found:" + releaseId);
            else if (goalId != request.GoalId)
                errors.Add("release_reservation_goal_mismatch:" + releaseId);
        }
    }

    private static void ValidateClaimIdentity(
        ReservationPortfolioCommitRequest request,
        string stateHash,
        int expectedRevision,
        string goalId,
        ICollection<string> errors)
    {
        if (stateHash != request.StateHash)
            errors.Add("reservation_portfolio_claim_state_hash_mismatch");
        if (expectedRevision != request.ExpectedLedgerRevision)
            errors.Add("reservation_portfolio_claim_ledger_revision_mismatch");
        if (goalId != request.GoalId)
            errors.Add("reservation_portfolio_claim_goal_mismatch");
    }

    private static MaterialReservationUpsertRequest Copy(
        MaterialReservationUpsertRequest source,
        int expectedRevision) => new()
        {
            StateHash = source.StateHash,
            ExpectedLedgerRevision = expectedRevision,
            ReservationId = source.ReservationId,
            SourceDecisionId = source.SourceDecisionId,
            GoalId = source.GoalId,
            NodeId = source.NodeId,
            SlotIndex = source.SlotIndex,
            QualifiedItemId = source.QualifiedItemId,
            Quantity = source.Quantity,
            Purpose = source.Purpose
        };

    private static CurrencyReservationUpsertRequest Copy(
        CurrencyReservationUpsertRequest source,
        int expectedRevision) => new()
        {
            StateHash = source.StateHash,
            ExpectedLedgerRevision = expectedRevision,
            ReservationId = source.ReservationId,
            SourceDecisionId = source.SourceDecisionId,
            GoalId = source.GoalId,
            CurrencyId = source.CurrencyId,
            Amount = source.Amount,
            Purpose = source.Purpose
        };

    private static ReservationPortfolioCommitResult Rejected(
        StrategyCommitmentLedger? ledger,
        ReservationPortfolioCommitRequest request,
        IEnumerable<string> errors) => new()
        {
            Accepted = false,
            PortfolioId = request.PortfolioId,
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
            Ledger = ledger
        };
}
