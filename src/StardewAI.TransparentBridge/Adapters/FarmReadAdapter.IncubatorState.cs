using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static readonly string[] IncubatorNameCatalog =
    {
        "Pip",
        "Miso",
        "Nova",
        "Clover",
        "Sunny",
        "Maple",
        "Juniper",
        "Lumi",
        "Olive",
        "Sage"
    };

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
        var activeIncubatorCount = location.objects.Values.Count(
            candidate =>
                candidate.GetMachineData()?.IsIncubator == true &&
                candidate.heldObject.Value is not null);
        var unreservedHatchSlotCount =
            occupantCount.HasValue &&
            occupantLimit.HasValue
                ? Math.Max(
                    0,
                    occupantLimit.Value -
                    occupantCount.Value -
                    activeIncubatorCount)
                : (int?)null;
        var ready =
            heldEgg is not null &&
            machine.MinutesUntilReady <= 0;
        var readyIncubatorsInNativeOrder = location.objects.Values
            .Where(candidate =>
                candidate.bigCraftable.Value &&
                candidate.GetMachineData()?.IsIncubator == true &&
                candidate.heldObject.Value is not null &&
                candidate.MinutesUntilReady <= 0)
            .ToArray();
        var nativeReadySelectionOrdinal =
            Array.FindIndex(
                readyIncubatorsInNativeOrder,
                candidate => ReferenceEquals(candidate, machine));
        var compatibilityCatalog =
            ReadIncubatorCompatibilityCatalog(machine, location);

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
            active_incubator_count =
                activeIncubatorCount,
            unreserved_hatch_slot_count =
                unreservedHatchSlotCount,
            native_ready_selection_ordinal =
                nativeReadySelectionOrdinal,
            native_ready_selected =
                ready &&
                nativeReadySelectionOrdinal == 0,
            native_ready_selection_contract =
                "AnimalHouse.objects.Values_first_ready_incubator_then_break",
            held_egg_qualified_item_id =
                heldEgg?.QualifiedItemId ?? string.Empty,
            hatch_animal_type_id =
                animalTypeId,
            hatch_animal_data_available =
                animalDataAvailable,
            suggested_hatch_name =
                ReadSuggestedIncubatorName(animalHouse),
            suggested_hatch_name_source =
                "controller_deterministic_first_unused_catalog_name",
            minutes_until_hatch =
                heldEgg is null
                    ? (int?)null
                    : Math.Max(0, machine.MinutesUntilReady),
            completion_interaction_kind =
                "AnimalHouse.resetSharedState_animalNaming_addNewHatchedAnimal",
            ordinary_output_collection_supported =
                false,
            hatch_executor_status =
                "covered_native_naming_menu_confirm",
            compatible_egg_catalog_status =
                compatibilityCatalog.Status,
            native_farm_animal_type_count =
                compatibilityCatalog.NativeAnimalTypeCount,
            native_egg_candidate_count =
                compatibilityCatalog.NativeEggCandidateCount,
            incubator_egg_compatibility_matrix_row_count =
                compatibilityCatalog.MatrixRowCount,
            incubator_egg_compatibility_matrix =
                compatibilityCatalog.MatrixRows,
            compatible_egg_count =
                compatibilityCatalog.CompatibleRows.Length,
            compatible_egg_catalog =
                compatibilityCatalog.CompatibleRows,
            compatibility_catalog_completeness_contract =
                "all_distinct_Game1.farmAnimalData_EggItemIds_" +
                "evaluated_without_row_truncation"
        };
    }

    private static string ReadSuggestedIncubatorName(
        AnimalHouse? animalHouse)
    {
        if (animalHouse is null)
        {
            return string.Empty;
        }

        var used = animalHouse.animals.Values
            .Select(animal => animal.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return IncubatorNameCatalog.FirstOrDefault(
                name => !used.Contains(name)) ??
            "Companion" +
            (animalHouse.animalsThatLiveHere.Count + 1);
    }
}
