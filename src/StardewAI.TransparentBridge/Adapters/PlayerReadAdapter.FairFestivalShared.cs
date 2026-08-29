using StardewValley;
using StardewValley.Internal;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string FairTokenShopId = "Festival_StardewValleyFair_StarTokens";
    private const int FairStardropPrice = 2000;

    private static (bool StardropAcquired, int ProjectedGrangeTokens, int RemainingDemand)
        ReadFairStardropDemand(Farmer player, StardewValley.Event festival)
    {
        var stardropAcquired = player.hasOrWillReceiveMail("CF_Fair");
        var projectedGrangeTokens = ProjectUnclaimedGrangeTokens(player, festival);
        var remainingDemand = stardropAcquired
            ? 0
            : Math.Max(0, FairStardropPrice - player.festivalScore - projectedGrangeTokens);
        return (stardropAcquired, projectedGrangeTokens, remainingDemand);
    }

    private static object[] ReadFairStarTokenShopRows()
    {
        if (!DataLoader.Shops(Game1.content).TryGetValue(FairTokenShopId, out var shop))
            return Array.Empty<object>();
        return ShopBuilder.GetShopStock(FairTokenShopId, shop)
            .OrderBy(entry => entry.Value.Price)
            .ThenBy(entry => entry.Key.QualifiedItemId, StringComparer.Ordinal)
            .Select(entry => new
            {
                qualified_item_id = entry.Key.QualifiedItemId,
                item_id = entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId,
                display_name = entry.Key.DisplayName,
                price_star_tokens = entry.Value.Price,
                stock = entry.Value.Stock,
                infinite_stock = entry.Value.Stock == StardewValley.Menus.ShopMenu.infiniteStock,
                limited_stock_mode = entry.Value.LimitedStockMode.ToString(),
                synced_key = entry.Value.SyncedKey,
                can_buy_item = entry.Key.CanBuyItem(Game1.player),
                can_afford_now = Game1.player.festivalScore >= entry.Value.Price
            })
            .Cast<object>()
            .ToArray();
    }

    private static int ProjectUnclaimedGrangeTokens(Farmer player, StardewValley.Event festival)
    {
        if (festival.grangeJudged)
            return 0;
        var display = Enumerable.Range(0, 9)
            .Select(slot => slot < player.team.grangeDisplay.Count ? player.team.grangeDisplay[slot] : null)
            .ToArray();
        var bestScore = ScoreSelectedGrange(SelectBestGrangeChoices(BuildGrangeChoices(player, display)));
        return bestScore >= 90 ? 1000 : bestScore >= 75 ? 500 : bestScore >= 60 ? 250 : bestScore == -666 ? 750 : 50;
    }
}
