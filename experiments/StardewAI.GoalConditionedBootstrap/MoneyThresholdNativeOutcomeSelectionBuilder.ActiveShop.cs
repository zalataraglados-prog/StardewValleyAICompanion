using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdNativeOutcomeSelectionBuilder
{
    internal sealed record BoundShopSale(
        MoneyRevenueCandidateCatalogEntry Candidate,
        SmallModelActionParameter[] ObjectiveParameters);

    internal static BoundShopSale[] BindShopSales(
        MoneyRevenueCandidateDenominatorCatalog catalog,
        SnapshotEnvelope? snapshot)
    {
        var nodes = catalog.Nodes.Where(node => node.PolicyChoice &&
            node.OptionId == "economy.sell_items").ToArray();
        Require(nodes.Length == 1 && nodes[0].EnumerationComplete,
            "Money threshold source has no unique complete shop-sale node.");
        var candidates = nodes[0].Candidates;
        var activeSales = candidates.Where(IsActiveEconomicSale).ToArray();
        var result = candidates.Where(IsExecutableShopSaleStage)
            .Select(candidate => new BoundShopSale(candidate, candidate.Parameters
                .Where(value => value.Name.StartsWith("continuation.", StringComparison.Ordinal)).ToArray()))
            .ToList();
        if (activeSales.Length == 0)
            return result.ToArray();
        if (snapshot is null)
            throw new InvalidDataException("Money threshold active economic sales require their source snapshot.");

        // The original catalog rebuild owns item/price/permission admission; bind its live UI context once.
        Require(snapshot.StateHash == catalog.SourceStateHash,
            "Money threshold active shop context does not bind the candidate catalog.");
        var menu = MoneyRevenueProgressionVerifier.ReadValue(snapshot, "menus", "active_menu");
        var context = MoneyRevenueProgressionVerifier.ReadValue(snapshot, "menus", "sell_context");
        var location = MoneyRevenueProgressionVerifier.ReadValue(snapshot, "player", "location_id");
        Require(menu.ValueKind == JsonValueKind.Object &&
                menu.TryGetProperty("is_open", out var open) && open.ValueKind == JsonValueKind.True &&
                NativeShopString(menu, "type") == "ShopMenu" &&
                context.ValueKind == JsonValueKind.Object &&
                NativeShopString(context, "kind") == "shop_sell_context" &&
                context.TryGetProperty("safety_timer", out var timer) && timer.ValueKind == JsonValueKind.Number &&
                timer.TryGetInt32(out var milliseconds) &&
                milliseconds <= 0 && location.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(location.GetString()),
            "Money threshold active native shop context is not ready.");
        var shopId = NativeShopString(context, "shop_id");
        Require(!string.IsNullOrWhiteSpace(shopId),
            "Money threshold active native shop identity is missing.");
        foreach (var candidate in activeSales.Where(candidate => candidate.ShopId == shopId))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["continuation.option_id"] = "economy.sell_items",
                ["continuation.shop_id"] = candidate.ShopId,
                ["continuation.target_location"] = location.GetString()!,
                ["continuation.item_id"] = candidate.ItemId,
                ["continuation.qualified_item_id"] = candidate.QualifiedItemId,
                ["continuation.slot_index"] = candidate.SlotIndex!.Value.ToString(CultureInfo.InvariantCulture),
                ["continuation.quantity"] = candidate.Quantity.ToString(CultureInfo.InvariantCulture),
                ["continuation.expected_unit_price"] = candidate.UnitPrice.ToString(CultureInfo.InvariantCulture)
            };
            result.Add(new BoundShopSale(candidate, values.Select(value => new SmallModelActionParameter
                { Name = value.Key, Value = value.Value }).ToArray()));
        }
        return result.ToArray();
    }

    private static bool IsActiveEconomicSale(MoneyRevenueCandidateCatalogEntry candidate) =>
        candidate.CandidateChannel == "economic_candidate" && candidate.CandidateKind == "sell_shop_item" &&
        candidate.SemanticKindBinding == "direct_declared_kind" && candidate.Available &&
        candidate.ComparisonEligible && candidate.CanShopSell == true && candidate.BlockingReasons.Length == 0 &&
        candidate.Parameters is { Length: 0 } && candidate.SlotIndex is >= 0 &&
        !string.IsNullOrWhiteSpace(candidate.ItemId) && !string.IsNullOrWhiteSpace(candidate.QualifiedItemId) &&
        !string.IsNullOrWhiteSpace(candidate.ShopId) && candidate.Quantity > 0 && candidate.UnitPrice > 0 &&
        candidate.TotalValue > 0 && (long)candidate.Quantity * candidate.UnitPrice == candidate.TotalValue;

    private static string NativeShopString(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
