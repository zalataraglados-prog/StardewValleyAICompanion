using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StardewAI.Core.Infrastructure;

public sealed record IslandGourmandCropSupplyDefinition(
    [property: JsonPropertyName("request_index")] int RequestIndex,
    [property: JsonPropertyName("tracker_key")] string TrackerKey,
    [property: JsonPropertyName("seed_id")] string SeedId,
    [property: JsonPropertyName("harvest_item_id")] string HarvestItemId,
    [property: JsonPropertyName("days_in_phase")] int[] DaysInPhase,
    [property: JsonPropertyName("base_grow_days")] int BaseGrowDays,
    [property: JsonPropertyName("needs_watering")] bool NeedsWatering,
    [property: JsonPropertyName("seed_shop_id")] string SeedShopId,
    [property: JsonPropertyName("seed_shop_conditions")] string[] SeedShopConditions,
    [property: JsonPropertyName("purchase_window_start_total_day")] int PurchaseWindowStartTotalDay,
    [property: JsonPropertyName("purchase_window_end_total_day_inclusive")] int PurchaseWindowEndTotalDayInclusive,
    [property: JsonPropertyName("native_object_base_price")] int NativeObjectBasePrice,
    [property: JsonPropertyName("standard_seed_shop_unit_price")] int StandardSeedShopUnitPrice,
    [property: JsonPropertyName("fresh_shop_quote_required")] bool FreshShopQuoteRequired,
    [property: JsonPropertyName("must_remain_standing")] bool MustRemainStanding);

public sealed record IslandGourmandSupplyScheduleProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("planting_location_id")] string PlantingLocationId,
    [property: JsonPropertyName("planting_location_ignores_seasons")] bool PlantingLocationIgnoresSeasons,
    [property: JsonPropertyName("deadline_total_day_exclusive")] int DeadlineTotalDayExclusive,
    [property: JsonPropertyName("earliest_all_seed_stock_total_day")] int EarliestAllSeedStockTotalDay,
    [property: JsonPropertyName("maximum_growth_nights")] int MaximumGrowthNights,
    [property: JsonPropertyName("latest_western_turtle_unlock_total_day_inclusive")] int LatestWesternTurtleUnlockTotalDayInclusive,
    [property: JsonPropertyName("latest_island_house_unlock_total_day_inclusive")] int LatestIslandHouseUnlockTotalDayInclusive,
    [property: JsonPropertyName("standard_seed_shop_total_price")] int StandardSeedShopTotalPrice,
    [property: JsonPropertyName("fresh_shop_quote_required")] bool FreshShopQuoteRequired,
    [property: JsonPropertyName("same_day_sequential_delivery_supported")] bool SameDaySequentialDeliverySupported,
    [property: JsonPropertyName("required_option_ids")] string[] RequiredOptionIds,
    [property: JsonPropertyName("crops")] IslandGourmandCropSupplyDefinition[] Crops,
    [property: JsonPropertyName("supply_schedule_complete")] bool SupplyScheduleComplete,
    [property: JsonPropertyName("western_turtle_unlock_total_day")] int? WesternTurtleUnlockTotalDay,
    [property: JsonPropertyName("island_house_unlock_total_day")] int? IslandHouseUnlockTotalDay,
    [property: JsonPropertyName("planting_total_day")] int? PlantingTotalDay,
    [property: JsonPropertyName("all_crops_mature_total_day")] int? AllCropsMatureTotalDay,
    [property: JsonPropertyName("delivery_total_day")] int? DeliveryTotalDay,
    [property: JsonPropertyName("deadline_composition_complete")] bool DeadlineCompositionComplete);

public static class IslandGourmandSupplyScheduleProofBuilder
{
    public const string SchemaVersion = "island_gourmand_supply_schedule.v1";
    public const int DeadlineTotalDayExclusive = 224;
    public const int EarliestAllSeedStockTotalDay = 140;

    private static readonly string[] RequiredOptionIds =
    {
        "economy.buy_supplies",
        "executor.advance_gourmand_request",
        "executor.plant_seed",
        "executor.sleep",
        "executor.till_soil",
        "executor.traverse_connector",
        "executor.water_crop",
        "farm.maintain_crops",
        "island.complete_gourmand_request"
    };

    private static readonly IslandGourmandCropSupplyDefinition[] NativeDefinitions =
    {
        new(
            0,
            "IslandGourmand1",
            "479",
            "254",
            new[] { 1, 2, 3, 3, 3 },
            12,
            true,
            "SeedShop",
            new[] { "SEASON summer" },
            140,
            167,
            40,
            80,
            true,
            true),
        new(
            1,
            "IslandGourmand2",
            "483",
            "262",
            new[] { 1, 1, 1, 1 },
            4,
            true,
            "SeedShop",
            new[] { "SEASON summer", "SEASON fall" },
            140,
            195,
            5,
            10,
            true,
            true),
        new(
            2,
            "IslandGourmand3",
            "476",
            "248",
            new[] { 1, 1, 1, 1 },
            4,
            true,
            "SeedShop",
            new[] { "SEASON spring, YEAR 2" },
            112,
            139,
            20,
            40,
            true,
            true)
    };

