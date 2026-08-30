using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string CasinoCurrencyTargetItemId = "(BC)126";
    private const int CasinoCurrencyTargetCoins = 10000;

    private static CasinoCurrencyDemand ReadCasinoCurrencyDemand(Farmer player)
    {
        var recipeUnlocked = player.craftingRecipes.ContainsKey("Deluxe Scarecrow");
        var societyReceivedOrPending = player.hasOrWillReceiveMail("RarecrowSociety");
        var targetExistsAnywhere = player.hasClubCard && !recipeUnlocked && !societyReceivedOrPending &&
            Utility.doesItemExistAnywhere(CasinoCurrencyTargetItemId);
        var targetRequired = !recipeUnlocked && !societyReceivedOrPending && !targetExistsAnywhere;
        return new CasinoCurrencyDemand(
            recipeUnlocked,
            societyReceivedOrPending,
            targetExistsAnywhere,
            targetRequired,
            targetRequired ? Math.Max(0, CasinoCurrencyTargetCoins - player.clubCoins) : 0);
    }

    private static object[] ReadCasinoShopRows()
    {
        var shops = DataLoader.Shops(Game1.content);
        if (!shops.TryGetValue("Casino", out var casino))
            return Array.Empty<object>();
        return casino.Items.Select(row => new
        {
            id = row.Id ?? string.Empty,
            item_id = row.ItemId ?? string.Empty,
            price_club_coins = row.Price,
            available_stock = row.AvailableStock,
            condition = row.Condition ?? string.Empty,
            actions_on_purchase = row.ActionsOnPurchase ?? new List<string>(),
            is_calico_jack_demand_target = string.Equals(row.ItemId, CasinoCurrencyTargetItemId, StringComparison.Ordinal),
            is_currency_demand_target = string.Equals(row.ItemId, CasinoCurrencyTargetItemId, StringComparison.Ordinal)
        }).ToArray();
    }

    private sealed record CasinoCurrencyDemand(
        bool DeluxeScarecrowRecipeUnlocked,
        bool RarecrowSocietyReceivedOrPending,
        bool TargetExistsAnywhere,
        bool TargetRequired,
        int RemainingClubCoinDemand);
}
