using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static class ReservationPortfolioCommitEvidenceVerifier
{
    internal static bool ValidateMutation(
        ReservationPortfolioCommitRequest request,
        StrategyCommitmentLedger baseLedger,
        StrategyCommitmentLedger committed,
        SnapshotEnvelope snapshot,
        ReservationPortfolioCommitResult result,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        if (!result.Accepted ||
            (result.Errors?.Length ?? 0) != 0 ||
            result.PortfolioId != request.PortfolioId ||
            result.CommittedLedgerRevision != baseLedger.Revision + 1 ||
            result.MaterialClaimCount != request.MaterialClaims.Length ||
            result.CurrencyClaimCount != request.CurrencyClaims.Length ||
            result.Ledger is null ||
            !EqualJson(result.Ledger, committed) ||
            !SameSet(result.ReleasedReservationIds ?? Array.Empty<string>(),
                request.ReleaseReservationIds))
        {
            reasons.Add("atomic_commit_result_mismatch");
        }
        if (committed.Revision != baseLedger.Revision + 1)
            reasons.Add("atomic_commit_did_not_advance_exactly_one_revision");
        var markers = (committed.History ??
                Array.Empty<StrategyCommitmentHistoryEntry>())
            .Where(row =>
                row.LedgerRevision == committed.Revision &&
                row.CommitmentId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_commit" &&
                row.SourceDecisionId == request.SourceDecisionId)
            .ToArray();
        if (markers.Length != 1)
        {
            reasons.Add("atomic_commit_exact_replay_marker_invalid");
        }
        else
        {
            var replay = new ReservationPortfolioLedgerService().Commit(
                baseLedger,
                snapshot,
                request,
                markers[0].RecordedAt);
            if (!replay.Accepted ||
                replay.Ledger is null ||
                !EqualJson(replay.Ledger, committed))
            {
                reasons.Add("atomic_commit_exact_replay_mismatch");
            }
        }
        ValidateCommitHistory(request, committed, reasons);
        return reasons.Count == initialCount;
    }

    internal static bool Exact(
        MaterialReservation row,
        MaterialReservationUpsertRequest claim,
        string playerId) =>
        long.TryParse(playerId, out var owner) &&
        row.ReservationId == claim.ReservationId &&
        row.Revision > 0 &&
        row.Status == StrategyCommitmentStatuses.Active &&
        row.SourceDecisionId == claim.SourceDecisionId &&
        row.SourceStateHash == claim.StateHash &&
        row.GoalId == claim.GoalId &&
        row.OwnerPlayerId == owner &&
        row.NodeId == claim.NodeId &&
        row.SlotIndex == claim.SlotIndex &&
        row.QualifiedItemId == claim.QualifiedItemId &&
        row.Quantity == claim.Quantity &&
        row.Purpose == claim.Purpose &&
        string.IsNullOrEmpty(row.CancelReason);

    internal static bool Exact(
        CurrencyReservation row,
        CurrencyReservationUpsertRequest claim,
        string playerId) =>
        long.TryParse(playerId, out var owner) &&
        NativeShopCurrencies.TryGetKey(claim.CurrencyId, out var currencyKey) &&
        row.ReservationId == claim.ReservationId &&
        row.Revision > 0 &&
        row.Status == StrategyCommitmentStatuses.Active &&
        row.SourceDecisionId == claim.SourceDecisionId &&
        row.SourceStateHash == claim.StateHash &&
        row.GoalId == claim.GoalId &&
        row.OwnerPlayerId == owner &&
        row.CurrencyId == claim.CurrencyId &&
        row.CurrencyKey == currencyKey &&
        row.Amount == claim.Amount &&
        row.Purpose == claim.Purpose &&
        string.IsNullOrEmpty(row.CancelReason);

    internal static bool ReleasesCancelled(
        ReservationPortfolioCommitRequest request,
        StrategyCommitmentLedger committed,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        foreach (var releaseId in request.ReleaseReservationIds)
        {
            var rows = committed.MaterialReservations
                .Where(row => row.ReservationId == releaseId)
                .Select(row => row.Status)
                .Concat(committed.CurrencyReservations
                    .Where(row => row.ReservationId == releaseId)
                    .Select(row => row.Status))
                .ToArray();
            if (rows.Length != 1 ||
                rows[0] != StrategyCommitmentStatuses.Cancelled)
            {
                reasons.Add("released_reservation_not_cancelled:" + releaseId);
            }
        }
        return reasons.Count == initialCount;
    }

    private static void ValidateCommitHistory(
        ReservationPortfolioCommitRequest request,
        StrategyCommitmentLedger committed,
        ICollection<string> reasons)
    {
        var expected = request.ReleaseReservationIds.Select(id =>
                (Id: id, OperationSuffix: "_reservation_cancel"))
            .Concat(request.MaterialClaims.Select(claim =>
                (Id: claim.ReservationId,
                    OperationSuffix: "material_reservation_upsert")))
            .Concat(request.CurrencyClaims.Select(claim =>
                (Id: claim.ReservationId,
                    OperationSuffix: "currency_reservation_upsert")))
            .ToArray();
        var current = (committed.History ??
                Array.Empty<StrategyCommitmentHistoryEntry>()).Where(row =>
                row.LedgerRevision == committed.Revision)
            .ToArray();
        if (current.Length != expected.Length + 1)
        {
            reasons.Add("atomic_commit_history_count_mismatch");
            return;
        }
        foreach (var entry in expected)
        {
            if (!current.Any(row =>
                    row.CommitmentId == entry.Id &&
                    (entry.OperationSuffix.StartsWith("_",
                            StringComparison.Ordinal)
                        ? row.Operation.EndsWith(entry.OperationSuffix,
                            StringComparison.Ordinal)
                        : row.Operation == entry.OperationSuffix)))
            {
                reasons.Add("atomic_commit_component_history_missing:" +
                    entry.Id);
            }
        }
        if (current.Count(row =>
                row.CommitmentId == request.PortfolioId &&
                row.Operation == "reservation_portfolio_commit" &&
                row.SourceDecisionId == request.SourceDecisionId) != 1)
        {
            reasons.Add("atomic_commit_portfolio_history_missing");
        }
    }

    private static bool SameSet(string[] left, string[] right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(
            right.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);

}
