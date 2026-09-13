using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionReservationSupplyValidator
{
    public static void Validate(
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources,
        AcquisitionShopQuoteSnapshotState currencies)
    {
        ValidateMaterials(ledgerState, resources);
        ValidateCurrencies(ledgerState, currencies);
    }

    private static void ValidateMaterials(
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionResourceInputSnapshotState resources)
    {
        var active = ledgerState.Ledger.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        if (active.Length == 0)
            return;
        var graph = resources.MaterialGraph;
        if (!resources.MaterialEvidenceAvailable || graph is null)
        {
            throw new InvalidDataException(
                "Active material reservations lack a current material graph.");
        }
        Require(graph.PlayerId == ledgerState.ActorPlayerId,
            "Active material reservations and material graph disagree on actor.");
        var projection = new MaterialSupplyProjection().Project(
            graph,
            active);
        Require(projection.Status == "available",
            "Active material reservation supply is invalid: " +
            string.Join(",", projection.BlockingReasons));
    }

    private static void ValidateCurrencies(
        AcquisitionStrategyLedgerState ledgerState,
        AcquisitionShopQuoteSnapshotState currencies)
    {
        var active = ledgerState.Ledger.CurrencyReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active)
            .ToArray();
        if (active.Length == 0)
            return;
        var balances = new Dictionary<int, int>();
        foreach (var definition in NativeShopCurrencies.All)
        {
            var lookup = currencies.CurrencyBalance(definition.Id);
            if (!lookup.EvidenceAvailable || !lookup.Balance.HasValue)
            {
                throw new InvalidDataException(
                    "Active currency reservations lack current balance evidence: " +
                    string.Join(",", lookup.BlockingReasons));
            }
            balances[definition.Id] = lookup.Balance.GetValueOrDefault();
        }
        var projection = new NativeCurrencySupplyProjection().Project(
            balances,
            ledgerState.ActorPlayerId,
            active);
        Require(projection.Status == "available",
            "Active currency reservation supply is invalid: " +
            string.Join(",", projection.BlockingReasons));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
