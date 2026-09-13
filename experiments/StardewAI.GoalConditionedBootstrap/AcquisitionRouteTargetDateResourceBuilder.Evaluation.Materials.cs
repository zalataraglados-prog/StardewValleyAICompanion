namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static AcquisitionRouteTargetDateResource EvaluateCrop(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionResourceInputSnapshotState state)
    {
        if (staticRoute.CropSource is null)
            return Blocked(route, CropSeed, "authoritative_crop_source_missing");
        var targets = route.TargetEvaluations;
        if (targets.Any(target =>
                target.MatchingExistingCropSlotCount > 0))
        {
            return Result(
                route,
                "resolved_resource_inputs_not_required",
                true,
                true,
                CropSeed,
                new[]
                {
                    new AcquisitionResourceInputEvaluation(
                        "existing_target_crop",
                        QualifiedItemId(route),
                        0,
                        targets.Sum(target =>
                            target.MatchingExistingCropSlotCount ?? 0),
                        "resolved_existing_target_crop_requires_no_new_seed",
                        new[]
                        {
                            "upstream_route.target_evaluations[].matching_existing_crop_slot_count"
                        },
                        Array.Empty<string>())
                },
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (!targets.Any(target => target.OpenPreparedSoilSlotCount > 0))
            return Blocked(route, CropSeed, "matched_crop_capacity_source_missing");

        var seedQualifiedItemId = QualifyObjectId(
            staticRoute.CropSource.SeedItemId);
        return EvaluateMaterial(
            route,
            CropSeed,
            "crop_seed",
            seedQualifiedItemId,
            1,
            state);
    }

    private static AcquisitionRouteTargetDateResource EvaluateShop(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionResourceInputSnapshotState state)
    {
        var shop = staticRoute.ShopSource;
        if (shop is null)
            return Blocked(route, ShopTrade, "authoritative_shop_source_missing");
        var quoteLookup = state.ShopQuotes.ShopQuote(
            shop,
            QualifiedItemId(route));
        if (!quoteLookup.EvidenceAvailable || !quoteLookup.Found ||
            quoteLookup.Quote is null)
        {
            return Blocked(
                route,
                ShopTrade,
                quoteLookup.BlockingReasons);
        }
        var quote = quoteLookup.Quote;
        var staticTradeItem = string.IsNullOrWhiteSpace(shop.TradeItemId)
            ? null
            : QualifyObjectId(shop.TradeItemId);
        if (quote.CurrencyId != shop.Currency)
        {
            return Blocked(
                route,
                ShopTrade,
                "current_native_shop_quote_currency_drifted");
        }
        if (!shop.RequiresItemQueryResolution &&
            (quote.TradeItemQualifiedId != staticTradeItem ||
             quote.TradeItemCount != (staticTradeItem is null
                    ? null
                    : shop.TradeItemAmount)))
        {
            return Blocked(
                route,
                ShopTrade,
                "current_native_shop_quote_trade_terms_drifted");
        }
        if (string.IsNullOrWhiteSpace(quote.TradeItemQualifiedId))
        {
            return NotRequired(route, ShopTrade);
        }
        if (!quote.TradeItemCount.HasValue ||
            !quote.TradeItemQualifiedId.StartsWith("(", StringComparison.Ordinal))
        {
            return Blocked(
                route,
                ShopTrade,
                "current_native_shop_quote_trade_terms_invalid");
        }
        return EvaluateMaterial(
            route,
            ShopTrade,
            "shop_trade_item",
            quote.TradeItemQualifiedId,
            quote.TradeItemCount.Value,
            state);
    }

    private static AcquisitionRouteTargetDateResource EvaluateMaterial(
        AcquisitionRouteTargetDateFacility route,
        string requirementKind,
        string inputKind,
        string qualifiedItemId,
        int requiredQuantity,
        AcquisitionResourceInputSnapshotState state)
    {
        if (!state.MaterialEvidenceAvailable)
            return Blocked(route, requirementKind, state.MaterialBlockingReasons);
        var available = state.AvailableQuantity(qualifiedItemId);
        var evaluation = new AcquisitionResourceInputEvaluation(
            inputKind,
            qualifiedItemId,
            requiredQuantity,
            available,
            available >= requiredQuantity
                ? "resolved_resource_input_match"
                : "resolved_resource_input_miss",
            new[]
            {
                "state.farm.material_inventory_graph.value.inventory_nodes[].slots[]"
            },
            Array.Empty<string>());
        return available >= requiredQuantity
            ? ResolvedMatch(route, requirementKind, evaluation)
            : ResolvedMiss(
                route,
                requirementKind,
                evaluation,
                "required_resource_quantity_unavailable:" +
                qualifiedItemId);
    }

    private static string QualifyObjectId(string itemId)
    {
        Require(!string.IsNullOrWhiteSpace(itemId),
            "A crop seed item ID is missing.");
        return itemId.StartsWith("(", StringComparison.Ordinal)
            ? itemId
            : "(O)" + itemId;
    }
}
