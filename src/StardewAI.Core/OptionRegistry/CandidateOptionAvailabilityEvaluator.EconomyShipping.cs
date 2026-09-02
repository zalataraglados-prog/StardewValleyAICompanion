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
        private static EconomicCandidate[] EconomicCandidates(
            SnapshotEnvelope snapshot,
            string optionId,
            SmallModelActionParameter[] boundParameters)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuyCandidates(snapshot, boundParameters);
            }

            if (optionId == "economy.sell_items")
            {
                return ActiveShopMenuOpen(snapshot)
                    ? SellCandidates(snapshot, boundParameters)
                    : Array.Empty<EconomicCandidate>();
            }

            if (optionId == "economy.ship_items")
            {
                return Array.Empty<EconomicCandidate>();
            }

            return Array.Empty<EconomicCandidate>();
        }

        private static string[] ValueGateBlockingReasons(
            SnapshotEnvelope snapshot,
            string optionId,
            EconomicCandidate[] economicCandidates,
            EventCandidate[] eventCandidates)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuySuppliesValueBlockReasons(snapshot, economicCandidates, eventCandidates);
            }

            if (optionId == "economy.sell_items")
            {
                return SellItemsValueBlockReasons(
                    snapshot,
                    economicCandidates,
                    eventCandidates);
            }

            if (optionId == "economy.ship_items")
            {
                return ShipItemsValueBlockReasons(snapshot);
            }

            return Array.Empty<string>();
        }

        private static string[] BuySuppliesValueBlockReasons(
            SnapshotEnvelope snapshot,
            EconomicCandidate[] candidates,
            EventCandidate[] eventCandidates)
        {
            if (candidates.Any(candidate => candidate.Available) ||
                eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            if (eventCandidates.Length > 0)
            {
                return eventCandidates
                    .SelectMany(candidate => candidate.BlockReasons)
                    .Concat(new[] { "no_value_available_purchase_candidates" })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
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

        private static string[] SellItemsValueBlockReasons(
            SnapshotEnvelope snapshot,
            EconomicCandidate[] candidates,
            EventCandidate[] eventCandidates)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "player_inventory_unavailable" };
            }

            if (candidates.Any(candidate => candidate.Available) ||
                eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            if (eventCandidates.Length > 0)
            {
                return eventCandidates
                    .SelectMany(candidate => candidate.BlockReasons)
                    .Concat(new[] { "no_value_available_sell_candidates" })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
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

        private static EconomicCandidate[] BuyCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            var shopId = ReadString(shopStock.Value, "shop_id");
            var continuationShopId = ReadParameter(boundParameters, "continuation.shop_id");
            var continuationQualifiedItemId = ReadParameter(
                boundParameters,
                "continuation.qualified_item_id");
            var continuationMaxUnitPrice = ReadParameterInt(
                boundParameters,
                "continuation.max_unit_price");
            var continuationParameters = PurchaseContinuationParameters(
                boundParameters);
            var safetyWaitTicks = ShopMenuSafetyWaitTicks(shopStock.Value);
            var targetLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            return entries.EnumerateArray()
                .Select((entry, index) =>
                {
                    var blockReasons = new List<string>(
                        BuyEntryBlockReasons(entry));
                    if (!string.IsNullOrWhiteSpace(continuationShopId) &&
                        !string.Equals(
                            shopId,
                            continuationShopId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        blockReasons.Add(
                            "active_shop_does_not_match_purchase_continuation");
                    }
                    if (!string.IsNullOrWhiteSpace(
                            continuationQualifiedItemId) &&
                        !string.Equals(
                            ReadString(entry, "qualified_item_id"),
                            continuationQualifiedItemId,
                            StringComparison.Ordinal))
                    {
                        blockReasons.Add(
                            "shop_item_does_not_match_purchase_continuation");
                    }
                    var price = ReadInt(entry, "price");
                    if (continuationMaxUnitPrice.HasValue &&
                        price > continuationMaxUnitPrice.Value)
                    {
                        blockReasons.Add(
                            "shop_item_price_exceeds_purchase_continuation");
                    }
                    var candidate = new EconomicCandidate
                    {
                        CandidateId = "buy:" + shopId + ":" +
                            ReadString(entry, "qualified_item_id") + ":" + index,
                        Kind = "buy_shop_item",
                        Available = blockReasons.Count == 0,
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
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    };
                    var objectiveContinuation = continuationParameters.Length > 0
                        ? continuationParameters
                        : PurchaseContinuationParameters(candidate, targetLocation);
                    candidate.Parameters = safetyWaitTicks > 0
                        ? objectiveContinuation
                            .Append(Parameter(
                                "runtime.shop_menu_safety_wait_ticks",
                                safetyWaitTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                            .ToArray()
                        : objectiveContinuation;
                    return candidate;
                })
                .ToArray();
        }

        private static int ShopMenuSafetyWaitTicks(JsonElement shopStock)
        {
            const int approximateTickMilliseconds = 16;
            const int readinessMarginTicks = 2;
            const int maxWaitTicks = 600;
            var safetyTimerMilliseconds = ReadInt(shopStock, "safety_timer");
            if (safetyTimerMilliseconds <= 0)
            {
                return 0;
            }

            var timerTicks = (safetyTimerMilliseconds + approximateTickMilliseconds - 1) /
                approximateTickMilliseconds;
            return Math.Clamp(timerTicks + readinessMarginTicks, 1, maxWaitTicks);
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

        private static EconomicCandidate[] SellCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            var sellContext = ReadStateFieldValue(snapshot, "menus", "sell_context");
            var sellContextBlockReasons = SellContextBlockReasons(sellContext);
            var shopSellAvailable = sellContextBlockReasons.Length == 0;
            var categories = ReadIntArray(sellContext, "categories_to_sell");
            var tagGroups = ReadNestedStringArrays(sellContext, "tag_groups_to_sell");
            var sellPercentage = sellContext.HasValue
                ? ReadDouble(sellContext.Value, "sell_percentage")
                : 0d;
            var shopId = sellContext.HasValue
                ? ReadString(sellContext.Value, "shop_id")
                : string.Empty;

            var continuationParameters = boundParameters
                .Where(parameter => parameter.Name.StartsWith(
                    "continuation.",
                    StringComparison.Ordinal))
                .ToArray();
            return inventory.Value.EnumerateArray()
                .Where(item => ReadBool(item, "is_empty") != true)
                .Select(item =>
                {
                    var acceptedByShop = ShopAcceptsItem(item, categories, tagGroups);
                    var blockReasons = SellItemBlockReasons(
                        item,
                        shopSellAvailable,
                        acceptedByShop,
                        sellContextBlockReasons);
                    var stack = Math.Max(1, ReadInt(item, "stack"));
                    var sellToStorePrice = (int)(ReadInt(item, "sell_to_store_price") * sellPercentage);
                    var canShopSell = shopSellAvailable && sellToStorePrice > 0 && acceptedByShop;
                    return new EconomicCandidate
                    {
                        CandidateId = "sell:" + ReadInt(item, "slot_index"),
                        Kind = "sell_shop_item",
                        Available = blockReasons.Length == 0,
                        ItemId = ReadString(item, "item_id"),
                        QualifiedItemId = ReadString(item, "qualified_item_id"),
                        DisplayName = ReadString(item, "display_name"),
                        ShopId = shopId,
                        SlotIndex = ReadInt(item, "slot_index"),
                        Quantity = stack,
                        UnitPrice = sellToStorePrice,
                        TotalValue = sellToStorePrice * stack,
                        CanShopSell = canShopSell,
                        BlockReasons = blockReasons,
                        Parameters = continuationParameters
                    };
                })
                .Where(candidate => SaleIdentityMatches(
                    candidate,
                    boundParameters))
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
                .Where(item => ReadBool(item, "can_be_shipped") == true && ReadInt(item, "sell_to_store_price") > 0 && ReadInt(item, "stack") > 0)
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
            var shippingUnitPrice = ReadInt(item, "sell_to_store_price");

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

            // The native shipping-menu primitive uses one right-click and therefore
            // transfers exactly one item. Remaining stack items are reconsidered from
            // the next transparent snapshot instead of widening executor semantics.
            const int quantity = 1;
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
                ";shipping_unit_price=" + shippingUnitPrice +
                ";total_shipping_value=" + (shippingUnitPrice * quantity) +
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
                UnitPrice = shippingUnitPrice,
                TotalValue = shippingUnitPrice * quantity,
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("slot_index", slotIndex.ToString()),
                    Parameter("item_id", itemId),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("quantity", quantity.ToString()),
                    Parameter("available_stack", availableStack.ToString()),
                    Parameter("sale_price", salePrice.ToString()),
                    Parameter("expected_unit_price", shippingUnitPrice.ToString()),
                    Parameter("shipping_unit_price", shippingUnitPrice.ToString()),
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

        private static string[] SellItemBlockReasons(
            JsonElement item,
            bool shopSellAvailable,
            bool acceptedByShop,
            string[] sellContextBlockReasons)
        {
            var reasons = new List<string>(sellContextBlockReasons);
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

            var canShopSell = shopSellAvailable && sellPrice > 0 && acceptedByShop;
            if (!canShopSell)
            {
                if (!shopSellAvailable) reasons.Add("menus_sell_context_unavailable");
                if (shopSellAvailable && !acceptedByShop) reasons.Add("item_not_accepted_by_active_shop");
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

        private static string[] SellContextBlockReasons(JsonElement? sellContext)
        {
            if (!sellContext.HasValue || sellContext.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { "menus_sell_context_unavailable" };
            }

            var reasons = new List<string>();
            var context = sellContext.Value;
            if (ReadBool(context, "read_only") != false) reasons.Add("shop_menu_read_only_or_unknown");
            if (ReadBool(context, "held_item_present") != false) reasons.Add("shop_menu_held_item_present_or_unknown");
            if (ReadInt(context, "safety_timer") > 0) reasons.Add("shop_menu_safety_timer_active");
            if (ReadInt(context, "currency") != 0) reasons.Add("non_money_shop_sale_requires_audit");
            if (ReadBool(context, "custom_on_sell_present") != false) reasons.Add("shop_custom_on_sell_requires_audit");
            if (ReadBool(context, "storage_shop") != false) reasons.Add("storage_shop_sale_requires_audit");
            if (ReadDouble(context, "sell_percentage") <= 0d) reasons.Add("shop_sell_percentage_missing_or_non_positive");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool ShopAcceptsItem(JsonElement item, int[] categories, string[][] tagGroups)
        {
            if (categories.Contains(ReadInt(item, "category")))
            {
                return true;
            }

            var contextTags = ReadStringArray(item, "context_tags").ToHashSet(StringComparer.Ordinal);
            return tagGroups.Any(group =>
                group.Length > 0 &&
                group.All(tag => contextTags.Contains(tag)));
        }

        private static string[][] ReadNestedStringArrays(JsonElement? parent, string propertyName)
        {
            if (!parent.HasValue ||
                parent.Value.ValueKind != JsonValueKind.Object ||
                !parent.Value.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string[]>();
            }

            return value.EnumerateArray()
                .Where(group => group.ValueKind == JsonValueKind.Array)
                .Select(group => group.EnumerateArray()
                    .Where(tag => tag.ValueKind == JsonValueKind.String)
                    .Select(tag => tag.GetString() ?? string.Empty)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray())
                .ToArray();
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
