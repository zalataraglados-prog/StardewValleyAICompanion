using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static MoneyRevenueCropTeacherCandidateEvaluation[]
        EvaluateCandidates(
            SnapshotEnvelope snapshot,
            int maximumTileCount,
            int viableTileCount)
    {
        var supply = ReadMaterialSupply(snapshot);
        var availableByItem = supply.Quantities.ToDictionary(
            value => value.QualifiedItemId,
            value => value.AvailableQuantity,
            StringComparer.Ordinal);
        var currentMoney = ReadStateInt(snapshot, "player", "money");
        var currentTotalDay = ReadStateInt(snapshot, "time", "total_days");
        var currentSeason = ReadStateString(snapshot, "time", "season");
        var currentDay = ReadStateInt(snapshot, "time", "day");
        var seasonEnd = checked(currentTotalDay + 28 - currentDay);
        var stock = ReadSeedStock(snapshot);
        var catalog = ReadStateValue(snapshot, "farm", "crop_catalog");
        Require(catalog.ValueKind == JsonValueKind.Array,
            "Money Revenue crop-production Teacher crop catalog is unavailable.");

        return catalog.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Object)
            .Select(crop =>
            {
                var seedId = ReadString(crop, "seed_id");
                var qualifiedSeedId = "(O)" + seedId;
                var reasons = new List<string>();
                var seasons = ReadStringArray(crop, "seasons");
                var growDays = ReadOptionalInt(crop, "grow_days") ?? 0;
                var regrowDays = ReadOptionalInt(crop, "regrow_days");
                if (regrowDays <= 0)
                    regrowDays = null;
                var minStack = ReadOptionalInt(
                    crop,
                    "harvest_min_stack") ?? 0;
                var unitPrice = ReadOptionalInt(
                    crop,
                    "harvest_unit_sale_price") ?? 0;
                var needsWatering = ReadOptionalBool(
                    crop,
                    "needs_watering") ?? true;
                if (!seasons.Contains(currentSeason,
                        StringComparer.OrdinalIgnoreCase))
                    reasons.Add("not_current_season");
                if (growDays <= 0 || checked(currentTotalDay + growDays) >
                    seasonEnd)
                    reasons.Add("cannot_mature_in_current_season");
                if (minStack <= 0 || unitPrice <= 0)
                    reasons.Add("non_positive_conservative_output");
                if (ReadOptionalBool(crop, "is_raised") == true)
                    reasons.Add("raised_crop_layout_topology_not_yet_proven");
                if (ReadOptionalBool(crop, "is_paddy_crop") == true)
                    reasons.Add("paddy_water_proximity_not_yet_proven");
                if ((ReadOptionalInt(crop,
                        "plantable_location_rule_count") ?? 0) != 0)
                    reasons.Add("special_planting_rules_not_admitted");
                if (!string.Equals(
                        ReadString(crop, "harvest_method"),
                        "Grab",
                        StringComparison.OrdinalIgnoreCase))
                    reasons.Add("non_grab_retention_path_not_admitted");

                var shopRows = stock.Where(value =>
                        value.QualifiedItemId == qualifiedSeedId)
                    .OrderBy(value => value.UnitPrice)
                    .ThenBy(value => value.ShopId, StringComparer.Ordinal)
                    .ToArray();
                var shop = shopRows.FirstOrDefault();
                var existing = availableByItem.GetValueOrDefault(
                    qualifiedSeedId);
                if (shopRows.Length == 0 && existing <= 0)
                    reasons.Add("no_current_seed_supply");
                var purchasable = shop is null || shop.UnitPrice <= 0
                    ? 0
                    : currentMoney / shop.UnitPrice;
                var tileCount = Math.Min(
                    Math.Min(maximumTileCount, viableTileCount),
                    checked(existing + purchasable));
                if (tileCount <= 0)
                    reasons.Add("no_affordable_seed_quantity");

                var firstHarvest = checked(currentTotalDay +
                    Math.Max(0, growDays));
                var waveCount = reasons.Contains(
                        "cannot_mature_in_current_season",
                        StringComparer.Ordinal)
                    ? 0
                    : regrowDays.HasValue
                        ? 1 + (seasonEnd - firstHarvest) /
                            regrowDays.Value
                        : 1;
                var lastHarvest = waveCount <= 0
                    ? firstHarvest
                    : regrowDays.HasValue
                        ? checked(firstHarvest +
                            (waveCount - 1) * regrowDays.Value)
                        : firstHarvest;
                var gross = checked((long)Math.Max(0, tileCount) *
                    Math.Max(0, waveCount) * Math.Max(0, minStack) *
                    Math.Max(0, unitPrice));
                var shortfall = Math.Max(0, tileCount - existing);
                var acquisitionCost = checked((long)shortfall *
                    (shop?.UnitPrice ?? 0));
                var wateringDays = waveCount <= 0
                    ? 0
                    : CropProductionObligationProjector.Project(
                        currentTotalDay,
                        firstHarvest,
                        lastHarvest,
                        regrowDays,
                        1,
                        minStack,
                        needsWatering,
                        unitPrice).RequiredWateringTotalDays.Length;
                return new MoneyRevenueCropTeacherCandidateEvaluation(
                    seedId,
                    qualifiedSeedId,
                    shop?.ShopId ?? string.Empty,
                    shop?.UnitPrice ?? 0,
                    existing,
                    tileCount,
                    waveCount,
                    gross,
                    acquisitionCost,
                    checked(gross - acquisitionCost),
                    lastHarvest,
                    checked(wateringDays * Math.Max(0, tileCount)),
                    reasons.Count == 0,
                    reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    false);
            })
            .OrderBy(value => value.SeedId, StringComparer.Ordinal)
            .ToArray();
    }

    private static MaterialSupplyProjectionResult ReadMaterialSupply(
        SnapshotEnvelope snapshot)
    {
        var value = ReadStateValue(
            snapshot,
            "farm",
            "material_inventory_graph");
        var graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.GetRawText(),
                JsonDefaults.Options)
            ?? throw new InvalidDataException(
                "Money Revenue crop-production Teacher material graph is empty.");
        var result = new MaterialSupplyProjection().Project(graph);
        Require(result.Status == "available" &&
                result.BlockingReasons.Length == 0,
            "Money Revenue crop-production Teacher material supply is blocked: " +
            string.Join(",", result.BlockingReasons));
        return result;
    }

    private static SeedStock[] ReadSeedStock(SnapshotEnvelope snapshot)
    {
        var admittedPurchases = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "economy.buy_supplies" })
            .Options
            .Single()
            .EventCandidates
            .Where(value => value.Available)
            .Select(value => new SeedPurchaseKey(
                value.ShopId,
                value.QualifiedItemId,
                value.UnitPrice))
            .ToHashSet();
        var root = ReadStateValue(snapshot, "locations", "shops");
        var shops = RequiredProperty(root, "shops");
        Require(shops.ValueKind == JsonValueKind.Array,
            "Money Revenue crop-production Teacher shop catalog is unavailable.");
        return shops.EnumerateArray().SelectMany(shop =>
        {
            var shopId = ReadString(shop, "shop_id");
            if (!shop.TryGetProperty("stock_preview", out var preview) ||
                !preview.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
                return Array.Empty<SeedStock>();
            return entries.EnumerateArray()
                .Where(value =>
                    ReadOptionalBool(value,
                        "executor_purchase_preview_enabled") == true &&
                    (ReadOptionalInt(value, "currency") ?? -1) == 0 &&
                    (ReadOptionalInt(value, "price") ?? 0) > 0 &&
                    ReadString(value, "qualified_item_id")
                        .StartsWith("(O)", StringComparison.Ordinal) &&
                    admittedPurchases.Contains(new SeedPurchaseKey(
                        shopId,
                        ReadString(value, "qualified_item_id"),
                        ReadInt(value, "price"))))
                .Select(value => new SeedStock(
                    shopId,
                    ReadString(value, "qualified_item_id"),
                    ReadInt(value, "price")))
                .ToArray();
        }).ToArray();
    }

    private sealed record SeedStock(
        string ShopId,
        string QualifiedItemId,
        int UnitPrice);

    private sealed record SeedPurchaseKey(
        string ShopId,
        string QualifiedItemId,
        int UnitPrice);
}