    public static IslandGourmandSupplyScheduleProof Build(
        int? westernTurtleUnlockTotalDay = null,
        int? islandHouseUnlockTotalDay = null) =>
        Build(
            NativeDefinitions,
            westernTurtleUnlockTotalDay,
            islandHouseUnlockTotalDay);

    public static IslandGourmandSupplyScheduleProof Build(
        IReadOnlyCollection<IslandGourmandCropSupplyDefinition> definitions,
        int? westernTurtleUnlockTotalDay = null,
        int? islandHouseUnlockTotalDay = null)
    {
        if (definitions is null)
            throw new ArgumentNullException(nameof(definitions));
        if (westernTurtleUnlockTotalDay is < 0)
            throw new ArgumentOutOfRangeException(nameof(westernTurtleUnlockTotalDay));
        if (islandHouseUnlockTotalDay is < 0)
            throw new ArgumentOutOfRangeException(nameof(islandHouseUnlockTotalDay));

        var crops = definitions
            .OrderBy(definition => definition.RequestIndex)
            .ToArray();
        Require(crops.Length == NativeDefinitions.Length, "Gourmand crop denominator drifted.");
        for (var index = 0; index < NativeDefinitions.Length; index++)
            RequireExact(crops[index], NativeDefinitions[index]);

        var maximumGrowthNights = crops.Max(crop => crop.BaseGrowDays);
        var latestPlantingTotalDay = DeadlineTotalDayExclusive - maximumGrowthNights - 1;
        var latestHouseUnlockTotalDay = DeadlineTotalDayExclusive - 1;
        int? plantingTotalDay = null;
        int? matureTotalDay = null;
        int? deliveryTotalDay = null;
        if (westernTurtleUnlockTotalDay.HasValue && islandHouseUnlockTotalDay.HasValue)
        {
            plantingTotalDay = Math.Max(
                EarliestAllSeedStockTotalDay,
                westernTurtleUnlockTotalDay.Value);
            matureTotalDay = checked(plantingTotalDay.Value + maximumGrowthNights);
            deliveryTotalDay = Math.Max(
                matureTotalDay.Value,
                islandHouseUnlockTotalDay.Value);
        }

        var deadlineComplete = deliveryTotalDay.HasValue &&
            deliveryTotalDay.Value < DeadlineTotalDayExclusive;
        return new IslandGourmandSupplyScheduleProof(
            SchemaVersion,
            deadlineComplete
                ? "supply_and_deadline_composition_complete"
                : "supply_schedule_complete_unlock_days_pending",
            "IslandWest",
            true,
            DeadlineTotalDayExclusive,
            EarliestAllSeedStockTotalDay,
            maximumGrowthNights,
            latestPlantingTotalDay,
            latestHouseUnlockTotalDay,
            crops.Sum(crop => crop.StandardSeedShopUnitPrice),
            crops.All(crop => crop.FreshShopQuoteRequired),
            true,
            RequiredOptionIds.ToArray(),
            crops,
            true,
            westernTurtleUnlockTotalDay,
            islandHouseUnlockTotalDay,
            plantingTotalDay,
            matureTotalDay,
            deliveryTotalDay,
            deadlineComplete);
    }

    private static void RequireExact(
        IslandGourmandCropSupplyDefinition actual,
        IslandGourmandCropSupplyDefinition expected)
    {
        Require(
            actual.RequestIndex == expected.RequestIndex &&
            actual.TrackerKey == expected.TrackerKey &&
            actual.SeedId == expected.SeedId &&
            actual.HarvestItemId == expected.HarvestItemId &&
            actual.DaysInPhase.SequenceEqual(expected.DaysInPhase) &&
            actual.BaseGrowDays == actual.DaysInPhase.Sum() &&
            actual.BaseGrowDays == expected.BaseGrowDays &&
            actual.NeedsWatering == expected.NeedsWatering &&
            actual.SeedShopId == expected.SeedShopId &&
            actual.SeedShopConditions.SequenceEqual(expected.SeedShopConditions, StringComparer.Ordinal) &&
            actual.PurchaseWindowStartTotalDay == expected.PurchaseWindowStartTotalDay &&
            actual.PurchaseWindowEndTotalDayInclusive == expected.PurchaseWindowEndTotalDayInclusive &&
            actual.NativeObjectBasePrice == expected.NativeObjectBasePrice &&
            actual.StandardSeedShopUnitPrice == expected.StandardSeedShopUnitPrice &&
            actual.StandardSeedShopUnitPrice == actual.NativeObjectBasePrice * 2 &&
            actual.FreshShopQuoteRequired == expected.FreshShopQuoteRequired &&
            actual.MustRemainStanding == expected.MustRemainStanding,
            $"Gourmand crop authority drifted for request {expected.RequestIndex}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
