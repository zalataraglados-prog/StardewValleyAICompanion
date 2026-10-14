using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    private static MoneyRevenueCandidateCatalogEntry BuildEntry(
        RevenueActionNodeDefinition definition,
        EventCandidate candidate) => BuildEntry(
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
            candidate.ShopId,
            candidate.SlotIndex,
            candidate.Quantity,
            candidate.UnitPrice,
            candidate.TotalValue,
            candidate.EstimatedTicks,
            candidate.EnergyCost,
            candidate.Parameters,
            candidate.BlockReasons);

    private static MoneyRevenueCandidateCatalogEntry BuildEntry(
        RevenueActionNodeDefinition definition,
        EconomicCandidate candidate) => BuildEntry(
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
            candidate.ShopId,
            candidate.SlotIndex,
            candidate.Quantity,
            candidate.UnitPrice,
            candidate.TotalValue,
            0,
            0,
            candidate.Parameters,
            candidate.BlockReasons);

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
        entry.CandidateSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                new
                {
                    entry.CatalogCandidateId,
                    entry.CandidateChannel,
                    entry.CandidateId,
                    entry.CandidateKind,
                    entry.SemanticKindBinding,
                    entry.Available,
                    entry.ComparisonEligible,
                    entry.LocationId,
                    entry.TileX,
                    entry.TileY,
                    entry.ItemId,
                    entry.QualifiedItemId,
                    entry.ShopId,
                    entry.SlotIndex,
                    entry.Quantity,
                    entry.UnitPrice,
                    entry.TotalValue,
                    entry.EstimatedTicks,
                    entry.EnergyCost,
                    entry.Parameters,
                    entry.BlockingReasons
                });
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
