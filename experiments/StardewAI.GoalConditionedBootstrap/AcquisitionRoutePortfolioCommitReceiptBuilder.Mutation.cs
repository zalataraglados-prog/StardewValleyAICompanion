using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    private static bool ValidateMutation(
        AcquisitionRoutePortfolioAdmission admission,
        StrategyCommitmentLedger baseLedger,
        StrategyCommitmentLedger committed,
        SnapshotEnvelope snapshot,
        string? commitResultPath,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        if (string.IsNullOrWhiteSpace(commitResultPath))
        {
            reasons.Add("atomic_commit_result_required");
            return false;
        }
        var result = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioCommitResult>(
            Path.GetFullPath(commitResultPath),
            "Reservation portfolio commit result");
        var request = admission.AtomicCommitRequest!;
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

    private static bool ValidateUnchangedLedger(
        StrategyCommitmentLedger baseLedger,
        StrategyCommitmentLedger committed,
        string? commitResultPath,
        ICollection<string> reasons)
    {
        var initialCount = reasons.Count;
        if (!string.IsNullOrWhiteSpace(commitResultPath))
            reasons.Add("commit_result_for_non_mutating_admission_forbidden");
        if (!EqualJson(baseLedger, committed))
            reasons.Add("non_mutating_portfolio_ledger_changed");
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
