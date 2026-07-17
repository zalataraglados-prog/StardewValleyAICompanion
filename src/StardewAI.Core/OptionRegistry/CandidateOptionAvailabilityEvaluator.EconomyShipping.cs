using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static EconomicCandidate[] EconomicCandidates(SnapshotEnvelope snapshot, string optionId)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuyCandidates(snapshot);
            }

            if (optionId == "economy.sell_items")
            {
                return SellCandidates(snapshot);
            }

            if (optionId == "economy.ship_items")
            {
                return Array.Empty<EconomicCandidate>();
            }

            return Array.Empty<EconomicCandidate>();
        }

        private static string[] ValueGateBlockingReasons(SnapshotEnvelope snapshot, string optionId, EconomicCandidate[] economicCandidates)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuySuppliesValueBlockReasons(snapshot, economicCandidates);
            }

            if (optionId == "economy.sell_items")
            {
                return SellItemsValueBlockReasons(snapshot, economicCandidates);
            }

            if (optionId == "economy.ship_items")
            {
                return ShipItemsValueBlockReasons(snapshot);
            }

            return Array.Empty<string>();
        }

        private static string[] BuySuppliesValueBlockReasons(SnapshotEnvelope snapshot, EconomicCandidate[] candidates)
        {
            if (candidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue || shopStock.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { "menus_shop_stock_unavailable" };
            }

            if (ReadBool(shopStock.Value, "read_only") == true)
            {
                return new[] { "shop_menu_read_only" };
            }

            if (!shopStock.Value.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return new[] { "shop_stock_empty", "no_value_available_purchase_candidates" };
            }

            return (candidates.Length == 0
                    ? new[] { "shop_stock_empty" }
                    : candidates.SelectMany(candidate => candidate.BlockReasons))
                .Concat(new[] { "no_value_available_purchase_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] SellItemsValueBlockReasons(SnapshotEnvelope snapshot, EconomicCandidate[] candidates)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "player_inventory_unavailable" };
            }

            if (candidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return (candidates.Length == 0
                    ? new[] { "inventory_empty" }
                    : candidates.SelectMany(candidate => candidate.BlockReasons))
                .Concat(new[] { "no_value_available_sell_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ShipItemsValueBlockReasons(SnapshotEnvelope snapshot)
        {
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!HasCompletedShippingBin(shippingBins))
            {
                return new[] { "no_completed_shipping_bin" };
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "player_inventory_unavailable" };
            }

            return Array.Empty<string>();
        }

        private static EconomicCandidate[] BuyCandidates(SnapshotEnvelope snapshot)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return BuyCandidatesFromShopPreview(snapshot);
            }

            var shopId = ReadString(shopStock.Value, "shop_id");
            return entries.EnumerateArray()
                .Select((entry, index) =>
                {
                    var blockReasons = BuyEntryBlockReasons(entry);
                    var price = ReadInt(entry, "price");
                    return new EconomicCandidate
                    {
                        CandidateId = "buy:" + index,
                        Kind = "buy_shop_item",
                        Available = blockReasons.Length == 0,
                        ItemId = ReadString(entry, "item_id"),
                        QualifiedItemId = ReadString(entry, "qualified_item_id"),
                        DisplayName = ReadString(entry, "display_name"),
                        ShopId = shopId,
                        Quantity = 1,
                        UnitPrice = price,
                        TotalValue = price,
                        CurrencyBalance = ReadInt(entry, "currency_balance"),
                        Stock = ReadInt(entry, "stock"),
                        InfiniteStock = ReadBool(entry, "infinite_stock") == true,
                        BlockReasons = blockReasons
                    };
                })
                .ToArray();
        }

        private static EconomicCandidate[] BuyCandidatesFromShopPreview(SnapshotEnvelope snapshot)
        {
            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (!shops.HasValue ||
                shops.Value.ValueKind != JsonValueKind.Object ||
                !shops.Value.TryGetProperty("shops", out var shopArray) ||
                shopArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            return shopArray.EnumerateArray()
                .Where(shop => shop.ValueKind == JsonValueKind.Object)
                .SelectMany(shop =>
                {
                    var shopId = ReadString(shop, "shop_id");
                    if (!shop.TryGetProperty("stock_preview", out var preview) ||
                        preview.ValueKind != JsonValueKind.Object ||
                        !preview.TryGetProperty("entries", out var previewEntries) ||
                        previewEntries.ValueKind != JsonValueKind.Array)
                    {
                        return Enumerable.Empty<EconomicCandidate>();
                    }

                    return previewEntries.EnumerateArray()
                        .Where(entry => entry.ValueKind == JsonValueKind.Object)
                        .Select((entry, index) =>
                        {
                            var blockReasons = ReadStringArray(entry, "executor_block_reasons");
                            var price = ReadInt(entry, "price");
                            return new EconomicCandidate
                            {
                                CandidateId = "buy-preview:" + shopId + ":" + index,
                                Kind = "buy_shop_item",
                                Available = blockReasons.Length == 0 && ReadBool(entry, "executor_purchase_preview_enabled") == true,
                                ItemId = ReadString(entry, "item_id"),
                                QualifiedItemId = ReadString(entry, "qualified_item_id"),
                                DisplayName = ReadString(entry, "display_name"),
                                ShopId = shopId,
                                Quantity = 1,
                                UnitPrice = price,
                                TotalValue = price,
                                CurrencyBalance = ReadInt(entry, "currency_balance"),
                                Stock = ReadInt(entry, "stock"),
                                InfiniteStock = ReadBool(entry, "infinite_stock") == true,
                                BlockReasons = blockReasons
                            };
                        });
                })
                .ToArray();
        }

        private static string[] BuyEntryBlockReasons(JsonElement entry)
        {
            var reasons = new List<string>();
            if (ReadBool(entry, "can_buy_item") != true) reasons.Add("shop_item_cannot_be_bought");
            if (ReadBool(entry, "infinite_stock") != true && ReadInt(entry, "stock") <= 0) reasons.Add("shop_item_out_of_stock");
            if (ReadBool(entry, "can_afford_one_with_currency") != true) reasons.Add("insufficient_currency_for_purchase");
            if (ReadBool(entry, "can_afford_one_with_trade_item") != true) reasons.Add("insufficient_trade_item_for_purchase");
            if (ReadBool(entry, "could_inventory_accept") != true) reasons.Add("inventory_cannot_accept_purchase");
            return reasons.ToArray();
        }

        private static EconomicCandidate[] SellCandidates(SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            var sellContext = ReadStateFieldValue(snapshot, "menus", "sell_context");
            var shopSellAvailable = sellContext.HasValue &&
                sellContext.Value.ValueKind == JsonValueKind.Object &&
                ReadBool(sellContext.Value, "read_only") != true &&
                ReadBool(sellContext.Value, "held_item_present") != true &&
                ReadInt(sellContext.Value, "safety_timer") <= 0;
            var categories = ReadIntArray(sellContext, "categories_to_sell");

            return inventory.Value.EnumerateArray()
                .Where(item => ReadBool(item, "is_empty") != true)
                .Select(item =>
                {
                    var blockReasons = SellItemBlockReasons(item, shopSellAvailable, categories);
                    var stack = Math.Max(1, ReadInt(item, "stack"));
                    var sellToStorePrice = ReadInt(item, "sell_to_store_price");
                    var canShopSell = shopSellAvailable && sellToStorePrice > 0 && CategoryAccepted(item, categories);
                    return new EconomicCandidate
                    {
                        CandidateId = "sell:" + ReadInt(item, "slot_index"),
                        Kind = "sell_shop_item",
                        Available = blockReasons.Length == 0,
                        ItemId = ReadString(item, "item_id"),
                        QualifiedItemId = ReadString(item, "qualified_item_id"),
                        DisplayName = ReadString(item, "display_name"),
                        SlotIndex = ReadInt(item, "slot_index"),
                        Quantity = stack,
                        UnitPrice = sellToStorePrice,
                        TotalValue = sellToStorePrice * stack,
                        CanShopSell = canShopSell,
                        BlockReasons = blockReasons
                    };
                })
                .ToArray();
        }

        private EventCandidate[] ShipCandidates(SnapshotEnvelope snapshot)
        {
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!HasCompletedShippingBin(shippingBins))
            {
                return Array.Empty<EventCandidate>();
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var fullShipmentIndex = ReadFullShipmentIndex(snapshot);
            var binBounds = SelectShippingBinTile(shippingBins);
            if (binBounds is null)
            {
                return Array.Empty<EventCandidate>();
            }

            var binContents = ReadShippingBinContents(snapshot);

            return inventory.Value.EnumerateArray()
                .Where(item => ReadBool(item, "is_empty") != true)
                .Where(item => ReadBool(item, "can_be_shipped") == true && ReadInt(item, "sale_price") > 0 && ReadInt(item, "stack") > 0)
                .Where(item => ReadBool(item, "protected_from_auto_sell") != true && !HasArrayItems(item, "auto_sell_protection_reasons"))
                .Select(item => ShipCandidateForItem(snapshot, item, fullShipmentIndex, binBounds, binContents)!)
                .ToArray();
        }

        private EventCandidate ShipCandidateForItem(
            SnapshotEnvelope snapshot,
            JsonElement item,
            IReadOnlyDictionary<string, FullShipmentItemIndexEntry>? fullShipmentIndex,
            ShippingBinTile binBounds,
            IReadOnlyDictionary<string, int> binContents)
        {
            var blockReasons = new List<string>();
            var locationId = "Farm";

            var itemId = ReadString(item, "item_id");
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            var slotIndex = ReadInt(item, "slot_index");
            var stack = Math.Max(1, ReadInt(item, "stack"));
            var salePrice = ReadInt(item, "sale_price");

            var fullShipmentContributes = false;
            var fullShipmentKnown = false;
            var fullShipmentEligible = false;
            var fullShipmentCurrentShippedCount = 0;
            var fullShipmentAlreadyShipped = false;

            if (fullShipmentIndex != null)
            {
                if (fullShipmentIndex.TryGetValue(itemId, out var fsEntry))
                {
                    fullShipmentKnown = true;
                    fullShipmentEligible = true;
                    fullShipmentCurrentShippedCount = fsEntry.CurrentShippedCount;
                    fullShipmentAlreadyShipped = fsEntry.Shipped;
                    fullShipmentContributes = !fsEntry.Shipped && blockReasons.Count == 0;
                }
                else
                {
                    fullShipmentKnown = true;
                    fullShipmentEligible = false;
                }
            }

            var quantity = fullShipmentContributes ? 1 : stack;
            var availableStack = stack;

            var standTile = ReadBinStandTile(snapshot, binBounds);
            CandidateTile? routeTarget = standTile;
            if (routeTarget is null)
            {
                blockReasons.Add("shipping_bin_no_transparent_interaction_stand_tile");
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var distance = routeTarget is not null
                ? Math.Abs(playerX - routeTarget.X) + Math.Abs(playerY - routeTarget.Y)
                : 0;
            var estimatedTicks = Math.Max(60, distance * 60 + 30);

            var currentBinCount = binContents.TryGetValue(qualifiedItemId, out var binCount) ? binCount : 0;

            var effect = "executor_kind=ship_inventory_item_to_bin" +
                ";qualified_item_id=" + qualifiedItemId +
                ";item_id=" + itemId +
                ";slot_index=" + slotIndex +
                ";quantity=" + quantity +
                ";available_stack=" + availableStack +
                ";sale_price=" + salePrice +
                ";total_shipping_value=" + (salePrice * quantity) +
                ";shipping_bin_tile=" + binBounds.TileX + "," + binBounds.TileY +
                ";shipping_bin_width=" + binBounds.Width + ",height=" + binBounds.Height +
                (routeTarget is not null
                    ? ";route_stand_tile=" + routeTarget.X + "," + routeTarget.Y
                    : ";route_stand_tile=blocked") +
                ";bin_location=" + locationId +
                ";bin_current_count_of_item=" + currentBinCount +
                ";full_shipment_known=" + fullShipmentKnown.ToString().ToLowerInvariant() +
                ";full_shipment_eligible=" + fullShipmentEligible.ToString().ToLowerInvariant() +
                ";full_shipment_current_shipped_count=" + fullShipmentCurrentShippedCount +
                ";full_shipment_already_shipped=" + fullShipmentAlreadyShipped.ToString().ToLowerInvariant() +
                ";full_shipment_contributes=" + fullShipmentContributes.ToString().ToLowerInvariant() +
                ";shipping_executor_status=runtime_verified";

            return new EventCandidate
            {
                CandidateId = "ship:" + locationId + ":" + binBounds.TileX + "," + binBounds.TileY + ":" + slotIndex + ":" + itemId,
                Kind = "ship_inventory_item_to_bin",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = routeTarget?.X,
                TileY = routeTarget?.Y,
                ExpectedEffect = effect,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = quantity,
                ShopId = "ShippingBin",
                EstimatedTicks = estimatedTicks,
                EnergyCost = 0,
                AvailabilityClass = "transparent_shipping_bin",
                FullShipmentKnown = fullShipmentKnown,
                FullShipmentEligible = fullShipmentEligible,
                FullShipmentCurrentShippedCount = fullShipmentCurrentShippedCount,
                FullShipmentAlreadyShipped = fullShipmentAlreadyShipped,
                FullShipmentContributes = fullShipmentContributes,
                AvailableStack = availableStack,
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("slot_index", slotIndex.ToString()),
                    Parameter("item_id", itemId),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("quantity", quantity.ToString()),
                    Parameter("available_stack", availableStack.ToString()),
                    Parameter("sale_price", salePrice.ToString()),
                    Parameter("bin_tile_x", binBounds.TileX.ToString()),
                    Parameter("bin_tile_y", binBounds.TileY.ToString()),
                    Parameter("route_stand_tile_x", (routeTarget?.X).ToString() ?? string.Empty),
                    Parameter("route_stand_tile_y", (routeTarget?.Y).ToString() ?? string.Empty),
                    Parameter("bin_location", locationId),
                    Parameter("full_shipment_known", fullShipmentKnown.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_eligible", fullShipmentEligible.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_current_shipped_count", fullShipmentCurrentShippedCount.ToString()),
                    Parameter("full_shipment_already_shipped", fullShipmentAlreadyShipped.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_contributes", fullShipmentContributes.ToString().ToLowerInvariant()),
                    Parameter("bin_current_count_of_item", currentBinCount.ToString()),
                    Parameter("shipping_executor_available", "runtime_verified")
                }
            };
        }

        private static CandidateTile? ReadBinStandTile(SnapshotEnvelope snapshot, ShippingBinTile bin)
        {
            if (bin.StandX.HasValue && bin.StandY.HasValue)
            {
                return new CandidateTile(bin.StandX.Value, bin.StandY.Value);
            }

            return null;
        }

        private static IReadOnlyDictionary<string, int> ReadShippingBinContents(SnapshotEnvelope snapshot)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var bin in shippingBins.Value.EnumerateArray())
            {
                if (!bin.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    var qualifiedId = ReadString(content, "qualified_item_id");
                    var count = ReadInt(content, "count");
                    if (!string.IsNullOrWhiteSpace(qualifiedId) && count > 0)
                    {
                        result[qualifiedId] = result.TryGetValue(qualifiedId, out var current)
                            ? current + count
                            : count;
                    }
                }
            }

            return result;
        }

        private static string[] SellItemBlockReasons(JsonElement item, bool shopSellAvailable, int[] categories)
        {
            var reasons = new List<string>();
            if (ReadBool(item, "protected_from_auto_sell") == true || HasArrayItems(item, "auto_sell_protection_reasons"))
            {
                reasons.Add("inventory_item_protected_from_auto_sell");
            }

            var stack = ReadInt(item, "stack");
            var sellPrice = ReadInt(item, "sell_to_store_price");
            if (stack <= 0 || sellPrice <= 0)
            {
                reasons.Add("non_positive_sell_price");
            }

            var canShopSell = shopSellAvailable && sellPrice > 0 && CategoryAccepted(item, categories);
            if (!canShopSell)
            {
                if (!shopSellAvailable) reasons.Add("menus_sell_context_unavailable");
                if (shopSellAvailable && !CategoryAccepted(item, categories)) reasons.Add("item_not_accepted_by_active_shop");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool HasUsableShippingBin(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return shippingBins.Value.EnumerateArray().Any(bin =>
                ReadInt(bin, "days_of_construction_left") <= 0);
        }

        private static bool HasCompletedShippingBin(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return shippingBins.Value.EnumerateArray().Any(bin =>
                ReadInt(bin, "days_of_construction_left") <= 0);
        }

        private static ShippingBinTile? SelectShippingBinTile(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var bin in shippingBins.Value.EnumerateArray())
            {
                if (ReadInt(bin, "days_of_construction_left") <= 0)
                {
                    var tileX = ReadInt(bin, "tile_x");
                    var tileY = ReadInt(bin, "tile_y");
                    var width = Math.Max(1, ReadInt(bin, "tile_width"));
                    if (width <= 0) width = ReadInt(bin, "tiles_wide");
                    if (width <= 0) width = 2;
                    var height = Math.Max(1, ReadInt(bin, "tile_height"));
                    if (height <= 0) height = ReadInt(bin, "tiles_high");
                    if (height <= 0) height = 1;
                    var standX = NullableReadInt(bin, "interaction_stand_tile_x");
                    var standY = NullableReadInt(bin, "interaction_stand_tile_y");
                    return new ShippingBinTile(tileX, tileY, width, height, standX, standY);
                }
            }

            return null;
        }

        private sealed class ShippingBinTile
        {
            public ShippingBinTile(int tileX, int tileY, int width, int height, int? standX, int? standY)
            {
                TileX = tileX;
                TileY = tileY;
                Width = width;
                Height = height;
                StandX = standX;
                StandY = standY;
            }

            public int TileX { get; }
            public int TileY { get; }
            public int Width { get; }
            public int Height { get; }
            public int? StandX { get; }
            public int? StandY { get; }
        }

        private static int[] ReadIntArray(JsonElement? parent, string propertyName)
        {
            if (!parent.HasValue ||
                parent.Value.ValueKind != JsonValueKind.Object ||
                !parent.Value.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int>();
            }

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Number)
                .Select(item => item.GetInt32())
                .ToArray();
        }

        private static string[] ReadStringArray(JsonElement parent, string propertyName)
        {
            if (parent.ValueKind != JsonValueKind.Object ||
                !parent.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static bool CategoryAccepted(JsonElement item, int[] categories)
        {
            return categories.Length == 0 || categories.Contains(ReadInt(item, "category"));
        }

        private static bool HasArrayItems(JsonElement value, string propertyName)
        {
            return value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty(propertyName, out var array) &&
                array.ValueKind == JsonValueKind.Array &&
                array.GetArrayLength() > 0;
        }

        private static bool? ReadBool(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static bool HasNumber(JsonElement value, string propertyName)
        {
            return value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Number;
        }

    }
}
