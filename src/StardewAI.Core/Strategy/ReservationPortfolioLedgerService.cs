using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public sealed partial class ReservationPortfolioLedgerService
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
        var markerOnly = request.ReleaseReservationIds.Length == 0 &&
            request.MaterialClaims.Length == 0 &&
            request.CurrencyClaims.Length == 0;
        StrategyCommitmentLedger? staged = markerOnly
            ? StrategyCommitmentLedgerSupport.CloneOrCreate(
                current,
                snapshot,
                updatedAt)
            : current;

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

    public ReservationPortfolioRouteSettlementResult SettleCompletedRoute(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        ReservationPortfolioRouteSettlementRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateSettlementRequest(current, request, errors);
        if (errors.Count > 0)
            return SettlementRejected(current, request, errors);

        var reservationIds = request.ReservationIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var reservationSet = reservationIds.ToHashSet(StringComparer.Ordinal);
        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        ledger.MaterialReservations = ledger.MaterialReservations.Select(row =>
        {
            if (!reservationSet.Contains(row.ReservationId))
                return row;
            var completed = StrategyCommitmentLedgerSupport.CloneMaterial(row);
            completed.Status = StrategyCommitmentStatuses.Completed;
            completed.Revision++;
            completed.CompletionReason = request.Reason;
            completed.CompletionEvidenceSha256 =
                request.FreshTerminalReceiptSha256;
            return completed;
        }).ToArray();
        ledger.CurrencyReservations = ledger.CurrencyReservations.Select(row =>
        {
            if (!reservationSet.Contains(row.ReservationId))
                return row;
            var completed = StrategyCommitmentLedgerSupport.CloneCurrency(row);
            completed.Status = StrategyCommitmentStatuses.Completed;
            completed.Revision++;
            completed.CompletionReason = request.Reason;
            completed.CompletionEvidenceSha256 =
                request.FreshTerminalReceiptSha256;
            return completed;
        }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        foreach (var row in ledger.MaterialReservations.Where(row =>
                     reservationSet.Contains(row.ReservationId)))
        {
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                row.ReservationId,
                row.Revision,
                row.SourceDecisionId,
                "material_reservation_complete",
                updatedAt,
                request.Reason);
        }
        foreach (var row in ledger.CurrencyReservations.Where(row =>
                     reservationSet.Contains(row.ReservationId)))
        {
            StrategyCommitmentLedgerSupport.AppendHistory(
                ledger,
                row.ReservationId,
                row.Revision,
                row.SourceDecisionId,
                "currency_reservation_complete",
                updatedAt,
                request.Reason);
        }
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            request.PortfolioId,
            ledger.Revision,
            request.RouteSourceDecisionId,
            "reservation_portfolio_route_complete",
            updatedAt,
            request.FreshTerminalReceiptSha256);
        return new ReservationPortfolioRouteSettlementResult
        {
            Accepted = true,
            PortfolioId = request.PortfolioId,
            RouteSourceDecisionId = request.RouteSourceDecisionId,
            FreshTerminalReceiptSha256 =
                request.FreshTerminalReceiptSha256,
            CommittedLedgerRevision = ledger.Revision,
            CompletedReservationIds = reservationIds,
            Ledger = ledger
        };
    }

    private static void ValidateSettlementRequest(
        StrategyCommitmentLedger? current,
        ReservationPortfolioRouteSettlementRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.PortfolioId))
            errors.Add("reservation_portfolio_id_required");
        if (string.IsNullOrWhiteSpace(request.GoalId))
            errors.Add("goal_id_required");
        if (string.IsNullOrWhiteSpace(request.RouteSourceDecisionId))
            errors.Add("route_source_decision_id_required");
        if (!IsLowerSha256(request.FreshTerminalReceiptSha256))
            errors.Add("fresh_terminal_receipt_sha256_invalid");
        if (string.IsNullOrWhiteSpace(request.Reason))
            errors.Add("completion_reason_required");
        if (request.ReservationIds is null)
        {
            errors.Add("reservation_ids_required");
            return;
        }
        if (request.ReservationIds.Any(string.IsNullOrWhiteSpace) ||
            request.ReservationIds.Distinct(StringComparer.Ordinal).Count() !=
            request.ReservationIds.Length)
        {
            errors.Add("reservation_ids_invalid");
        }
        if (current is null)
        {
            errors.Add("reservation_portfolio_ledger_required");
            return;
        }
        if (!(current.History ?? Array.Empty<StrategyCommitmentHistoryEntry>())
            .Any(row => row.CommitmentId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_commit"))
        {
            errors.Add("reservation_portfolio_commit_not_found");
        }
        if ((current.History ?? Array.Empty<StrategyCommitmentHistoryEntry>())
            .Any(row => row.CommitmentId == request.PortfolioId &&
                row.SourceDecisionId == request.RouteSourceDecisionId &&
                row.Operation == "reservation_portfolio_route_complete"))
        {
            errors.Add("reservation_portfolio_route_already_completed");
        }

        var active = current.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId == request.RouteSourceDecisionId)
            .Select(row => (row.ReservationId, row.GoalId))
            .Concat(current.CurrencyReservations
                .Where(row =>
                    row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceDecisionId == request.RouteSourceDecisionId)
                .Select(row => (row.ReservationId, row.GoalId)))
            .ToArray();
        if (active.Any(row => row.GoalId != request.GoalId))
            errors.Add("reservation_portfolio_route_goal_mismatch");
        var expected = active.Select(row => row.ReservationId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var supplied = request.ReservationIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(supplied, StringComparer.Ordinal))
            errors.Add("reservation_portfolio_route_active_set_mismatch");
    }

    private static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ReservationPortfolioRouteSettlementResult
        SettlementRejected(
            StrategyCommitmentLedger? ledger,
            ReservationPortfolioRouteSettlementRequest request,
            IEnumerable<string> errors) => new()
            {
                Accepted = false,
                PortfolioId = request.PortfolioId,
                RouteSourceDecisionId = request.RouteSourceDecisionId,
                FreshTerminalReceiptSha256 =
                request.FreshTerminalReceiptSha256,
                Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
                Ledger = ledger
            };

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
