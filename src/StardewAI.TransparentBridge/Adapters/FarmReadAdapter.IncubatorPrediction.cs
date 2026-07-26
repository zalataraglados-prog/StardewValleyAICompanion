using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private const string IncubatorPredictionModelId =
        "incubator_animal_hatch.v1";

    private static bool IsVettedIncubatorOutputMethod(
        StardewValley.Object machine,
        string outputMethod)
    {
        return machine.GetMachineData()?.IsIncubator == true &&
            machine.GetType() == typeof(StardewValley.Object) &&
            outputMethod.StartsWith(
                "StardewValley.Object,",
                StringComparison.Ordinal) &&
            outputMethod.EndsWith(
                ": OutputIncubator",
                StringComparison.Ordinal);
    }

    private static bool TryReadIncubatorPrediction(
        StardewValley.Object machine,
        Item inputItem,
        MachineOutputRule outputRule,
        MachineOutputTriggerRule? triggerRule,
        MachineItemOutput outputData,
        out object prediction)
    {
        prediction = new { status = "unavailable" };
        if (!IsVettedIncubatorOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            return false;
        }

        if (machine.Location is not AnimalHouse house)
        {
            prediction = BlockedIncubatorPrediction(
                "incubator_location_is_not_animal_house");
            return true;
        }

        var activeIncubatorCount = house.objects.Values.Count(
            candidate =>
                candidate.GetMachineData()?.IsIncubator == true &&
                candidate.heldObject.Value is not null);
        var unreservedHatchSlotCount = Math.Max(
            0,
            house.animalLimit.Value -
            house.animalsThatLiveHere.Count -
            activeIncubatorCount);
        if (unreservedHatchSlotCount <= 0)
        {
            prediction = BlockedIncubatorPrediction(
                "incubator_animal_house_has_no_unreserved_hatch_slot");
            return true;
        }

        if (!FarmAnimal.TryGetAnimalDataFromEgg(
                inputItem,
                house,
                out var animalTypeId,
                out var animalData))
        {
            prediction = BlockedIncubatorPrediction(
                "incubator_egg_animal_data_unavailable");
            return true;
        }

        var outputItem = StardewValley.Object.OutputIncubator(
            machine,
            inputItem,
            probe: true,
            outputData,
            Game1.player,
            out var overrideMinutesUntilReady);
        if (outputItem is null ||
            !overrideMinutesUntilReady.HasValue ||
            overrideMinutesUntilReady.Value <= 0)
        {
            prediction = BlockedIncubatorPrediction(
                "incubator_native_probe_returned_no_hatch");
            return true;
        }

        var purchaseEquivalentValue =
            animalData.PurchasePrice >= 0
                ? animalData.PurchasePrice
                : Math.Max(0, animalData.SellPrice);
        prediction = new
        {
            status = "available",
            training_eligibility_status =
                ExactMachinePredictionStatus,
            source =
                "decompiled_Object.OutputIncubator_vetted_native_probe",
            special_prediction_model_id =
                IncubatorPredictionModelId,
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
                overrideMinutesUntilReady.Value,
            hatch_animal_type_id = animalTypeId,
            animal_house_occupant_count =
                house.animalsThatLiveHere.Count,
            animal_house_occupant_limit =
                house.animalLimit.Value,
            active_incubator_count =
                activeIncubatorCount,
            unreserved_hatch_slot_count =
                unreservedHatchSlotCount,
            animal_purchase_equivalent_value =
                purchaseEquivalentValue,
            animal_value_basis =
                animalData.PurchasePrice >= 0
                    ? "native_purchase_price"
                    : "native_base_sell_price",
            days_to_mature = animalData.DaysToMature,
            days_to_produce = animalData.DaysToProduce,
            suggested_hatch_name =
                ReadSuggestedIncubatorName(house),
            completion_interaction_kind =
                "AnimalHouse.resetSharedState_animalNaming_addNewHatchedAnimal",
            rng_safety_status =
                "vetted_native_probe_no_rng_or_state_mutation"
        };
        return true;
    }

    private static object BlockedIncubatorPrediction(
        string reason)
    {
        return new
        {
            status = "blocked",
            reason,
            special_prediction_model_id =
                IncubatorPredictionModelId,
            training_eligibility_status =
                "blocked_incubator_model_precondition"
        };
    }
}
