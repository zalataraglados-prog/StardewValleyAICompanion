using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Shops;
using StardewValley.Internal;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ShopAccessReadAdapter : ReadAdapterBase
{
    private sealed record ShopAccessSummary(
        string shop_id,
        int owner_rule_count,
        int current_owner_rule_count,
        bool has_current_owner_rule,
        object[] current_owner_rules,
        int item_rule_count,
        bool condition_present,
        string? currency,
        string[] salable_item_tags,
        object stock_preview,
        object sale_preview);

    private sealed record FriendshipDoorGate(
        bool AllowedNow,
        int RequiredHearts,
        string[] NpcNames,
        bool GreenRainOverride,
        string Source);

    private static ShopAccessSummary ReadShopSummary(string shopId, ShopData shopData)
    {
        var ownerEntries = ReadEnumerableProperty(shopData, "Owners");
        var currentOwners = ShopBuilder.GetCurrentOwners((StardewValley.GameData.Shops.ShopData)shopData)
            .Select(owner => new
            {
                name = ReadStringProperty(owner, "Name"),
                type = ReadProperty(owner, "Type")?.ToString(),
                has_closed_message = !string.IsNullOrWhiteSpace(ReadStringProperty(owner, "ClosedMessage")),
                condition_present = !string.IsNullOrWhiteSpace(ReadStringProperty(owner, "Condition"))
            })
            .Cast<object>()
            .ToArray();

        var salableItemTags = (shopData.SalableItemTags ?? new List<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        return new ShopAccessSummary(
            shopId,
            ownerEntries.Length,
            currentOwners.Length,
            currentOwners.Length > 0,
            currentOwners,
            ReadEnumerableProperty(shopData, "Items").Length,
            !string.IsNullOrWhiteSpace(ReadStringProperty(shopData, "Condition")),
            ReadProperty(shopData, "Currency")?.ToString(),
            salableItemTags,
            ReadShopStockPreview(shopId, shopData),
            ReadShopSalePreview(shopId, shopData, salableItemTags));
    }

    private static object ReadShopSalePreview(
        string shopId,
        ShopData shopData,
        string[] salableItemTags)
    {
        var currency = ReadIntProperty(shopData, "Currency") ?? 0;
        var blockReasons = new List<string>();
        if (currency != 0)
        {
            blockReasons.Add("non_money_shop_sale_requires_audit");
        }
        if (salableItemTags.Length == 0)
        {
            blockReasons.Add("shop_has_no_salable_item_tags");
        }

        return new
        {
            kind = "shop_sale_preview",
            shop_id = shopId,
            source = "Data/Shops ShopData.SalableItemTags",
            currency,
            default_sell_percentage = 1f,
            salable_item_tags = salableItemTags,
            tag_groups_to_sell = salableItemTags
                .Select(tag => new[] { tag })
                .ToArray(),
            runtime_menu_recheck_required = true,
            executor_sale_preview_enabled = blockReasons.Count == 0,
            executor_block_reasons = blockReasons.ToArray()
        };
    }

    private static object ReadShopStockPreview(string shopId, ShopData shopData)
    {
        var stock = ShopBuilder.GetShopStock(shopId, shopData)
            .OrderBy(entry => entry.Key.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key.DisplayName, StringComparer.Ordinal)
            .Select(entry =>
            {
                var blockReasons = ShopStockPreviewBlockReasons(entry.Key, entry.Value);
                return new
                {
                    item_id = entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId,
                    qualified_item_id = entry.Key.QualifiedItemId,
                    display_name = entry.Key.DisplayName,
                    name = entry.Key.Name,
                    stack = entry.Key.Stack,
                    quality = entry.Key.Quality,
                    is_recipe = entry.Key.IsRecipe,
                    runtime_type = entry.Key.GetType().FullName,
                    price = entry.Value.Price,
                    stock = entry.Value.Stock,
                    infinite_stock = entry.Value.Stock == StardewValley.Menus.ShopMenu.infiniteStock,
                    trade_item = entry.Value.TradeItem,
                    trade_item_count = entry.Value.TradeItemCount,
                    effective_trade_item_count = entry.Value.TradeItem is null ? (int?)null : entry.Value.TradeItemCount ?? 5,
                    limited_stock_mode = entry.Value.LimitedStockMode.ToString(),
                    synced_key = entry.Value.SyncedKey,
                    action_on_purchase_count = entry.Value.ActionsOnPurchase?.Count ?? 0,
                    can_buy_item = entry.Key.CanBuyItem(Game1.player),
                    total_price_for_one_purchase = entry.Value.Price,
                    currency_balance = Game1.player.Money,
                    can_afford_one_with_currency = Game1.player.Money >= entry.Value.Price,
                    trade_item_available_count = entry.Value.TradeItem is null ? (int?)null : CountAvailableTradeItem(entry.Value.TradeItem),
                    can_afford_one_with_trade_item = entry.Value.TradeItem is null || CountAvailableTradeItem(entry.Value.TradeItem) >= (entry.Value.TradeItemCount ?? 5),
                    could_inventory_accept = entry.Key.GetSalableInstance() is Item salableItem && Game1.player.couldInventoryAcceptThisItem(salableItem),
                    action_when_purchased_may_discard_or_mutate = entry.Key.IsRecipe || entry.Value.ActionsOnPurchase?.Count > 0 || entry.Key.GetType() != typeof(StardewValley.Object),
                    executor_purchase_preview_enabled = blockReasons.Length == 0,
                    executor_block_reasons = blockReasons,
                    runtime_menu_recheck_required = true
                };
            })
            .ToArray();
        var anyEnabled = stock.Any(entry => entry.executor_purchase_preview_enabled);

        return new
        {
            kind = "shop_stock_preview",
            shop_id = shopId,
            source = "ShopBuilder.GetShopStock(shopId, shopData)",
            runtime_menu_recheck_required = true,
            executor_purchase_preview_enabled = anyEnabled,
            executor_block_reason = anyEnabled ? "" : "no_safe_executor_purchase_preview_candidate",
            entry_count = stock.Length,
            entries = stock
        };
    }

    private static string[] ShopStockPreviewBlockReasons(ISalable item, ItemStockInformation stock)
    {
        var reasons = new List<string>();

        if (stock.TradeItem is not null)
        {
            reasons.Add("trade_item_purchase_requires_consumption_audit");
        }

        if (stock.ActionsOnPurchase?.Count > 0)
        {
            reasons.Add("actions_on_purchase_present");
        }

        if (item.IsRecipe)
        {
            reasons.Add("recipe_purchase_discards_item_and_learns_recipe");
        }

        if (item.GetType() != typeof(StardewValley.Object))
        {
            reasons.Add("non_plain_object_purchase_side_effects_unmodeled");
        }

        if (stock.Stock != StardewValley.Menus.ShopMenu.infiniteStock &&
            (stock.LimitedStockMode.ToString() != "None" || stock.SyncedKey is not null))
        {
            reasons.Add("synchronized_or_limited_stock_requires_post_state_audit");
        }

        if (!item.CanBuyItem(Game1.player))
        {
            reasons.Add("shop_item_cannot_be_bought");
        }

        if (stock.Stock != StardewValley.Menus.ShopMenu.infiniteStock && stock.Stock <= 0)
        {
            reasons.Add("shop_item_out_of_stock");
        }

        if (Game1.player.Money < stock.Price)
        {
            reasons.Add("insufficient_currency_for_purchase");
        }

        if (stock.TradeItem is not null && CountAvailableTradeItem(stock.TradeItem) < (stock.TradeItemCount ?? 5))
        {
            reasons.Add("insufficient_trade_item_for_purchase");
        }

        if (item.GetSalableInstance() is not Item salableItem || !Game1.player.couldInventoryAcceptThisItem(salableItem))
        {
            reasons.Add("inventory_cannot_accept_purchase");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountAvailableTradeItem(string qualifiedOrUnqualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item is not null)
            .Where(item =>
                string.Equals(item!.QualifiedItemId, qualifiedOrUnqualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ItemId, qualifiedOrUnqualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item!.Stack);
    }

    private static object[] ReadEnumerableProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) is System.Collections.IEnumerable enumerable
            ? enumerable.Cast<object>().ToArray()
            : Array.Empty<object>();
    }

    private static string? ReadStringProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName)?.ToString();
    }

    private static int? ReadIntProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) as int?;
    }

    private static bool? ReadBoolProperty(object source, string propertyName)
    {
        return ReadProperty(source, propertyName) as bool?;
    }

    private static string? ResolveShopEndpointId(GameLocation location, string[] parts)
    {
        if (parts.Length == 0)
        {
            return null;
        }

        var actionType = parts[0];
        if (string.Equals(actionType, "JojaShop", StringComparison.OrdinalIgnoreCase))
        {
            return "Joja";
        }

        if (string.Equals(actionType, "Blacksmith", StringComparison.OrdinalIgnoreCase))
        {
            return "Blacksmith";
        }

        if (string.Equals(actionType, "Carpenter", StringComparison.OrdinalIgnoreCase))
        {
            return "Carpenter";
        }

        if (string.Equals(actionType, "Marnie", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionType, "AnimalShop", StringComparison.OrdinalIgnoreCase))
        {
            return "AnimalShop";
        }

        if (string.Equals(actionType, "AdventureGuild", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actionType, "AdventureShop", StringComparison.OrdinalIgnoreCase))
        {
            return "AdventureShop";
        }

        var rawShopId = Part(parts, 1);
        if (string.Equals(actionType, "Buy", StringComparison.OrdinalIgnoreCase))
        {
            return ShopIdResolver.ResolveLegacyBuy(location, rawShopId);
        }

        return rawShopId;
    }

    private static string? Part(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length ? parts[index] : null;
    }

    private static int? ParseIntPart(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length && int.TryParse(parts[index], out var value)
            ? value
            : null;
    }

    private static object? ReadProperty(object source, string propertyName)
    {
        var type = source.GetType();
        return type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(source) ??
            type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(source);
    }
}
