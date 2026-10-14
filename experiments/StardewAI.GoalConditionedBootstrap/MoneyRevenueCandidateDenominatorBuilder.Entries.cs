using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    private static MoneyRevenueCandidateCatalogEntry BuildEntry(
        RevenueActionNodeDefinition definition,
        EventCandidate candidate)
    {
        var entry = BuildEntry(
            definition,
            "event_candidate",
            candidate.CandidateId,
            candidate.Kind,
            candidate.Available,
            candidate.LocationId,
            candidate.TileX,
            candidate.TileY,
            candidate.ItemId,
            candidate.QualifiedItemId,
            candidate.DisplayName,
            candidate.ShopId,
            candidate.SlotIndex,
            candidate.Quantity,
            candidate.UnitPrice,
            candidate.TotalValue,
            candidate.EstimatedTicks,
            candidate.EnergyCost,
            candidate.Parameters,
            candidate.BlockReasons);
        entry.ExpectedEffect = candidate.ExpectedEffect;
        entry.AvailabilityClass = candidate.AvailabilityClass;
        entry.AllowedNow = candidate.AllowedNow;
        entry.AllowedToday = candidate.AllowedToday;
        entry.NextOpenTime = candidate.NextOpenTime;
        entry.EffectiveOpenTime = candidate.EffectiveOpenTime;
        entry.ClosesAt = candidate.ClosesAt;
        entry.WaitCost = candidate.WaitCost;
        entry.GateReasons = candidate.GateReasons;
        entry.FullShipmentKnown = candidate.FullShipmentKnown;
        entry.FullShipmentEligible = candidate.FullShipmentEligible;
        entry.FullShipmentCurrentShippedCount =
            candidate.FullShipmentCurrentShippedCount;
        entry.FullShipmentAlreadyShipped =
            candidate.FullShipmentAlreadyShipped;
        entry.FullShipmentContributes = candidate.FullShipmentContributes;
        entry.AvailableStack = candidate.AvailableStack;
        return Seal(entry);
    }

    private static MoneyRevenueCandidateCatalogEntry BuildEntry(
        RevenueActionNodeDefinition definition,
        EconomicCandidate candidate)
    {
        var entry = BuildEntry(
            definition,
            "economic_candidate",
            candidate.CandidateId,
            candidate.Kind,
            candidate.Available,
            string.Empty,
            null,
            null,
            candidate.ItemId,
            candidate.QualifiedItemId,
            candidate.DisplayName,
            candidate.ShopId,
            candidate.SlotIndex,
            candidate.Quantity,
            candidate.UnitPrice,
            candidate.TotalValue,
            0,
            0,
            candidate.Parameters,
            candidate.BlockReasons);
        entry.StockId = candidate.StockId;
        entry.OutputStack = candidate.OutputStack;
        entry.OutputQuality = candidate.OutputQuality;
        entry.CurrencyBalance = candidate.CurrencyBalance;
        entry.Stock = candidate.Stock;
        entry.InfiniteStock = candidate.InfiniteStock;
        entry.CanShip = candidate.CanShip;
        entry.CanShopSell = candidate.CanShopSell;
        entry.FullShipmentKnown = candidate.FullShipmentKnown;
        entry.FullShipmentEligible = candidate.FullShipmentEligible;
        entry.FullShipmentCurrentShippedCount =
            candidate.FullShipmentCurrentShippedCount;
        entry.FullShipmentAlreadyShipped =
            candidate.FullShipmentAlreadyShipped;
        entry.FullShipmentContributes = candidate.FullShipmentContributes;
        return Seal(entry);
    }

    private static MoneyRevenueCandidateCatalogEntry BuildEntry(
        RevenueActionNodeDefinition definition,
        string channel,
        string candidateId,
        string candidateKind,
        bool available,
        string locationId,
        int? tileX,
        int? tileY,
        string itemId,
        string qualifiedItemId,
        string displayName,
        string shopId,
        int? slotIndex,
        int quantity,
        int unitPrice,
        int totalValue,
        int estimatedTicks,
        double energyCost,
        SmallModelActionParameter[] parameters,
        string[] blockingReasons)
    {
        parameters ??= Array.Empty<SmallModelActionParameter>();
        blockingReasons ??= Array.Empty<string>();
        var semanticBinding = SemanticKindBinding(
            definition,
            candidateKind,
            parameters);
        var catalogCandidateId = definition.NodeId + "::" + channel +
            "::" + candidateId;
        var entry = new MoneyRevenueCandidateCatalogEntry
        {
            CatalogCandidateId = catalogCandidateId,
            CandidateChannel = channel,
            CandidateId = candidateId,
            CandidateKind = candidateKind,
            SemanticKindBinding = semanticBinding,
            Available = available,
            ComparisonEligible = definition.PolicyChoice &&
                available &&
                blockingReasons.Length == 0 &&
                semanticBinding != "unbound",
            LocationId = locationId,
            TileX = tileX,
            TileY = tileY,
            ItemId = itemId,
            QualifiedItemId = qualifiedItemId,
            DisplayName = displayName,
            ShopId = shopId,
            SlotIndex = slotIndex,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalValue = totalValue,
            EstimatedTicks = estimatedTicks,
            EnergyCost = energyCost,
            Parameters = parameters,
            BlockingReasons = blockingReasons
        };
        return entry;
    }

    private static MoneyRevenueCandidateCatalogEntry Seal(
        MoneyRevenueCandidateCatalogEntry entry)
    {
        entry.CandidateSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                entry);
        return entry;
    }

    private static string SemanticKindBinding(
        RevenueActionNodeDefinition definition,
        string candidateKind,
        SmallModelActionParameter[] parameters)
    {
        if (definition.CandidateKinds.Contains(
                candidateKind,
                StringComparer.Ordinal))
        {
            return "direct_declared_kind";
        }
        var continuationOptionIds = parameters
            .Where(parameter => string.Equals(
                parameter.Name,
                "continuation.option_id",
                StringComparison.Ordinal))
            .Select(parameter => parameter.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return continuationOptionIds.Length == 1 && string.Equals(
            continuationOptionIds[0],
            definition.OptionId,
            StringComparison.Ordinal)
                ? "staged_continuation"
                : "unbound";
    }
}
