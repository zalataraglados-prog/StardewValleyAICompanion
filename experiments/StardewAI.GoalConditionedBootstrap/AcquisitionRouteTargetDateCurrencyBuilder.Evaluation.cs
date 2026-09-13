namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateCurrencyBuilder
{
    private const string NoCurrency = "no_direct_currency_cost";
    private const string ShopPurchase = "current_native_shop_purchase_quote";
    private const string DirectMoneyPayment = "native_money_payment";

    private static readonly IReadOnlyDictionary<string, string>
        CurrencyClassByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = NoCurrency,
            ["harvests_as"] = NoCurrency,
            ["machine_output"] = NoCurrency,
            ["native_bush_shake"] = NoCurrency,
            ["native_crab_pot_output"] = NoCurrency,
            ["native_farm_animal_deluxe_produce"] = NoCurrency,
            ["native_farm_animal_produce"] = NoCurrency,
            ["native_fish_pond_output"] = NoCurrency,
            ["native_fruit_tree_produce"] = NoCurrency,
            ["native_geode_default_drop"] = NoCurrency,
            ["native_geode_drop"] = NoCurrency,
            ["native_ginger_harvest"] = NoCurrency,
            ["native_location_artifact_spot"] = NoCurrency,
            ["native_location_fish_spawn"] = NoCurrency,
            ["native_location_forage_spawn"] = NoCurrency,
            ["native_machine_flavored_output"] = NoCurrency,
            ["native_machine_item_query_output"] = NoCurrency,
            ["native_mine_buried_item"] = NoCurrency,
            ["native_mine_fishing_override"] = NoCurrency,
            ["native_money_payment"] = DirectMoneyPayment,
            ["native_monster_drop_table"] = NoCurrency,
            ["native_object_artifact_spot_chance"] = NoCurrency,
            ["native_radioactive_ore_node"] = NoCurrency,
            ["native_solar_panel_output"] = NoCurrency,
            ["native_spring_onion_harvest"] = NoCurrency,
            ["native_tea_bush_harvest"] = NoCurrency,
            ["native_tree_moss_harvest"] = NoCurrency,
            ["native_wild_tree_chop_drop"] = NoCurrency,
            ["native_wild_tree_seed"] = NoCurrency,
            ["native_wild_tree_seed_drop"] = NoCurrency,
            ["native_wild_tree_tapper_output"] = NoCurrency,
            ["recipe_output"] = NoCurrency,
            ["sells"] = ShopPurchase
        };

    private static AcquisitionRouteTargetDateCurrency Evaluate(
        AcquisitionRouteTargetDateResource route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRouteAmount amount,
        AcquisitionShopQuoteSnapshotState state)
    {
        if (!route.ResourceInputAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_resource_input_axis",
                false,
                null,
                "upstream_resource_inputs",
                null,
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_resource_input_axis_unresolved" });
        }
        if (route.ResourceInputsMatchTargetDate is null)
        {
            return Result(
                route,
                "not_applicable_upstream_resource_input_axis",
                true,
                null,
                "not_applicable",
                null,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (route.ResourceInputsMatchTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_upstream_resource_input_miss",
                true,
                null,
                "not_applicable",
                null,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        return CurrencyClassByRouteKind[RouteKind(route)] switch
        {
            NoCurrency => NotRequired(route),
            ShopPurchase => EvaluateShop(route, staticRoute, state),
            DirectMoneyPayment => EvaluateMoneyPayment(route, amount, state),
            _ => throw new InvalidDataException(
                "Unknown currency requirement classification.")
        };
    }

    private static AcquisitionRouteTargetDateCurrency EvaluateShop(
        AcquisitionRouteTargetDateResource route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionShopQuoteSnapshotState state)
    {
        var source = staticRoute.ShopSource;
        if (source is null)
            return Blocked(route, ShopPurchase,
                "authoritative_shop_source_missing");
        var lookup = state.ShopQuote(source, QualifiedItemId(route));
        if (!lookup.EvidenceAvailable)
            return Blocked(route, ShopPurchase, lookup.BlockingReasons);
        if (!lookup.Found || lookup.Quote is null)
        {
            return ResolvedMiss(
                route,
                ShopPurchase,
                new AcquisitionCurrencyEvaluation(
                    source.Currency,
                    string.Empty,
                    null,
                    null,
                    "current_native_shop_quote_missing",
                    "resolved_currency_budget_miss",
                    new[] { "state.locations.shops.value.shops[]" }),
                lookup.BlockingReasons);
        }

        var quote = lookup.Quote;
        if (quote.CurrencyId != source.Currency)
            return Blocked(route, ShopPurchase,
                "current_native_shop_quote_currency_drifted");
        if (!ResourceTermsMatchQuote(route, quote))
            return Blocked(route, ShopPurchase,
                "resource_axis_and_current_shop_quote_disagree");

        var balance = state.CurrencyBalance(quote.CurrencyId);
        if (!balance.EvidenceAvailable || !balance.Balance.HasValue)
            return Blocked(route, ShopPurchase, balance.BlockingReasons);
        var evaluation = new AcquisitionCurrencyEvaluation(
            quote.CurrencyId,
            balance.CurrencyKey,
            quote.Price,
            balance.Balance,
            !quote.CanBuyItem
                ? "current_native_shop_item_not_buyable"
                : !quote.InfiniteStock && quote.Stock <= 0
                    ? "current_native_shop_item_out_of_stock"
                    : "current_native_shop_quote_available",
            balance.Balance >= quote.Price && quote.CanBuyItem &&
                (quote.InfiniteStock || quote.Stock > 0)
                    ? "resolved_currency_budget_match"
                    : "resolved_currency_budget_miss",
            new[]
            {
                "state.locations.shops.value.shops[].stock_preview.entries[]",
                "state.player.shop_currency_balances.value.rows[]"
            });
        var reasons = new List<string>();
        if (!quote.CanBuyItem)
            reasons.Add("current_native_shop_item_not_buyable");
        if (!quote.InfiniteStock && quote.Stock <= 0)
            reasons.Add("current_native_shop_item_out_of_stock");
        if (balance.Balance < quote.Price)
            reasons.Add("required_currency_amount_unavailable:" +
                balance.CurrencyKey);
        return reasons.Count == 0
            ? ResolvedMatch(route, ShopPurchase, evaluation)
            : ResolvedMiss(route, ShopPurchase, evaluation, reasons.ToArray());
    }

    private static AcquisitionRouteTargetDateCurrency EvaluateMoneyPayment(
        AcquisitionRouteTargetDateResource route,
        AcquisitionRouteAmount amount,
        AcquisitionShopQuoteSnapshotState state)
    {
        if (amount.MatchKind != "money_payment" || amount.Amount <= 0 ||
            amount.QualifiedItemId.Length != 0 || amount.SourceId != "money")
        {
            return Blocked(route, DirectMoneyPayment,
                "native_money_payment_lowering_contract_invalid");
        }
        var balance = state.CurrencyBalance(0);
        if (!balance.EvidenceAvailable || !balance.Balance.HasValue)
            return Blocked(route, DirectMoneyPayment, balance.BlockingReasons);
        var evaluation = new AcquisitionCurrencyEvaluation(
            0,
            balance.CurrencyKey,
            amount.Amount,
            balance.Balance,
            "not_applicable_direct_money_payment",
            balance.Balance >= amount.Amount
                ? "resolved_currency_budget_match"
                : "resolved_currency_budget_miss",
            new[]
            {
                "acquisition_lowering.requirement_sets[].groups[].alternatives[].amount",
                "state.player.shop_currency_balances.value.rows[]"
            });
        return balance.Balance >= amount.Amount
            ? ResolvedMatch(route, DirectMoneyPayment, evaluation)
            : ResolvedMiss(
                route,
                DirectMoneyPayment,
                evaluation,
                "required_currency_amount_unavailable:money");
    }

    private static bool ResourceTermsMatchQuote(
        AcquisitionRouteTargetDateResource route,
        AcquisitionShopQuote quote)
    {
        if (quote.TradeItemQualifiedId is null)
        {
            return route.ResourceInputAxisStatus ==
                    "resolved_resource_inputs_not_required" &&
                route.InputEvaluations.Length == 0;
        }
        return route.ResourceInputAxisStatus ==
                "resolved_resource_inputs_match" &&
            route.InputEvaluations.Length == 1 &&
            route.InputEvaluations[0].QualifiedItemId ==
                quote.TradeItemQualifiedId &&
            route.InputEvaluations[0].RequiredQuantity == quote.TradeItemCount;
    }
}
