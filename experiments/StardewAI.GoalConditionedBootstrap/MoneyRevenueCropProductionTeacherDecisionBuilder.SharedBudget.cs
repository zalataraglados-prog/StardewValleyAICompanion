using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static int ReadUnreservedMoney(
        SnapshotEnvelope snapshot,
        AcquisitionStrategyLedgerState? baseLedger)
    {
        var money = ReadStateInt(snapshot, "player", "money");
        if (baseLedger is null || !baseLedger.Ledger.CurrencyReservations.Any(
                row => row.Status == StrategyCommitmentStatuses.Active))
            return money;

        // Reuse the acquisition currency reader without serializing the full world.
        var currencies = new AcquisitionShopQuoteSnapshotState(
            JsonSerializer.SerializeToElement(
                new { player = snapshot.State["player"] },
                JsonDefaults.Options));
        var balances = new Dictionary<int, int>();
        foreach (var currency in NativeShopCurrencies.All)
        {
            var lookup = currencies.CurrencyBalance(currency.Id);
            Require(lookup.EvidenceAvailable && lookup.Balance.HasValue,
                "Money Revenue crop-production Teacher reserved currency lacks current balance evidence: " +
                string.Join(",", lookup.BlockingReasons));
            balances[currency.Id] = lookup.Balance!.Value;
        }
        var supply = new NativeCurrencySupplyProjection().Project(
            balances,
            baseLedger.ActorPlayerId,
            baseLedger.Ledger.CurrencyReservations);
        Require(supply.Status == "available" && supply.BlockingReasons.Length == 0,
            "Money Revenue crop-production Teacher currency supply is blocked: " +
            string.Join(",", supply.BlockingReasons));
        return supply.Balances.Single(row => row.CurrencyId == NativeShopCurrencies.Money)
            .AvailableAmount;
    }
}
