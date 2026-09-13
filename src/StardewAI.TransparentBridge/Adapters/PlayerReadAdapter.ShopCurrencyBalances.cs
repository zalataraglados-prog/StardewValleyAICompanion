using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static readonly (int Id, string Key, string NativeMember)[]
        ShopCurrencyDomain =
        {
            (0, "money", "Farmer.Money"),
            (1, "star_tokens", "Farmer.festivalScore"),
            (2, "club_coins", "Farmer.clubCoins"),
            (4, "qi_gems", "Farmer.QiGems")
        };

    private static object ReadShopCurrencyBalances(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new
            {
                schema_version = "shop_currency_balances.v1",
                projection_status = "unavailable_world_or_player",
                rows = Array.Empty<object>(),
                supported_currency_ids = ShopCurrencyDomain
                    .Select(row => row.Id)
                    .ToArray()
            };
        }

        return new
        {
            schema_version = "shop_currency_balances.v1",
            projection_status =
                "complete_locked_base_1.6.15_shop_menu_currency_domain",
            rows = ShopCurrencyDomain.Select(row => new
            {
                currency_id = row.Id,
                currency_key = row.Key,
                balance = ReadNativeShopCurrencyBalance(player, row.Id),
                native_member = row.NativeMember
            }).ToArray(),
            supported_currency_ids = ShopCurrencyDomain
                .Select(row => row.Id)
                .ToArray(),
            native_read_method = "ShopMenu.getPlayerCurrencyAmount",
            native_charge_method = "ShopMenu.chargePlayer"
        };
    }

    internal static int ReadNativeShopCurrencyBalance(
        Farmer player,
        int currencyId) =>
        ShopMenu.getPlayerCurrencyAmount(player, currencyId);
}
