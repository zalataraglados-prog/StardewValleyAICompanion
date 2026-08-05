using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] SellItemStageCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            if (ActiveShopMenuOpen(snapshot))
            {
                return Array.Empty<EventCandidate>();
            }

            var previews = SellCandidatesFromShopPreview(snapshot)
                .Where(candidate => SaleIdentityMatches(candidate, boundParameters))
                .ToArray();
            return ShopObjectiveStageCandidates(
                snapshot,
                previews,
                "sale",
                SaleContinuationParameters);
        }

        private static EconomicCandidate[] SellCandidatesFromShopPreview(
            SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array ||
                !shops.HasValue || shops.Value.ValueKind != JsonValueKind.Object ||
                !shops.Value.TryGetProperty("shops", out var shopArray) ||
                shopArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            var items = inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Where(item => ReadBool(item, "is_empty") != true)
                .ToArray();
            var results = new List<EconomicCandidate>();
            foreach (var shop in shopArray.EnumerateArray()
                         .Where(shop => shop.ValueKind == JsonValueKind.Object))
            {
                if (!shop.TryGetProperty("sale_preview", out var salePreview) ||
                    salePreview.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var shopId = ReadString(shop, "shop_id");
                var tagGroups = ReadNestedStringArrays(
                    salePreview,
                    "tag_groups_to_sell");
                var previewReasons = ReadStringArray(
                    salePreview,
                    "executor_block_reasons");
                var previewEnabled = ReadBool(
                    salePreview,
                    "executor_sale_preview_enabled") == true;
                var sellPercentage = ReadDouble(
                    salePreview,
                    "default_sell_percentage");

                foreach (var item in items)
                {
                    var accepted = ShopAcceptsItem(
                        item,
                        Array.Empty<int>(),
                        tagGroups);
                    var reasons = new List<string>(previewReasons);
                    if (!previewEnabled)
                    {
                        reasons.Add("shop_sale_preview_not_enabled");
                    }
                    if (!accepted)
                    {
                        reasons.Add("item_not_accepted_by_shop_preview");
                    }
                    if (ReadBool(item, "protected_from_auto_sell") == true ||
                        HasArrayItems(item, "auto_sell_protection_reasons"))
                    {
                        reasons.Add("inventory_item_protected_from_auto_sell");
                    }

                    var stack = ReadInt(item, "stack");
                    var unitPrice = (int)(ReadInt(
                        item,
                        "sell_to_store_price") * sellPercentage);
                    if (stack <= 0 || unitPrice <= 0)
                    {
                        reasons.Add("non_positive_sell_price");
                    }

                    results.Add(new EconomicCandidate
                    {
                        CandidateId = "sell-preview:" + shopId + ":" +
                            ReadInt(item, "slot_index").ToString(
                                CultureInfo.InvariantCulture),
                        Kind = "sell_shop_item",
                        Available = reasons.Count == 0,
                        ItemId = ReadString(item, "item_id"),
                        QualifiedItemId = ReadString(
                            item,
                            "qualified_item_id"),
                        DisplayName = ReadString(item, "display_name"),
                        ShopId = shopId,
                        SlotIndex = ReadInt(item, "slot_index"),
                        Quantity = Math.Max(1, stack),
                        UnitPrice = unitPrice,
                        TotalValue = unitPrice * Math.Max(1, stack),
                        CanShopSell = reasons.Count == 0,
                        BlockReasons = reasons
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    });
                }
            }

            return results.ToArray();
        }

        private static bool SaleIdentityMatches(
            EconomicCandidate candidate,
            SmallModelActionParameter[] boundParameters)
        {
            var shopId = ReadParameter(boundParameters, "continuation.shop_id");
            var qualifiedItemId = ReadParameter(
                boundParameters,
                "continuation.qualified_item_id");
            var slotIndex = ReadParameterInt(
                boundParameters,
                "continuation.slot_index");
            var quantity = ReadParameterInt(
                boundParameters,
                "continuation.quantity");
            var expectedUnitPrice = ReadParameterInt(
                boundParameters,
                "continuation.expected_unit_price");
            return (string.IsNullOrWhiteSpace(shopId) ||
                    string.Equals(
                        candidate.ShopId,
                        shopId,
                        StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    string.Equals(
                        candidate.QualifiedItemId,
                        qualifiedItemId,
                        StringComparison.Ordinal)) &&
                (!slotIndex.HasValue || candidate.SlotIndex == slotIndex) &&
                (!quantity.HasValue || candidate.Quantity == quantity) &&
                (!expectedUnitPrice.HasValue ||
                    candidate.UnitPrice == expectedUnitPrice);
        }

        private static SmallModelActionParameter[] SaleContinuationParameters(
            EconomicCandidate candidate,
            string targetLocation)
        {
            return new[]
            {
                Parameter("continuation.option_id", "economy.sell_items"),
                Parameter("continuation.shop_id", candidate.ShopId),
                Parameter("continuation.target_location", targetLocation),
                Parameter("continuation.item_id", candidate.ItemId),
                Parameter(
                    "continuation.qualified_item_id",
                    candidate.QualifiedItemId),
                Parameter(
                    "continuation.slot_index",
                    candidate.SlotIndex?.ToString(CultureInfo.InvariantCulture) ?? ""),
                Parameter(
                    "continuation.quantity",
                    candidate.Quantity.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "continuation.expected_unit_price",
                    candidate.UnitPrice.ToString(CultureInfo.InvariantCulture))
            };
        }

        private static bool IsSaleContinuationCandidate(
            OptionAvailabilityCandidate candidate)
        {
            return string.Equals(
                    candidate.OptionId,
                    "economy.sell_items",
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(ReadParameter(
                    candidate.Parameters,
                    "continuation.shop_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(
                    candidate.Parameters,
                    "continuation.qualified_item_id")) &&
                ReadParameterInt(
                    candidate.Parameters,
                    "continuation.slot_index").HasValue;
        }
    }
}
