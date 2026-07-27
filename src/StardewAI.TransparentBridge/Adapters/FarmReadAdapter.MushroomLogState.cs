using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string MushroomLogQualifiedItemId =
        "(BC)MushroomLog";
    private const string MushroomLogStateModelId =
        "mushroom_log_nearby_tree_distribution.v1";
    private const int MushroomLogTreeRadius = 3;

    private static bool IsVettedMushroomLogOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId ==
                MushroomLogQualifiedItemId &&
            machine.GetType() ==
                typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputMushroomLog",
                StringComparison.Ordinal);
    }

    private static object? ReadMushroomLogSpecialState(
        StardewValley.Object machine,
        GameLocation location)
    {
        if (machine.QualifiedItemId !=
                MushroomLogQualifiedItemId ||
            machine.GetType() != typeof(StardewValley.Object))
        {
            return null;
        }

        var outputMethod = machine.GetMachineData()
            ?.OutputRules?
            .Where(rule =>
                rule.Triggers?.Any(trigger =>
                    trigger.Trigger.HasFlag(
                        MachineOutputTrigger.DayUpdate)) ==
                true)
            .SelectMany(
                rule =>
                    rule.OutputItem ??
                    new List<MachineItemOutput>())
            .Select(output =>
                output.OutputMethod ?? string.Empty)
            .FirstOrDefault(method =>
                !string.IsNullOrWhiteSpace(method)) ??
            string.Empty;
        if (!IsVettedMushroomLogOutputMethod(
                machine,
                outputMethod))
        {
            return BlockedMushroomLogState(
                machine,
                outputMethod,
                "mushroom_log_output_method_not_vetted");
        }

        var nearbyTrees = ReadMushroomLogNearbyTrees(
            machine,
            location);
        var allTreeCount = nearbyTrees.Length;
        var matureTreeCount = nearbyTrees.Count(
            row => row.Mature);
        var matureMossTreeCount = nearbyTrees.Count(
            row => row.Mature && row.HasMoss);
        var genericPoolEntryCount = Math.Max(
            1,
            (int)(allTreeCount * 0.75f));
        var totalPoolEntryCount =
            matureTreeCount + genericPoolEntryCount;
        var itemDistribution =
            ReadMushroomLogItemDistribution(
                nearbyTrees,
                genericPoolEntryCount,
                totalPoolEntryCount);
        var amountDistribution =
            ReadMushroomLogAmountDistribution(
                allTreeCount);
        var qualityChanceRaw =
            matureMossTreeCount * 0.025f +
            allTreeCount * 0.025f;
        var qualityChance = Math.Clamp(
            qualityChanceRaw,
            0,
            1);
        var qualityDistribution =
            ReadMushroomLogQualityDistribution(
                qualityChance);
        var heldItem = machine.heldObject.Value;
        var locationContextId =
            location.GetLocationContextId();
        var hasWeather = Game1.netWorldState.Value
            .LocationWeather.TryGetValue(
                locationContextId,
                out var weather);
        return new
        {
            schema_version =
                "mushroom_log_special_state.v1",
            status = "available",
            special_state_model_id =
                MushroomLogStateModelId,
            source =
                "decompiled_Object.OutputMushroomLog_and_DayUpdate",
            vetted_output_method = outputMethod,
            lifecycle_state = heldItem is null
                ? "waiting_for_day_update"
                : machine.readyForHarvest.Value
                    ? "ready_for_collection"
                    : "processing",
            held_item = SummarizeItem(heldItem),
            ready_for_harvest =
                machine.readyForHarvest.Value,
            minutes_until_ready =
                machine.MinutesUntilReady,
            location_context_id = locationContextId,
            current_weather =
                ReadMushroomLogWeather(weather),
            current_weather_status = hasWeather
                ? "available_existing_context"
                : "blocked_location_context_weather_not_initialized",
            day_update_contract = new
            {
                trigger = "DayUpdate",
                creates_output_only_when_held_item_is_null =
                    true,
                initial_days_until_morning = 3,
                rainy_day_branch_applies_for_current_weather =
                    hasWeather
                        ? (bool?)weather!.IsRaining
                        : null,
                rainy_day_timer_adjustment_formula =
                    "-Utility.CalculateMinutesUntilMorning(Game1.timeOfDay_at_DayUpdate)",
                rainy_day_timer_adjustment_status =
                    hasWeather
                        ? "formula_exact_event_time_not_reconstructed_from_post_event_snapshot"
                        : "blocked_weather_context_not_initialized",
                clears_previous_contents_overnight =
                    false
            },
            current_environment = new
            {
                scan_radius = MushroomLogTreeRadius,
                scan_width = MushroomLogTreeRadius *
                    2 + 1,
                all_tree_count = allTreeCount,
                mature_tree_count = matureTreeCount,
                mature_moss_tree_count =
                    matureMossTreeCount,
                generic_pool_entry_count =
                    genericPoolEntryCount,
                total_pool_entry_count =
                    totalPoolEntryCount,
                nearby_trees = nearbyTrees.Select(
                    row => row.ToSnapshot()).ToArray()
            },
            next_generation_current_environment_distribution =
                new
                {
                    item_distribution =
                        itemDistribution.Select(
                            row => row.ToSnapshot())
                            .ToArray(),
                    amount_distribution =
                        amountDistribution.Select(
                            row => row.ToSnapshot())
                            .ToArray(),
                    quality_distribution =
                        qualityDistribution.Select(
                            row => row.ToSnapshot())
                            .ToArray(),
                    quality_success_chance_raw =
                        qualityChanceRaw,
                    quality_success_chance_effective =
                        qualityChance,
                    item_probability_sum =
                        itemDistribution.Sum(
                            row => row.Probability),
                    amount_probability_sum =
                        amountDistribution.Sum(
                            row => row.Probability),
                    quality_probability_sum =
                        qualityDistribution.Sum(
                            row => row.Probability)
                },
            planning_distribution_status =
                "complete_marginals_for_current_nearby_tree_snapshot",
            realized_output_identity_status =
                "blocked_shared_Game1_random_state_not_read",
            joint_distribution_status =
                "unavailable_shared_rng_draw_order_not_replayed",
            existing_held_item_origin_status =
                heldItem is null
                    ? "not_applicable_no_existing_output"
                    : "exact_held_item_current_generation_environment_not_reconstructed",
            foraging_experience_on_harvest = 5
        };
    }

    private static object BlockedMushroomLogState(
        StardewValley.Object machine,
        string outputMethod,
        string reason)
    {
        return new
        {
            schema_version =
                "mushroom_log_special_state.v1",
            status = "blocked",
            reason,
            special_state_model_id =
                MushroomLogStateModelId,
            qualified_item_id =
                machine.QualifiedItemId,
            runtime_type =
                machine.GetType().FullName ??
                machine.GetType().Name,
            observed_output_method = outputMethod
        };
    }

    private static object? ReadMushroomLogWeather(
        LocationWeather? weather)
    {
        return weather is null
            ? null
            : new
            {
                weather = weather.Weather,
                is_raining = weather.IsRaining,
                is_snowing = weather.IsSnowing,
                is_lightning = weather.IsLightning,
                is_green_rain = weather.IsGreenRain
            };
    }

}
