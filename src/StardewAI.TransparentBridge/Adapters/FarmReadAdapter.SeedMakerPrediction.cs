using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string SeedMakerPredictionModelId =
        "seed_maker_day_save_rng.v1";
    private const string SeedMakerQualifiedItemId = "(BC)25";

    private static bool IsVettedSeedMakerOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.QualifiedItemId == SeedMakerQualifiedItemId &&
            machine.GetType() == typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputSeedMaker",
                StringComparison.Ordinal);
    }

    private static bool TryReadSeedMakerPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (!IsVettedSeedMakerOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        var machineData = machine.GetMachineData();
        if (machineData?.ReadyTimeModifiers?.Count > 0)
        {
            prediction = BlockedSeedMakerPrediction(
                "seed_maker_ready_time_modifiers_not_modeled");
            return true;
        }

        var sourceCrop = Game1.cropData.FirstOrDefault(pair =>
            ItemRegistry.HasItemId(
                inputItem,
                pair.Value.HarvestItemId));
        if (string.IsNullOrWhiteSpace(sourceCrop.Key))
        {
            prediction = BlockedSeedMakerPrediction(
                "seed_maker_input_has_no_live_crop_mapping");
            return true;
        }

        var outputItem = StardewValley.Object.OutputSeedMaker(
            machine,
            inputItem,
            probe: true,
            outputData,
            Game1.player,
            out var overrideMinutesUntilReady);
        if (outputItem is null)
        {
            prediction = BlockedSeedMakerPrediction(
                "seed_maker_native_prediction_returned_null");
            return true;
        }

        var ruleMinutesUntilReady =
            ReadIntNullable(outputRule, "MinutesUntilReady") ?? -1;
        var ruleDaysUntilReady =
            ReadIntNullable(outputRule, "DaysUntilReady") ?? -1;
        var effectiveMinutesUntilReady =
            overrideMinutesUntilReady ??
            (ruleDaysUntilReady >= 0
                ? Utility.CalculateMinutesUntilMorning(
                    Game1.timeOfDay,
                    ruleDaysUntilReady)
                : ruleMinutesUntilReady);
        if (effectiveMinutesUntilReady < 0)
        {
            prediction = BlockedSeedMakerPrediction(
                "seed_maker_duration_unavailable");
            return true;
        }

        var branch = outputItem.QualifiedItemId switch
        {
            "(O)499" => "ancient_seeds",
            "(O)770" => "mixed_seeds",
            _ when outputItem.ItemId == sourceCrop.Key =>
                "source_crop_seeds",
            _ => "unrecognized_output"
        };
        if (branch == "unrecognized_output")
        {
            prediction = BlockedSeedMakerPrediction(
                "seed_maker_output_branch_unrecognized");
            return true;
        }

        prediction = new
        {
            status = "available",
            training_eligibility_status =
                ExactMachinePredictionStatus,
            source =
                "decompiled_Object.OutputSeedMaker_vetted_native_probe",
            special_prediction_model_id =
                SeedMakerPredictionModelId,
            vetted_output_method =
                outputData.OutputMethod ?? string.Empty,
            matched_rule_id = outputRule.Id ?? string.Empty,
            required_item_id =
                triggerRule?.RequiredItemId ?? string.Empty,
            required_tags =
                triggerRule?.RequiredTags?.ToArray() ??
                Array.Empty<string>(),
            required_count = triggerRule?.RequiredCount ?? 0,
            item = SummarizeItem(outputItem),
            sale_price = outputItem.salePrice(),
            stack = outputItem.Stack,
            quality = outputItem.Quality,
            effective_minutes_until_ready =
                effectiveMinutesUntilReady,
            source_crop_seed_item_id = sourceCrop.Key,
            source_crop_harvest_item_id =
                sourceCrop.Value.HarvestItemId,
            output_branch = branch,
            rng_seed_inputs = new
            {
                save_days_played = Game1.stats.DaysPlayed,
                save_id_half =
                    Game1.uniqueIDForThisGame / 2,
                machine_tile_x = machine.TileLocation.X,
                machine_tile_y_times_77 =
                    machine.TileLocation.Y * 77f,
                time_of_day = Game1.timeOfDay
            },
            rng_safety_status =
                "vetted_native_probe_uses_fresh_Utility_CreateDaySaveRandom",
            output_distribution = new
            {
                ancient_seeds_probability = 0.005,
                mixed_seeds_probability = 0.0199,
                source_crop_seeds_probability = 0.9751,
                source_crop_seed_stack_min = 1,
                source_crop_seed_stack_max = 3,
                mixed_seed_stack_min = 1,
                mixed_seed_stack_max = 4
            }
        };
        return true;
    }

    private static object BlockedSeedMakerPrediction(string reason)
    {
        return new
        {
            status = "blocked",
            reason,
            special_prediction_model_id =
                SeedMakerPredictionModelId,
            training_eligibility_status =
                "blocked_seed_maker_model_precondition"
        };
    }
}
