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
        private EventCandidate[] FarmMaintenanceCandidates(SnapshotEnvelope snapshot)
        {
            return WateringCandidates(snapshot)
                .Concat(HarvestCropCandidates(snapshot))
                .Concat(HarvestGiantCropCandidates(snapshot))
                .Concat(ClearFarmResourceClumpCandidates(snapshot))
                .Concat(PickupDebrisCandidates(snapshot))
                .Concat(PlantSeedCandidates(snapshot).Select(candidate => new EventCandidate
                {
                    CandidateId = "farm-maintenance:" + candidate.CandidateId,
                    Kind = candidate.Kind,
                    Available = candidate.Available,
                    LocationId = candidate.LocationId,
                    TileX = candidate.TileX,
                    TileY = candidate.TileY,
                    ExpectedEffect = "farm_maintenance_plant_seed=true;" + candidate.ExpectedEffect,
                    ItemId = candidate.ItemId,
                    QualifiedItemId = candidate.QualifiedItemId,
                    SlotIndex = candidate.SlotIndex,
                    Quantity = candidate.Quantity,
                    ShopId = candidate.ShopId,
                    EstimatedTicks = candidate.EstimatedTicks,
                    EnergyCost = candidate.EnergyCost,
                    AvailabilityClass = candidate.AvailabilityClass,
                    AllowedNow = candidate.AllowedNow,
                    AllowedToday = candidate.AllowedToday,
                    NextOpenTime = candidate.NextOpenTime,
                    EffectiveOpenTime = candidate.EffectiveOpenTime,
                    ClosesAt = candidate.ClosesAt,
                    WaitCost = candidate.WaitCost,
                    GateReasons = candidate.GateReasons,
                    BlockReasons = candidate.BlockReasons
                }))
                .Concat(ClearObstacleCandidates(snapshot).Select(candidate => new EventCandidate
                {
                    CandidateId = "farm-maintenance:" + candidate.CandidateId,
                    Kind = candidate.Kind,
                    Available = candidate.Available,
                    LocationId = candidate.LocationId,
                    TileX = candidate.TileX,
                    TileY = candidate.TileY,
                    ExpectedEffect = "farm_maintenance_clear_obstacle=true;" + candidate.ExpectedEffect,
                    ItemId = candidate.ItemId,
                    QualifiedItemId = candidate.QualifiedItemId,
                    SlotIndex = candidate.SlotIndex,
                    Quantity = candidate.Quantity,
                    ShopId = candidate.ShopId,
                    EstimatedTicks = candidate.EstimatedTicks,
                    EnergyCost = candidate.EnergyCost,
                    AvailabilityClass = candidate.AvailabilityClass,
                    AllowedNow = candidate.AllowedNow,
                    AllowedToday = candidate.AllowedToday,
                    NextOpenTime = candidate.NextOpenTime,
                    EffectiveOpenTime = candidate.EffectiveOpenTime,
                    ClosesAt = candidate.ClosesAt,
                    WaitCost = candidate.WaitCost,
                    GateReasons = candidate.GateReasons,
                    BlockReasons = candidate.BlockReasons
                }))
                .ToArray();
        }

        private EventCandidate[] PlantSeedCandidates(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "planting_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("hoe_dirt_tiles", out var tiles) ||
                tiles.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = ReadString(context.Value, "location_id");
            }

            var seedStacks = SeedInventoryStacks(snapshot);
            var cropCatalog = CropCatalogBySeed(snapshot);
            var seedCosts = SeedUnitCosts(snapshot);
            return tiles.EnumerateArray()
                .Where(tile => tile.ValueKind == JsonValueKind.Object && HasNumber(tile, "tile_x") && HasNumber(tile, "tile_y"))
                .SelectMany(tile => PlantSeedCandidatesForTile(snapshot, tile, string.IsNullOrWhiteSpace(locationId) ? "current_location" : locationId, seedStacks, cropCatalog, seedCosts))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ThenBy(candidate => candidate.ItemId, StringComparer.Ordinal)
                .Take(64)
                .ToArray();
        }

        private IEnumerable<EventCandidate> PlantSeedCandidatesForTile(
            SnapshotEnvelope snapshot,
            JsonElement tile,
            string locationId,
            IReadOnlyDictionary<string, int> seedStacks,
            IReadOnlyDictionary<string, CropCatalogEntry> cropCatalog,
            IReadOnlyDictionary<string, int> seedCosts)
        {
            if (!tile.TryGetProperty("seed_results", out var seedResults) ||
                seedResults.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            var x = ReadInt(tile, "tile_x");
            var y = ReadInt(tile, "tile_y");
            var hasCrop = ReadBool(tile, "has_crop") == true;
            foreach (var result in seedResults.EnumerateArray().Where(result => result.ValueKind == JsonValueKind.Object))
            {
                var seedId = ReadString(result, "seed_id");
                if (string.IsNullOrWhiteSpace(seedId))
                {
                    continue;
                }

                var blockReasons = new List<string>();
                if (hasCrop)
                {
                    blockReasons.Add("plant_seed_target_already_has_crop");
                }

                if (ReadBool(result, "hard_rule_allows_planting") != true)
                {
                    blockReasons.Add("plant_seed_not_allowed_by_transparent_context");
                }

                var seedStack = seedStacks.TryGetValue(seedId, out var stack) ? stack : 0;
                if (seedStack <= 0)
                {
                    blockReasons.Add("plant_seed_inventory_seed_missing");
                }

                if (ReadBool(result, "can_mature_before_season_end_with_paddy_if_eligible") == false)
                {
                    blockReasons.Add("seed_would_not_mature_before_season_end");
                }

                blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "executor.plant_seed",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", x.ToString()),
                        Parameter("target_tile_y", y.ToString()),
                        Parameter("seed_id", seedId)
                    }
                }));

                var qualifiedItemId = "(O)" + seedId;
                var adjustedGrowDays = NullableReadInt(result, "adjusted_grow_days_with_paddy_if_eligible");
                var daysRemaining = NullableReadInt(result, "days_remaining_in_season");
                cropCatalog.TryGetValue(seedId, out var crop);
                var expectedFirstHarvestValue = crop.HarvestUnitSalePrice > 0
                    ? crop.HarvestUnitSalePrice.Value * Math.Max(1, crop.HarvestMinStack ?? 1)
                    : (int?)null;
                var estimatedFirstHarvestQuantity = EstimatedFirstHarvestQuantity(crop);
                var estimatedFirstHarvestValue = crop.HarvestUnitSalePrice > 0 && estimatedFirstHarvestQuantity.HasValue
                    ? crop.HarvestUnitSalePrice.Value * estimatedFirstHarvestQuantity.Value
                    : (double?)null;
                var seedUnitCost = seedCosts.TryGetValue(seedId, out var cost) ? cost : (int?)null;
                var conservativeNetValue = expectedFirstHarvestValue.HasValue && seedUnitCost.HasValue
                    ? expectedFirstHarvestValue.Value - seedUnitCost.Value
                    : (int?)null;
                var estimatedNetValue = estimatedFirstHarvestValue.HasValue && seedUnitCost.HasValue
                    ? estimatedFirstHarvestValue.Value - seedUnitCost.Value
                    : (double?)null;
                var regrowHarvestCount = EstimatedRegrowHarvestCount(crop, adjustedGrowDays, daysRemaining);
                var totalHarvestCount = EstimatedTotalHarvestCount(adjustedGrowDays, daysRemaining, regrowHarvestCount);
                var expectedSeasonHarvestValue = expectedFirstHarvestValue.HasValue && totalHarvestCount.HasValue
                    ? expectedFirstHarvestValue.Value * totalHarvestCount.Value
                    : (int?)null;
                var estimatedSeasonHarvestValue = estimatedFirstHarvestValue.HasValue && totalHarvestCount.HasValue
                    ? estimatedFirstHarvestValue.Value * totalHarvestCount.Value
                    : (double?)null;
                var expectedSeasonHarvestNetValue = expectedSeasonHarvestValue.HasValue && seedUnitCost.HasValue
                    ? expectedSeasonHarvestValue.Value - seedUnitCost.Value
                    : (int?)null;
                var estimatedSeasonHarvestNetValue = estimatedSeasonHarvestValue.HasValue && seedUnitCost.HasValue
                    ? estimatedSeasonHarvestValue.Value - seedUnitCost.Value
                    : (double?)null;
                yield return new EventCandidate
                {
                    CandidateId = "plant:" + locationId + ":" + x + "," + y + ":" + seedId,
                    Kind = "plant_seed_tile",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect = "current_location.planting_context[" + x + "," + y + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases;seed_id=" + seedId +
                        (adjustedGrowDays.HasValue ? ";adjusted_grow_days=" + adjustedGrowDays.Value : string.Empty) +
                        (daysRemaining.HasValue ? ";days_remaining_in_season=" + daysRemaining.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestItemId) ? ";harvest_item_id=" + crop.HarvestItemId : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestItemQualifiedId) ? ";harvest_item_qualified_id=" + crop.HarvestItemQualifiedId : string.Empty) +
                        (crop.HarvestUnitSalePrice.HasValue ? ";harvest_unit_sale_price=" + crop.HarvestUnitSalePrice.Value : string.Empty) +
                        (crop.HarvestMinStack.HasValue ? ";harvest_min_stack=" + crop.HarvestMinStack.Value : string.Empty) +
                        (crop.HarvestMaxStack.HasValue ? ";harvest_max_stack=" + crop.HarvestMaxStack.Value : string.Empty) +
                        (crop.HarvestMaxIncreasePerFarmingLevel.HasValue ? ";harvest_max_increase_per_farming_level=" + FormatNumber(crop.HarvestMaxIncreasePerFarmingLevel.Value) : string.Empty) +
                        (crop.ExtraHarvestChance.HasValue ? ";extra_harvest_chance=" + FormatNumber(crop.ExtraHarvestChance.Value) : string.Empty) +
                        (crop.HarvestMinQuality.HasValue ? ";harvest_min_quality=" + crop.HarvestMinQuality.Value : string.Empty) +
                        (crop.HarvestMaxQuality.HasValue ? ";harvest_max_quality=" + crop.HarvestMaxQuality.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestMethod) ? ";harvest_method=" + crop.HarvestMethod : string.Empty) +
                        (crop.RegrowDays.HasValue ? ";regrow_days=" + crop.RegrowDays.Value : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_value=" + expectedFirstHarvestValue.Value : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_quantity=" + Math.Max(1, crop.HarvestMinStack ?? 1) : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_value_basis=conservative_min_stack_only" : string.Empty) +
                        (estimatedFirstHarvestQuantity.HasValue ? ";estimated_first_harvest_quantity=" + FormatNumber(estimatedFirstHarvestQuantity.Value) : string.Empty) +
                        (estimatedFirstHarvestValue.HasValue ? ";estimated_first_harvest_value=" + FormatNumber(estimatedFirstHarvestValue.Value) : string.Empty) +
                        (estimatedFirstHarvestValue.HasValue ? ";estimated_first_harvest_value_basis=mean_stack_plus_extra_chance_quality0_no_farming_scaling" : string.Empty) +
                        (regrowHarvestCount.HasValue ? ";estimated_regrow_harvest_count=" + regrowHarvestCount.Value : string.Empty) +
                        (totalHarvestCount.HasValue ? ";estimated_total_harvest_count=" + totalHarvestCount.Value : string.Empty) +
                        (expectedSeasonHarvestValue.HasValue ? ";expected_season_harvest_value=" + expectedSeasonHarvestValue.Value : string.Empty) +
                        (estimatedSeasonHarvestValue.HasValue ? ";estimated_season_harvest_value=" + FormatNumber(estimatedSeasonHarvestValue.Value) : string.Empty) +
                        (seedUnitCost.HasValue ? ";seed_unit_cost=" + seedUnitCost.Value : string.Empty) +
                        (conservativeNetValue.HasValue ? ";expected_first_harvest_net_value=" + conservativeNetValue.Value : string.Empty) +
                        (estimatedNetValue.HasValue ? ";estimated_first_harvest_net_value=" + FormatNumber(estimatedNetValue.Value) : string.Empty) +
                        (expectedSeasonHarvestNetValue.HasValue ? ";expected_season_harvest_net_value=" + expectedSeasonHarvestNetValue.Value : string.Empty) +
                        (estimatedSeasonHarvestNetValue.HasValue ? ";estimated_season_harvest_net_value=" + FormatNumber(estimatedSeasonHarvestNetValue.Value) : string.Empty) +
                        (estimatedSeasonHarvestValue.HasValue ? ";season_harvest_value_basis=first_harvest_value_times_transparent_regrow_count_seed_cost_once" : string.Empty) +
                        (regrowHarvestCount.HasValue ? ";regrow_estimate_basis=adjusted_grow_days_days_remaining_regrow_days" : string.Empty) +
                        (estimatedNetValue.HasValue ? ";net_value_basis=transparent_seed_unit_cost_subtracted" : string.Empty),
                    ItemId = seedId,
                    QualifiedItemId = qualifiedItemId,
                    SlotIndex = NullableReadInt(result, "slot_index"),
                    Quantity = seedStack,
                    EstimatedTicks = 60,
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_planting_context",
                    BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
                };
            }
        }

        private static IReadOnlyDictionary<string, int> SeedInventoryStacks(SnapshotEnvelope snapshot)
        {
            var seedInventory = ReadStateFieldValue(snapshot, "player", "seed_inventory");
            if (!seedInventory.HasValue || seedInventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            return seedInventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .GroupBy(item => ReadString(item, "item_id"), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => Math.Max(0, ReadInt(item, "stack"))),
                    StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, int> InventoryStacksByQualifiedId(SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && ReadBool(item, "is_empty") != true)
                .Select(item => new
                {
                    QualifiedId = NormalizeObjectQualifiedId(ReadString(item, "qualified_item_id"), ReadString(item, "item_id")),
                    Stack = Math.Max(0, ReadInt(item, "stack"))
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.QualifiedId) && item.Stack > 0)
                .GroupBy(item => item.QualifiedId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.Stack),
                    StringComparer.OrdinalIgnoreCase);
        }

        private readonly struct CropCatalogEntry
        {
            public CropCatalogEntry(
                string harvestItemId,
                string harvestItemQualifiedId,
                int? harvestUnitSalePrice,
                int? harvestMinStack,
                int? harvestMaxStack,
                double? harvestMaxIncreasePerFarmingLevel,
                double? extraHarvestChance,
                int? harvestMinQuality,
                int? harvestMaxQuality,
                string harvestMethod,
                int? regrowDays)
            {
                HarvestItemId = harvestItemId;
                HarvestItemQualifiedId = harvestItemQualifiedId;
                HarvestUnitSalePrice = harvestUnitSalePrice;
                HarvestMinStack = harvestMinStack;
                HarvestMaxStack = harvestMaxStack;
                HarvestMaxIncreasePerFarmingLevel = harvestMaxIncreasePerFarmingLevel;
                ExtraHarvestChance = extraHarvestChance;
                HarvestMinQuality = harvestMinQuality;
                HarvestMaxQuality = harvestMaxQuality;
                HarvestMethod = harvestMethod;
                RegrowDays = regrowDays;
            }

            public string HarvestItemId { get; }
            public string HarvestItemQualifiedId { get; }
            public int? HarvestUnitSalePrice { get; }
            public int? HarvestMinStack { get; }
            public int? HarvestMaxStack { get; }
            public double? HarvestMaxIncreasePerFarmingLevel { get; }
            public double? ExtraHarvestChance { get; }
            public int? HarvestMinQuality { get; }
            public int? HarvestMaxQuality { get; }
            public string HarvestMethod { get; }
            public int? RegrowDays { get; }
        }

        private static IReadOnlyDictionary<string, CropCatalogEntry> CropCatalogBySeed(SnapshotEnvelope snapshot)
        {
            var cropCatalog = ReadStateFieldValue(snapshot, "farm", "crop_catalog");
            if (!cropCatalog.HasValue || cropCatalog.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, CropCatalogEntry>(StringComparer.Ordinal);
            }

            return cropCatalog.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .GroupBy(item => ReadString(item, "seed_id"), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var item = group.First();
                        return new CropCatalogEntry(
                            ReadString(item, "harvest_item_id"),
                            ReadString(item, "harvest_item_qualified_id"),
                            NullableReadInt(item, "harvest_unit_sale_price"),
                            NullableReadInt(item, "harvest_min_stack"),
                            NullableReadInt(item, "harvest_max_stack"),
                            NullableReadDouble(item, "harvest_max_increase_per_farming_level"),
                            NullableReadDouble(item, "extra_harvest_chance"),
                            NullableReadInt(item, "harvest_min_quality"),
                            NullableReadInt(item, "harvest_max_quality"),
                            ReadString(item, "harvest_method"),
                            NullableReadInt(item, "regrow_days"));
                    },
                    StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, int> SeedUnitCosts(SnapshotEnvelope snapshot)
        {
            return ActiveShopSeedUnitCosts(snapshot)
                .Concat(PreviewShopSeedUnitCosts(snapshot))
                .GroupBy(item => item.SeedId, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(item => item.UnitCost),
                    StringComparer.Ordinal);
        }

        private static IEnumerable<(string SeedId, int UnitCost)> ActiveShopSeedUnitCosts(SnapshotEnvelope snapshot)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var entry in entries.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object))
            {
                var seedId = ReadString(entry, "item_id");
                var price = ReadInt(entry, "price");
                if (!string.IsNullOrWhiteSpace(seedId) && price > 0)
                {
                    yield return (seedId, price);
                }
            }
        }

        private static IEnumerable<(string SeedId, int UnitCost)> PreviewShopSeedUnitCosts(SnapshotEnvelope snapshot)
        {
            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (!shops.HasValue ||
                shops.Value.ValueKind != JsonValueKind.Object ||
                !shops.Value.TryGetProperty("shops", out var shopArray) ||
                shopArray.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var shop in shopArray.EnumerateArray().Where(shop => shop.ValueKind == JsonValueKind.Object))
            {
                if (!shop.TryGetProperty("stock_preview", out var preview) ||
                    preview.ValueKind != JsonValueKind.Object ||
                    !preview.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var entry in entries.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object))
                {
                    var seedId = ReadString(entry, "item_id");
                    var price = ReadInt(entry, "price");
                    if (!string.IsNullOrWhiteSpace(seedId) && price > 0)
                    {
                        yield return (seedId, price);
                    }
                }
            }
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double? EstimatedFirstHarvestQuantity(CropCatalogEntry crop)
        {
            if (!crop.HarvestMinStack.HasValue && !crop.HarvestMaxStack.HasValue && !crop.ExtraHarvestChance.HasValue)
            {
                return null;
            }

            var minStack = Math.Max(1, crop.HarvestMinStack ?? 1);
            var maxStack = Math.Max(minStack, crop.HarvestMaxStack ?? minStack);
            var meanStack = (minStack + maxStack) / 2.0;
            var extraChance = Math.Clamp(crop.ExtraHarvestChance ?? 0, 0, 0.9);
            var expectedExtra = extraChance <= 0 ? 0 : extraChance / (1 - extraChance);
            return meanStack + expectedExtra;
        }

        private static int? EstimatedRegrowHarvestCount(CropCatalogEntry crop, int? adjustedGrowDays, int? daysRemaining)
        {
            if (!crop.RegrowDays.HasValue || crop.RegrowDays.Value <= 0 || !adjustedGrowDays.HasValue || !daysRemaining.HasValue)
            {
                return null;
            }

            var remainingAfterFirstHarvest = daysRemaining.Value - adjustedGrowDays.Value;
            if (remainingAfterFirstHarvest < crop.RegrowDays.Value)
            {
                return 0;
            }

            return remainingAfterFirstHarvest / crop.RegrowDays.Value;
        }

        private static int? EstimatedTotalHarvestCount(int? adjustedGrowDays, int? daysRemaining, int? regrowHarvestCount)
        {
            if (!adjustedGrowDays.HasValue || !daysRemaining.HasValue || daysRemaining.Value < adjustedGrowDays.Value)
            {
                return null;
            }

            return 1 + Math.Max(0, regrowHarvestCount ?? 0);
        }

        private static EventCandidate[] WateringCandidates(SnapshotEnvelope snapshot)
        {
            var crops = ReadStateFieldValue(snapshot, "farm", "crops");
            if (!crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return crops.Value.EnumerateArray()
                .Where(crop => crop.ValueKind == JsonValueKind.Object && ReadBool(crop, "needs_watering") == true)
                .Select(crop =>
                {
                    var x = ReadInt(crop, "tile_x");
                    var y = ReadInt(crop, "tile_y");
                    return new EventCandidate
                    {
                        CandidateId = "water:Farm:" + x + "," + y,
                        Kind = "water_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = "farm.crops[" + x + "," + y + "].needs_watering=false",
                        EstimatedTicks = 60,
                        EnergyCost = 2
                    };
                })
                .ToArray();
        }

        private static EventCandidate[] HarvestCropCandidates(SnapshotEnvelope snapshot)
        {
            var crops = ReadStateFieldValue(snapshot, "farm", "crops");
            if (!crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return crops.Value.EnumerateArray()
                .Where(crop => crop.ValueKind == JsonValueKind.Object && ReadBool(crop, "ready_for_harvest") == true)
                .Select(crop =>
                {
                    var x = ReadInt(crop, "tile_x");
                    var y = ReadInt(crop, "tile_y");
                    var harvestItemId = ReadString(crop, "harvest_item_id");
                    var harvestQualifiedItemId = ReadString(crop, "harvest_item_qualified_id");
                    var harvestItemCategory = ReadInt(crop, "harvest_item_category");
                    var harvestMethod = ReadString(crop, "harvest_method");
                    var skillId = ReadString(crop, "harvest_experience_skill_id");
                    var skillMinimum = NullableReadInt(crop, "harvest_experience_on_success_min");
                    var skillMaximum = NullableReadInt(crop, "harvest_experience_on_success_max");
                    var skillCondition = ReadString(crop, "harvest_experience_condition");
                    var skillStatus = ReadString(crop, "harvest_experience_projection_status");
                    var effect = "farm.crops[" + x + "," + y + "].ready_for_harvest=false" +
                        (!string.IsNullOrWhiteSpace(harvestItemId) ? ";harvest_item_id=" + harvestItemId : string.Empty) +
                        (!string.IsNullOrWhiteSpace(harvestQualifiedItemId) ? ";harvest_item_qualified_id=" + harvestQualifiedItemId : string.Empty) +
                        ";harvest_item_category=" + harvestItemCategory +
                        (!string.IsNullOrWhiteSpace(harvestMethod) ? ";harvest_method=" + harvestMethod : string.Empty) +
                        (!string.IsNullOrWhiteSpace(skillId) ? ";skill_experience_skill_id=" + skillId : string.Empty) +
                        (skillMinimum.HasValue ? ";skill_experience_on_success_min=" + skillMinimum.Value : string.Empty) +
                        (skillMaximum.HasValue ? ";skill_experience_on_success_max=" + skillMaximum.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(skillCondition) ? ";skill_experience_condition=" + skillCondition : string.Empty) +
                        (!string.IsNullOrWhiteSpace(skillStatus) ? ";skill_experience_projection_status=" + skillStatus : string.Empty) +
                        ";harvest_executor_status=runtime_verified";
                    return new EventCandidate
                    {
                        CandidateId = "harvest:Farm:" + x + "," + y,
                        Kind = "harvest_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ItemId = harvestItemId,
                        QualifiedItemId = harvestQualifiedItemId,
                        Quantity = Math.Max(1, ReadInt(crop, "harvest_min_stack")),
                        ExpectedEffect = effect,
                        EstimatedTicks = 60,
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_ready_for_harvest_runtime_verified",
                        Parameters = new[]
                        {
                            Parameter("skill_experience_skill_id", skillId),
                            Parameter("skill_experience_on_success_min", skillMinimum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_on_success_max", skillMaximum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_condition", skillCondition),
                            Parameter("skill_experience_projection_status", skillStatus),
                            Parameter("harvest_method", harvestMethod),
                            Parameter("harvest_item_qualified_id", harvestQualifiedItemId),
                            Parameter("harvest_item_category", harvestItemCategory.ToString()),
                            Parameter("harvest_context_tags_json", JsonSerializer.Serialize(ReadStringArray(crop, "harvest_context_tags")))
                        }
                    };
                })
                .ToArray();
        }

        private static EventCandidate[] HarvestGiantCropCandidates(SnapshotEnvelope snapshot)
        {
            var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
            if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return clumps.Value.EnumerateArray()
                .Where(clump => clump.ValueKind == JsonValueKind.Object && ReadBool(clump, "is_giant_crop") == true)
                .Select(clump =>
                {
                    var x = ReadInt(clump, "tile_x");
                    var y = ReadInt(clump, "tile_y");
                    var id = ReadString(clump, "giant_crop_id");
                    var health = ReadInt(clump, "health");
                    var skillId = ReadString(clump, "harvest_experience_skill_id");
                    var skillMinimum = NullableReadInt(clump, "harvest_experience_on_success_min");
                    var skillMaximum = NullableReadInt(clump, "harvest_experience_on_success_max");
                    var skillCondition = ReadString(clump, "harvest_experience_condition");
                    var skillStatus = ReadString(clump, "harvest_experience_projection_status");
                    var outputProjectionStatus = ReadString(clump, "giant_crop_output_projection_status");
                    var guaranteedOutputsJson = ReadString(clump, "giant_crop_guaranteed_outputs_json");
                    var effect = "farm.resource_clumps[" + x + "," + y + "].is_giant_crop=false" +
                        (!string.IsNullOrWhiteSpace(id) ? ";giant_crop_id=" + id : string.Empty) +
                        (!string.IsNullOrWhiteSpace(outputProjectionStatus) ? ";giant_crop_output_projection_status=" + outputProjectionStatus : string.Empty) +
                        ";required_tool=axe" +
                        ";resource_clump_health=" + health +
                        (!string.IsNullOrWhiteSpace(skillId) ? ";skill_experience_skill_id=" + skillId : string.Empty) +
                        (skillMinimum.HasValue ? ";skill_experience_on_success_min=" + skillMinimum.Value : string.Empty) +
                        (skillMaximum.HasValue ? ";skill_experience_on_success_max=" + skillMaximum.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(skillCondition) ? ";skill_experience_condition=" + skillCondition : string.Empty) +
                        (!string.IsNullOrWhiteSpace(skillStatus) ? ";skill_experience_projection_status=" + skillStatus : string.Empty) +
                        ";harvest_giant_crop_executor_status=runtime_verified";
                    return new EventCandidate
                    {
                        CandidateId = "harvest-giant-crop:Farm:" + x + "," + y,
                        Kind = "harvest_giant_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = effect,
                        EstimatedTicks = Math.Max(3, health) * 60,
                        EnergyCost = Math.Max(1, health),
                        AvailabilityClass = "transparent_giant_crop_resource_clump_runtime_verified",
                        Parameters = new[]
                        {
                            Parameter("skill_experience_skill_id", skillId),
                            Parameter("skill_experience_on_success_min", skillMinimum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_on_success_max", skillMaximum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_condition", skillCondition),
                            Parameter("skill_experience_projection_status", skillStatus),
                            Parameter("giant_crop_guaranteed_outputs_json", guaranteedOutputsJson),
                            Parameter("giant_crop_output_projection_status", outputProjectionStatus)
                        }
                    };
                })
                .ToArray();
        }

        private EventCandidate[] ClearFarmResourceClumpCandidates(SnapshotEnvelope snapshot)
        {
            var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
            if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return clumps.Value.EnumerateArray()
                .Where(clump => clump.ValueKind == JsonValueKind.Object &&
                    ReadString(clump, "clear_kind") is "resource_stump" or "hollow_log")
                .Select(clump =>
                {
                    var x = ReadInt(clump, "tile_x");
                    var y = ReadInt(clump, "tile_y");
                    var clearKind = ReadString(clump, "clear_kind");
                    var status = ReadString(clump, "clear_obstacle_executor_status");
                    var hits = Math.Max(1, ReadInt(clump, "expected_tool_hits_to_clear"));
                    var width = Math.Max(1, ReadInt(clump, "width"));
                    var height = Math.Max(1, ReadInt(clump, "height"));
                    var standSelection = FindBestResourceClumpStandTile(snapshot, x, y, width, height);
                    var standTile = standSelection?.Stand;
                    var hitTile = standSelection?.Hit;
                    var skillId = ReadString(clump, "harvest_experience_skill_id");
                    var skillMinimum = NullableReadInt(clump, "harvest_experience_on_success_min");
                    var skillMaximum = NullableReadInt(clump, "harvest_experience_on_success_max");
                    var skillCondition = ReadString(clump, "harvest_experience_condition");
                    var skillStatus = ReadString(clump, "harvest_experience_projection_status");
                    var blockReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                    {
                        OptionId = "executor.break_farm_resource_clump",
                        Parameters = new[]
                        {
                            Parameter("target_tile_x", (hitTile?.X ?? x).ToString()),
                            Parameter("target_tile_y", (hitTile?.Y ?? y).ToString()),
                            Parameter("stand_tile_x", (standTile?.X ?? x).ToString()),
                            Parameter("stand_tile_y", (standTile?.Y ?? y).ToString()),
                            Parameter("resource_clump_tile_x", x.ToString()),
                            Parameter("resource_clump_tile_y", y.ToString()),
                            Parameter("resource_clump_width", width.ToString()),
                            Parameter("resource_clump_height", height.ToString()),
                            Parameter("resource_clump_parent_sheet_index", ReadInt(clump, "parent_sheet_index").ToString()),
                            Parameter("tool_slot_index", ReadInt(clump, "tool_slot_index").ToString()),
                            Parameter("required_tool_kind", "axe"),
                            Parameter("max_tool_swings", hits.ToString())
                        }
                    }).ToList();
                    if (!string.Equals(status, "ready", StringComparison.Ordinal))
                    {
                        blockReasons.Add(string.IsNullOrWhiteSpace(status) ? "resource_clump_clear_projection_unavailable" : status);
                    }
                    if (standTile is null)
                    {
                        blockReasons.Add("resource_clump_no_adjacent_route_stand_tile");
                    }

                    var distance = standTile is null
                        ? 0
                        : Math.Abs(playerX - standTile.X) + Math.Abs(playerY - standTile.Y);
                    var effect = (standTile is not null ? "resource_clump_stand_tile=" + standTile.X + "," + standTile.Y + ";" : string.Empty) +
                        (hitTile is not null ? "resource_clump_hit_tile=" + hitTile.X + "," + hitTile.Y + ";" : string.Empty) +
                        "farm.resource_clumps[" + x + "," + y + "].present=false" +
                        ";clear_kind=" + clearKind +
                        ";resource_clump_tile=" + x + "," + y +
                        ";resource_clump_width=" + width +
                        ";resource_clump_height=" + height +
                        ";resource_clump_parent_sheet_index=" + ReadInt(clump, "parent_sheet_index") +
                        ";max_tool_swings=" + hits +
                        ";max_movement_tiles=512" +
                        ";tool_slot_index=" + ReadInt(clump, "tool_slot_index") +
                        ";required_tool_kind=axe" +
                        SkillExperienceEffect(clump);
                    return new EventCandidate
                    {
                        CandidateId = "clear-resource-clump:Farm:" + x + "," + y + ":" + clearKind,
                        Kind = "clear_farm_resource_clump",
                        Available = blockReasons.Count == 0,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = effect,
                        EstimatedTicks = Math.Max(60, distance * 60 + hits * 60),
                        EnergyCost = hits * 2,
                        AvailabilityClass = "transparent_farm_resource_clump",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = new[]
                        {
                            Parameter("skill_experience_skill_id", skillId),
                            Parameter("skill_experience_on_success_min", skillMinimum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_on_success_max", skillMaximum?.ToString() ?? string.Empty),
                            Parameter("skill_experience_condition", skillCondition),
                            Parameter("skill_experience_projection_status", skillStatus)
                        }
                    };
                })
                .ToArray();
        }

        private static ResourceClumpStandSelection? FindBestResourceClumpStandTile(
            SnapshotEnvelope snapshot,
            int anchorX,
            int anchorY,
            int width,
            int height)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var candidates = new List<ResourceClumpStandSelection>();
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                candidates.Add(new ResourceClumpStandSelection(
                    new CandidateTile(anchorX + offsetX, anchorY - 1),
                    new CandidateTile(anchorX + offsetX, anchorY)));
                candidates.Add(new ResourceClumpStandSelection(
                    new CandidateTile(anchorX + offsetX, anchorY + height),
                    new CandidateTile(anchorX + offsetX, anchorY + height - 1)));
            }
            for (var offsetY = 0; offsetY < height; offsetY++)
            {
                candidates.Add(new ResourceClumpStandSelection(
                    new CandidateTile(anchorX - 1, anchorY + offsetY),
                    new CandidateTile(anchorX, anchorY + offsetY)));
                candidates.Add(new ResourceClumpStandSelection(
                    new CandidateTile(anchorX + width, anchorY + offsetY),
                    new CandidateTile(anchorX + width - 1, anchorY + offsetY)));
            }

            return candidates
                .Where(candidate => !CollisionGridBlocksTile(snapshot, candidate.Stand.X, candidate.Stand.Y))
                .OrderBy(candidate => Math.Abs(playerX - candidate.Stand.X) + Math.Abs(playerY - candidate.Stand.Y))
                .FirstOrDefault();
        }

        private sealed class ResourceClumpStandSelection
        {
            public ResourceClumpStandSelection(CandidateTile stand, CandidateTile hit)
            {
                Stand = stand;
                Hit = hit;
            }

            public CandidateTile Stand { get; }

            public CandidateTile Hit { get; }
        }

        private static EventCandidate[] PickupDebrisCandidates(SnapshotEnvelope snapshot)
        {
            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var debris = ReadStateFieldValue(snapshot, "current_location", "debris");
            if ((!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array) &&
                string.Equals(locationId, "Farm", StringComparison.OrdinalIgnoreCase))
            {
                debris = ReadStateFieldValue(snapshot, "farm", "debris");
            }
            if (!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return debris.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item =>
                {
                    var index = ReadInt(item, "debris_index");
                    var tile = FirstDebrisChunkTile(item);
                    var qualifiedItemId = ReadString(item, "qualified_item_id");
                    var itemId = ReadString(item, "item_id");
                    var contextTags = item.TryGetProperty("item", out var itemState) &&
                        itemState.ValueKind == JsonValueKind.Object
                            ? ReadStringArray(itemState, "context_tags")
                            : Array.Empty<string>();
                    var blockReasons = new List<string>();
                    if (tile is null)
                    {
                        blockReasons.Add("pickup_debris_no_chunk_tile");
                    }

                    if (string.IsNullOrWhiteSpace(qualifiedItemId) && string.IsNullOrWhiteSpace(itemId))
                    {
                        blockReasons.Add("pickup_debris_item_id_unavailable");
                    }

                    if (!InventoryMayAcceptItem(snapshot, qualifiedItemId, itemId, ReadInt(item, "item_quality")))
                    {
                        blockReasons.Add("pickup_debris_inventory_cannot_accept_item");
                    }

                    var x = tile?.X ?? 0;
                    var y = tile?.Y ?? 0;
                    var distance = tile is null ? 0 : Math.Abs(playerX - x) + Math.Abs(playerY - y);
                    return new EventCandidate
                    {
                        CandidateId = "pickup-debris:" + locationId + ":" + index + ":" + x + "," + y + ":" + (string.IsNullOrWhiteSpace(qualifiedItemId) ? itemId : qualifiedItemId),
                        Kind = "pickup_debris_item",
                        Available = blockReasons.Count == 0,
                        LocationId = locationId,
                        TileX = tile?.X,
                        TileY = tile?.Y,
                        ExpectedEffect = "current_location.debris[" + index + "].chunk_count_decreases_or_removed=true" +
                            (!string.IsNullOrWhiteSpace(qualifiedItemId) ? ";qualified_item_id=" + qualifiedItemId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(itemId) ? ";item_id=" + itemId : string.Empty) +
                            ";debris_index=" + index +
                            ";pickup_executor_status=runtime_collect",
                        ItemId = itemId,
                        QualifiedItemId = qualifiedItemId,
                        Quantity = Math.Max(1, ReadInt(item, "chunk_count")),
                        EstimatedTicks = Math.Max(60, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_current_location_debris_runtime_collect",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = new[]
                        {
                            Parameter("debris_context_tags_json", JsonSerializer.Serialize(contextTags))
                        }
                    };
                })
                .OrderBy(candidate => candidate.TileY ?? int.MaxValue)
                .ThenBy(candidate => candidate.TileX ?? int.MaxValue)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

    }
}
