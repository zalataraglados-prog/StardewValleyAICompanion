using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private TrainingExecutionResult ExecuteBuyShopItem(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), reasons.ToArray());
        }

        if (Game1.activeClickableMenu is not ShopMenu menu)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_not_open");
        }

        var quantity = request.Quantity ?? 1;
        if (quantity != 1)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "quantity_one_required_for_safe_purchase_slice");
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedShopId) &&
            !string.Equals(menu.ShopId, request.ExpectedShopId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_id_mismatch");
        }

        if (menu.readOnly)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_read_only");
        }

        if (menu.safetyTimer > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_safety_timer_active");
        }

        if (menu.heldItem is not null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_menu_held_item_present");
        }

        if (menu.currency != 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "non_money_currency_purchase_requires_audit");
        }

        if (menu.onPurchase is not null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_on_purchase_callback_present");
        }

        var match = menu.itemPriceAndStock
            .FirstOrDefault(entry =>
                (string.IsNullOrWhiteSpace(request.QualifiedItemId) || string.Equals(entry.Key.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(request.ShopItemId) || string.Equals(entry.Key is Item item ? item.ItemId : entry.Key.QualifiedItemId, request.ShopItemId, StringComparison.OrdinalIgnoreCase)));
        if (match.Key is null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "shop_item_not_found");
        }

        var salable = match.Key;
        var stock = match.Value;
        var blockReasons = SafePurchaseBlockReasons(menu, salable, stock, request);
        if (blockReasons.Length > 0)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), blockReasons);
        }

        var itemToAdd = salable.GetSalableInstance() as Item;
        if (itemToAdd is null)
        {
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "salable_instance_not_item");
        }

        itemToAdd.Stack = quantity;
        var qualifiedItemId = itemToAdd.QualifiedItemId;
        var beforeMoney = Game1.player.Money;
        var beforeCount = CountInventoryItems(qualifiedItemId);
        var beforeStock = stock.Stock;
        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.player.Money -= stock.Price * quantity;
        var accepted = Game1.player.addItemToInventoryBool(itemToAdd);
        if (!accepted)
        {
            Game1.player.Money = beforeMoney;
            return BlockedWithPrimitive(request, "buy_shop_item", BuyShopItemRequestedEffect(request), BuyShopItemObservedEffect(), "inventory_acceptance_failed_after_precheck");
        }

        if (stock.Stock != ShopMenu.infiniteStock)
        {
            stock.Stock = Math.Max(0, stock.Stock - quantity);
        }

        var afterMoney = Game1.player.Money;
        var afterCount = CountInventoryItems(qualifiedItemId);
        var afterStock = stock.Stock;
        var verified = afterMoney == beforeMoney - stock.Price * quantity && afterCount >= beforeCount + quantity;
        var verificationReasons = verified
            ? new[] { "money_decreased_by_price", "inventory_count_increased" }
            : new[] { "purchase_post_state_mismatch" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "buy_shop_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = BuyShopItemRequestedEffect(request),
            ObservedEffect = BuyShopItemObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = beforeMoney.ToString(), After = afterMoney.ToString() },
                new SimulatedFactChange { Path = "player.inventory." + qualifiedItemId + ".count", Before = beforeCount.ToString(), After = afterCount.ToString() },
                new SimulatedFactChange { Path = "menus.shop_stock." + qualifiedItemId + ".stock", Before = beforeStock.ToString(), After = afterStock.ToString() }
            }
        };
    }

    private static string[] SafePurchaseBlockReasons(ShopMenu menu, ISalable salable, ItemStockInformation stock, TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (salable.IsRecipe)
        {
            reasons.Add("recipe_purchase_discards_item_and_learns_recipe");
        }

        if (salable.GetType() != typeof(StardewValley.Object))
        {
            reasons.Add("non_plain_object_purchase_side_effects_unmodeled");
        }

        if (stock.TradeItem is not null)
        {
            reasons.Add("trade_item_purchase_requires_consumption_audit");
        }

        if (stock.ActionsOnPurchase?.Count > 0)
        {
            reasons.Add("actions_on_purchase_present");
        }

        if (stock.Stock != ShopMenu.infiniteStock && (stock.LimitedStockMode.ToString() != "None" || stock.SyncedKey is not null))
        {
            reasons.Add("synchronized_or_limited_stock_requires_post_state_audit");
        }

        if (stock.Stock != ShopMenu.infiniteStock && stock.Stock < 1)
        {
            reasons.Add("shop_item_out_of_stock");
        }

        if (request.MaxUnitPrice.HasValue && stock.Price > request.MaxUnitPrice.Value)
        {
            reasons.Add("purchase_price_exceeds_request_limit");
        }

        if (Game1.player.Money < stock.Price)
        {
            reasons.Add("insufficient_currency_for_purchase");
        }

        var itemToAdd = salable.GetSalableInstance() as Item;
        if (itemToAdd is null)
        {
            reasons.Add("salable_instance_not_item");
        }
        else if (!Game1.player.couldInventoryAcceptThisItem(itemToAdd))
        {
            reasons.Add("inventory_cannot_accept_purchase");
        }

        if (!salable.CanBuyItem(Game1.player))
        {
            reasons.Add("shop_item_cannot_be_bought");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int CountInventoryItems(string qualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item?.Stack ?? 0);
    }

    private static string BuyShopItemRequestedEffect(TrainingExecutionRequest request)
    {
        return "shop_id=" + (string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "any" : request.ExpectedShopId) +
            ";qualified_item_id=" + (string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "missing" : request.QualifiedItemId) +
            ";shop_item_id=" + (string.IsNullOrWhiteSpace(request.ShopItemId) ? "missing" : request.ShopItemId) +
            ";quantity=" + (request.Quantity?.ToString() ?? "1") +
            ";max_unit_price=" + (request.MaxUnitPrice?.ToString() ?? "unset");
    }

    private static string BuyShopItemObservedEffect()
    {
        return "menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";shop_id=" + (Game1.activeClickableMenu is ShopMenu menu ? menu.ShopId : "none") +
            ";money=" + Game1.player.Money;
    }

    private static string InteractRequestedEffect(TrainingExecutionRequest request)
    {
        return "interact.kind=" + (string.IsNullOrWhiteSpace(request.InteractionKind) ? "missing" : request.InteractionKind) +
            ";target_tile=" + (request.TargetTileX.HasValue && request.TargetTileY.HasValue ? request.TargetTileX.Value + "," + request.TargetTileY.Value : "missing") +
            ";expected_action_type=" + (string.IsNullOrWhiteSpace(request.ExpectedActionType) ? "missing" : request.ExpectedActionType);
    }

    private static string InteractObservedEffect()
    {
        return "menus.active_menu.is_open=" + (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() +
            ";menus.active_menu.type=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
            ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + (Game1.player?.TilePoint.X.ToString() ?? "missing") + "," + (Game1.player?.TilePoint.Y.ToString() ?? "missing");
    }

    private static bool IsSafeCloseMenuType(string type)
    {
        return type is "GameMenu" or "InventoryMenu" or "QuestLog" or "MapPage" or "ProfileMenu" or "ShopMenu";
    }
}
