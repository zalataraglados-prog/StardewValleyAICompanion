using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateCurrencyBuilder
{
    private static AcquisitionRouteTargetDateCurrency EvaluateGeodeFee(
        AcquisitionRouteTargetDateResource route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionShopQuoteSnapshotState state,
        AcquisitionCurrentRouteCandidateIndex? candidates)
    {
        if (candidates is null)
        {
            return Blocked(
                route,
                CurrentGeodeFee,
                "current_geode_candidate_index_missing");
        }
        if (!candidates.TryFind(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Blocked(
                route,
                CurrentGeodeFee,
                blockingReasons);
        }

        var candidate = matches
            .Where(value => value.OptionId == "processing.crack_geode" &&
                value.PriceGold > 0 &&
                value.MoneyBefore.HasValue &&
                value.ExpectedOutputStack >= staticRoute.RequiredAmount &&
                value.ExpectedOutputQuality >= staticRoute.MinimumQuality)
            .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is null)
        {
            return Blocked(
                route,
                CurrentGeodeFee,
                "matching_current_geode_fee_candidate_not_present");
        }

        var balance = state.CurrencyBalance(NativeShopCurrencies.Money);
        if (!balance.EvidenceAvailable || !balance.Balance.HasValue)
            return Blocked(route, CurrentGeodeFee, balance.BlockingReasons);
        if (balance.Balance.Value != candidate.MoneyBefore)
        {
            return Blocked(
                route,
                CurrentGeodeFee,
                "current_geode_money_balance_drifted");
        }

        var required = candidate.PriceGold!.Value;
        var matchesBudget = balance.Balance.Value >= required;
        var evaluation = new AcquisitionCurrencyEvaluation(
            NativeShopCurrencies.Money,
            balance.CurrencyKey,
            required,
            balance.Balance,
            candidate.ExpectedOutputStack,
            candidate.ExpectedOutputQuality,
            1,
            "current_native_geode_fee_available",
            matchesBudget
                ? "resolved_currency_budget_match"
                : "resolved_currency_budget_miss",
            new[]
            {
                "candidate:processing.crack_geode",
                "state.player.geode_processing.value.money_before",
                "state.player.geode_processing.value.price_gold",
                "state.player.shop_currency_balances.value.rows[]"
            });
        return matchesBudget
            ? ResolvedMatch(route, CurrentGeodeFee, evaluation)
            : ResolvedMiss(
                route,
                CurrentGeodeFee,
                evaluation,
                "required_currency_amount_unavailable:money");
    }
}
