using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object? ReadIncubatorSpecialState(
        StardewValley.Object machine,
        GameLocation location)
    {
        if (machine.GetMachineData()?.IsIncubator != true)
        {
            return null;
        }

        var animalHouse = location as AnimalHouse;
        var heldEgg = machine.heldObject.Value;
        var animalTypeId = string.Empty;
        var animalDataAvailable = heldEgg is not null &&
            FarmAnimal.TryGetAnimalDataFromEgg(
                heldEgg,
                location,
                out animalTypeId,
                out _);
        var occupantCount =
            animalHouse?.animalsThatLiveHere.Count;
        var occupantLimit = animalHouse?.animalLimit.Value;
        var hasCapacity =
            occupantCount.HasValue &&
            occupantLimit.HasValue &&
            occupantCount.Value < occupantLimit.Value;
        var ready =
            heldEgg is not null &&
            machine.MinutesUntilReady <= 0;

        return new
        {
            schema_version =
                "machine_special_state.v1",
            status = heldEgg is null
                ? "idle"
                : ready
                    ? hasCapacity
                        ? "ready_requires_native_naming_event"
                        : "ready_blocked_animal_house_full"
                    : "incubating",
            special_prediction_model_id =
                "incubator_animal_hatch.v1",
            location_is_animal_house =
                animalHouse is not null,
            animal_house_occupant_count =
                occupantCount,
            animal_house_occupant_limit =
                occupantLimit,
            animal_house_has_capacity =
                animalHouse is null
                    ? (bool?)null
                    : hasCapacity,
            held_egg_qualified_item_id =
                heldEgg?.QualifiedItemId ?? string.Empty,
            hatch_animal_type_id =
                animalTypeId,
            hatch_animal_data_available =
                animalDataAvailable,
            minutes_until_hatch =
                heldEgg is null
                    ? (int?)null
                    : Math.Max(0, machine.MinutesUntilReady),
            completion_interaction_kind =
                "AnimalHouse.resetSharedState_animalNaming_addNewHatchedAnimal",
            ordinary_output_collection_supported =
                false,
            hatch_executor_status =
                "blocked_native_naming_executor_not_implemented"
        };
    }
}
