using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public sealed class CurrencyReservationLedgerService
{
    private readonly NativeCurrencySupplyProjection projection = new();

    public StrategyCommitmentMutationResult Upsert(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        CurrencyReservationUpsertRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        ValidateRequest(request, errors);
        var balances = TryReadBalances(snapshot, errors);
        var ownerPlayerId = ParsePlayerId(snapshot, errors);
        if (balances is not null && ownerPlayerId.HasValue && errors.Count == 0)
        {
            var otherActive = (current?.CurrencyReservations ??
                    Array.Empty<CurrencyReservation>())
                .Where(row => !string.Equals(
                    row.ReservationId,
                    request.ReservationId,
                    StringComparison.Ordinal))
                .ToArray();
            var supply = projection.Project(
                balances,
                ownerPlayerId.Value,
                otherActive);
            if (supply.Status != "available")
                errors.AddRange(supply.BlockingReasons);
            var row = supply.Balances.SingleOrDefault(value =>
                value.CurrencyId == request.CurrencyId);
            if (row is null || row.AvailableAmount < request.Amount)
            {
                errors.Add(
                    "currency_reservation_insufficient_unreserved_amount");
            }
        }
        if (errors.Count > 0)
            return Rejected(current, errors);

        NativeShopCurrencies.TryGetKey(request.CurrencyId, out var currencyKey);
        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        var existing = ledger.CurrencyReservations.FirstOrDefault(row =>
            string.Equals(
                row.ReservationId,
                request.ReservationId,
                StringComparison.Ordinal));
        var reservation = new CurrencyReservation
        {
            ReservationId = request.ReservationId,
            Revision = (existing?.Revision ?? 0) + 1,
            Status = StrategyCommitmentStatuses.Active,
            SourceDecisionId = request.SourceDecisionId,
            SourceStateHash = snapshot.StateHash,
            GoalId = request.GoalId,
            OwnerPlayerId = ownerPlayerId!.Value,
            CurrencyId = request.CurrencyId,
            CurrencyKey = currencyKey,
            Amount = request.Amount,
            Purpose = request.Purpose
        };
        ledger.CurrencyReservations = ledger.CurrencyReservations
            .Where(row => !string.Equals(
                row.ReservationId,
                reservation.ReservationId,
                StringComparison.Ordinal))
            .Append(reservation)
            .OrderBy(row => row.ReservationId, StringComparer.Ordinal)
            .ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            reservation.ReservationId,
            reservation.Revision,
            reservation.SourceDecisionId,
            "currency_reservation_upsert",
            updatedAt,
            string.Empty);
        return Accepted(ledger);
    }

    public StrategyCommitmentMutationResult Cancel(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        string reservationId,
        StrategyCommitmentCancelRequest request,
        string updatedAt)
    {
        var errors = StrategyCommitmentLedgerSupport.ValidateCommon(
            current,
            snapshot,
            request.StateHash,
            request.ExpectedLedgerRevision);
        var existing = current?.CurrencyReservations.FirstOrDefault(row =>
            string.Equals(row.ReservationId, reservationId,
                StringComparison.Ordinal));
        if (existing is null)
            errors.Add("currency_reservation_not_found");
        else if (existing.Status != StrategyCommitmentStatuses.Active)
            errors.Add("only_active_currency_reservation_can_be_cancelled");
        if (string.IsNullOrWhiteSpace(request.Reason))
            errors.Add("cancel_reason_required");
        if (errors.Count > 0)
            return Rejected(current, errors);

        var ledger = StrategyCommitmentLedgerSupport.CloneOrCreate(
            current,
            snapshot,
            updatedAt);
        ledger.CurrencyReservations = ledger.CurrencyReservations.Select(row =>
        {
            if (row.ReservationId != reservationId)
                return row;
            var cancelled = StrategyCommitmentLedgerSupport.CloneCurrency(row);
            cancelled.Status = StrategyCommitmentStatuses.Cancelled;
            cancelled.Revision++;
            cancelled.CancelReason = request.Reason;
            return cancelled;
        }).ToArray();
        StrategyCommitmentLedgerSupport.Advance(ledger, snapshot, updatedAt);
        var cancelledReservation = ledger.CurrencyReservations.Single(row =>
            row.ReservationId == reservationId);
        StrategyCommitmentLedgerSupport.AppendHistory(
            ledger,
            cancelledReservation.ReservationId,
            cancelledReservation.Revision,
            cancelledReservation.SourceDecisionId,
            "currency_reservation_cancel",
            updatedAt,
            request.Reason);
        return Accepted(ledger);
    }

    private static void ValidateRequest(
        CurrencyReservationUpsertRequest request,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationId))
            errors.Add("currency_reservation_id_required");
        if (string.IsNullOrWhiteSpace(request.SourceDecisionId))
            errors.Add("source_decision_id_required");
        if (string.IsNullOrWhiteSpace(request.GoalId))
            errors.Add("goal_id_required");
        if (!NativeShopCurrencies.TryGetKey(request.CurrencyId, out _))
            errors.Add("currency_reservation_currency_id_unsupported");
        if (request.Amount <= 0)
            errors.Add("currency_reservation_amount_not_positive");
        if (string.IsNullOrWhiteSpace(request.Purpose))
            errors.Add("currency_reservation_purpose_required");
    }

    private static IReadOnlyDictionary<int, int>? TryReadBalances(
        SnapshotEnvelope snapshot,
        ICollection<string> errors)
    {
        if (!ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "player",
                "shop_currency_balances")))
        {
            errors.Add("shop_currency_balances_unavailable");
            return null;
        }
        var value = ReadStateFieldValue(
            snapshot,
            "player",
            "shop_currency_balances");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object ||
            ReadString(value.Value, "schema_version") !=
                "shop_currency_balances.v1" ||
            ReadString(value.Value, "projection_status") !=
                "complete_locked_base_1.6.15_shop_menu_currency_domain" ||
            !value.Value.TryGetProperty("supported_currency_ids", out var ids) ||
            ids.ValueKind != JsonValueKind.Array ||
            !value.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            errors.Add("shop_currency_balances_invalid");
            return null;
        }
        var supported = ids.EnumerateArray()
            .Where(row => row.TryGetInt32(out _))
            .Select(row => row.GetInt32())
            .ToArray();
        if (supported.Length != NativeShopCurrencies.All.Count ||
            !supported.ToHashSet().SetEquals(
                NativeShopCurrencies.All.Select(row => row.Id)))
        {
            errors.Add("shop_currency_balance_domain_invalid");
            return null;
        }
        var result = new Dictionary<int, int>();
        foreach (var row in rows.EnumerateArray())
        {
            var id = ReadInt(row, "currency_id");
            var key = ReadString(row, "currency_key");
            var balance = ReadInt(row, "balance");
            if (!id.HasValue ||
                !NativeShopCurrencies.TryGetKey(id.Value, out var expectedKey) ||
                key != expectedKey || !balance.HasValue || balance < 0 ||
                !result.TryAdd(id.Value, balance.Value))
            {
                errors.Add("shop_currency_balance_row_invalid");
                return null;
            }
        }
        if (result.Count != NativeShopCurrencies.All.Count)
        {
            errors.Add("shop_currency_balance_row_count_invalid");
            return null;
        }
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "money")) ||
            !ReadStateFieldValue(snapshot, "player", "money")
                .GetValueOrDefault()
                .TryGetInt32(out var money) ||
            result[NativeShopCurrencies.Money] != money)
        {
            errors.Add("shop_currency_money_balance_drifted");
            return null;
        }
        return result;
    }

    private static long? ParsePlayerId(
        SnapshotEnvelope snapshot,
        ICollection<string> errors)
    {
        if (long.TryParse(snapshot.PlayerId.Value, out var playerId))
            return playerId;
        errors.Add("snapshot_player_id_invalid_for_currency_reservation");
        return null;
    }

    private static string ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var result)
            ? result
            : null;

    private static StrategyCommitmentMutationResult Accepted(
        StrategyCommitmentLedger ledger) => new()
        {
            Accepted = true,
            Ledger = ledger
        };

    private static StrategyCommitmentMutationResult Rejected(
        StrategyCommitmentLedger? ledger,
        IEnumerable<string> errors) => new()
        {
            Accepted = false,
            Errors = errors.Distinct(StringComparer.Ordinal).ToArray(),
            Ledger = ledger
        };
}
