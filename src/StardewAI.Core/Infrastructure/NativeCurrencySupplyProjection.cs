using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Infrastructure;

public sealed class NativeCurrencySupplyProjection
{
    public NativeCurrencySupplyProjectionResult Project(
        IReadOnlyDictionary<int, int> balances,
        long ownerPlayerId,
        IEnumerable<CurrencyReservation>? reservations = null)
    {
        var reasons = new List<string>();
        ValidateBalances(balances, reasons);
        var reserved = ReadReservations(
            ownerPlayerId,
            reservations,
            reasons);
        var rows = NativeShopCurrencies.All.Select(currency =>
        {
            var total = balances.TryGetValue(currency.Id, out var balance)
                ? Math.Max(0, balance)
                : 0;
            var committed = reserved.TryGetValue(currency.Id, out var amount)
                ? amount
                : 0;
            if (committed > total)
            {
                reasons.Add(
                    "currency_reservation_exceeds_balance:" + currency.Key);
            }
            return new NativeCurrencySupplyBalance(
                currency.Id,
                currency.Key,
                total,
                committed,
                Math.Max(0, total - committed));
        }).ToArray();
        var distinctReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();
        return new NativeCurrencySupplyProjectionResult
        {
            Status = distinctReasons.Length == 0 ? "available" : "blocked",
            Balances = rows,
            BlockingReasons = distinctReasons
        };
    }

    private static void ValidateBalances(
        IReadOnlyDictionary<int, int> balances,
        ICollection<string> reasons)
    {
        if (balances.Count != NativeShopCurrencies.All.Count ||
            !balances.Keys.ToHashSet().SetEquals(
                NativeShopCurrencies.All.Select(row => row.Id)))
        {
            reasons.Add("native_currency_balance_domain_incomplete");
        }
        foreach (var row in balances)
        {
            if (!NativeShopCurrencies.TryGetKey(row.Key, out _) ||
                row.Value < 0)
            {
                reasons.Add("native_currency_balance_invalid");
            }
        }
    }

    private static IReadOnlyDictionary<int, int> ReadReservations(
        long ownerPlayerId,
        IEnumerable<CurrencyReservation>? reservations,
        ICollection<string> reasons)
    {
        var totals = new Dictionary<int, int>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reservation in reservations ??
                 Array.Empty<CurrencyReservation>())
        {
            if (reservation is null ||
                string.IsNullOrWhiteSpace(reservation.ReservationId) ||
                !ids.Add(reservation.ReservationId))
            {
                reasons.Add("currency_reservation_identity_invalid_or_duplicate");
                continue;
            }
            if (reservation.Status != StrategyCommitmentStatuses.Active &&
                reservation.Status != StrategyCommitmentStatuses.Cancelled &&
                reservation.Status != StrategyCommitmentStatuses.Completed)
            {
                reasons.Add(
                    "currency_reservation_status_invalid:" +
                    reservation.ReservationId);
                continue;
            }
            if (!string.Equals(
                    reservation.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!NativeShopCurrencies.TryGetKey(
                    reservation.CurrencyId,
                    out var expectedKey) ||
                reservation.CurrencyKey != expectedKey ||
                reservation.Amount <= 0 ||
                reservation.OwnerPlayerId != ownerPlayerId ||
                string.IsNullOrWhiteSpace(reservation.SourceDecisionId) ||
                string.IsNullOrWhiteSpace(reservation.GoalId) ||
                string.IsNullOrWhiteSpace(reservation.Purpose))
            {
                reasons.Add(
                    "active_currency_reservation_contract_invalid:" +
                    reservation.ReservationId);
                continue;
            }
            var previous = totals.TryGetValue(
                reservation.CurrencyId,
                out var value)
                ? value
                : 0;
            var combined = (long)previous + reservation.Amount;
            if (combined > int.MaxValue)
            {
                reasons.Add(
                    "currency_reservation_amount_overflow:" + expectedKey);
                totals[reservation.CurrencyId] = int.MaxValue;
                continue;
            }
            totals[reservation.CurrencyId] = (int)combined;
        }
        return totals;
    }
}
