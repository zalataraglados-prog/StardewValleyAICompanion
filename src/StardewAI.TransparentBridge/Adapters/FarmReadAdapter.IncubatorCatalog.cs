using StardewValley;
using StardewValley.GameData.Machines;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static IncubatorCompatibilityCatalog
        ReadIncubatorCompatibilityCatalog(
            StardewValley.Object machine,
            GameLocation location)
    {
        if (location is not AnimalHouse house)
        {
            return new IncubatorCompatibilityCatalog(
                "blocked_location_is_not_animal_house",
                0,
                0,
                0,
                Array.Empty<object>(),
                Array.Empty<object>());
        }

        var machineData = machine.GetMachineData();
        if (machineData?.IsIncubator != true ||
            Game1.player is null)
        {
            return new IncubatorCompatibilityCatalog(
                "blocked_machine_context_unavailable",
                0,
                0,
                0,
                Array.Empty<object>(),
                Array.Empty<object>());
        }

        var eggIds = Game1.farmAnimalData.Values
            .Where(data => data.EggItemIds is not null)
            .SelectMany(data => data.EggItemIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rows = new List<object>();
        var compatibleRows = new List<object>();

        foreach (var eggItemId in eggIds)
        {
            var qualifiedEggId = eggItemId.StartsWith(
                    "(",
                    StringComparison.Ordinal)
                ? eggItemId
                : "(O)" + eggItemId;
            StardewValley.Object egg;
            try
            {
                egg = ItemRegistry.Create<StardewValley.Object>(
                    qualifiedEggId);
            }
            catch (Exception ex)
            {
                rows.Add(new
                {
                    egg_item_id = eggItemId,
                    egg_qualified_item_id = qualifiedEggId,
                    compatible = false,
                    status = "blocked_item_registry_create_failed",
                    exception_type = ex.GetType().Name
                });
                continue;
            }

            var globallyMatchingAnimalTypeIds =
                Game1.farmAnimalData
                    .Where(pair =>
                        pair.Value.EggItemIds?.Contains(
                            egg.ItemId) == true)
                    .Select(pair => pair.Key)
                    .ToArray();
            var animalResolved =
                FarmAnimal.TryGetAnimalDataFromEgg(
                    egg,
                    house,
                    out var animalTypeId,
                    out var animalData);
            var machineRuleResolved =
                TryResolveIncubatorMachineRule(
                    machine,
                    machineData,
                    house,
                    egg,
                    out var outputRule,
                    out var triggerRule,
                    out var outputData,
                    out var machineRuleReason);

            Item? nativeProbeOutput = null;
            int? nativeDuration = null;
            if (animalResolved &&
                machineRuleResolved &&
                outputData is not null)
            {
                nativeProbeOutput =
                    StardewValley.Object.OutputIncubator(
                        machine,
                        egg,
                        probe: true,
                        outputData,
                        Game1.player,
                        out nativeDuration);
            }

            var compatible =
                animalResolved &&
                machineRuleResolved &&
                nativeProbeOutput is not null &&
                nativeDuration > 0;
            var status = compatible
                ? "compatible_exact_native_probe"
                : !animalResolved
                    ? "incompatible_building_occupant_type"
                    : !machineRuleResolved
                        ? machineRuleReason
                        : "blocked_native_output_probe_failed";
            var purchaseEquivalentValue =
                animalResolved
                    ? animalData.PurchasePrice >= 0
                        ? animalData.PurchasePrice
                        : Math.Max(0, animalData.SellPrice)
                    : (int?)null;
            var row = new
            {
                egg_item_id = egg.ItemId,
                egg_qualified_item_id = egg.QualifiedItemId,
                egg_display_name = egg.DisplayName,
                egg_sale_price = egg.salePrice(),
                globally_matching_animal_type_ids =
                    globallyMatchingAnimalTypeIds,
                building_valid_occupant_types =
                    house.ParentBuilding?.GetData()?
                        .ValidOccupantTypes?.ToArray() ??
                    Array.Empty<string>(),
                native_animal_resolution_supported =
                    animalResolved,
                hatch_animal_type_id =
                    animalResolved ? animalTypeId : string.Empty,
                animal_house_type =
                    animalResolved ? animalData.House : string.Empty,
                native_machine_rule_supported =
                    machineRuleResolved,
                matched_rule_id =
                    outputRule?.Id ?? string.Empty,
                required_item_id =
                    triggerRule?.RequiredItemId ?? string.Empty,
                required_count =
                    triggerRule?.RequiredCount ?? 0,
                vetted_output_method =
                    outputData?.OutputMethod ?? string.Empty,
                effective_minutes_until_ready =
                    nativeDuration,
                animal_purchase_equivalent_value =
                    purchaseEquivalentValue,
                animal_value_basis =
                    !animalResolved
                        ? string.Empty
                        : animalData.PurchasePrice >= 0
                            ? "native_purchase_price"
                            : "native_base_sell_price",
                days_to_mature =
                    animalResolved
                        ? animalData.DaysToMature
                        : (int?)null,
                days_to_produce =
                    animalResolved
                        ? animalData.DaysToProduce
                        : (int?)null,
                compatible,
                status,
                native_contract =
                    "Game1.farmAnimalData_EggItemIds_then_" +
                    "MachineDataUtility.TryGetMachineOutputRule_then_" +
                    "FarmAnimal.TryGetAnimalDataFromEgg_then_" +
                    "Object.OutputIncubator_probe_true"
            };
            rows.Add(row);
            if (compatible)
            {
                compatibleRows.Add(row);
            }
        }

        return new IncubatorCompatibilityCatalog(
            "available_complete_native_data_and_rule_probe",
            Game1.farmAnimalData.Count,
            eggIds.Length,
            rows.Count,
            rows.ToArray(),
            compatibleRows.ToArray());
    }

    private static bool TryResolveIncubatorMachineRule(
        StardewValley.Object machine,
        MachineData machineData,
        AnimalHouse house,
        Item egg,
        out MachineOutputRule? outputRule,
        out MachineOutputTriggerRule? triggerRule,
        out MachineItemOutput? outputData,
        out string reason)
    {
        outputRule = null;
        triggerRule = null;
        outputData = null;
        reason = "incompatible_machine_output_rule";

        if (!MachineInputProbeIsRngSafe(machineData))
        {
            reason =
                "blocked_random_trigger_condition_read_would_advance_game_rng";
            return false;
        }

        if (!MachineDataUtility.TryGetMachineOutputRule(
                machine,
                machineData,
                MachineOutputTrigger.ItemPlacedInMachine,
                egg,
                Game1.player,
                house,
                out outputRule,
                out triggerRule,
                out _,
                out _))
        {
            return false;
        }

        var outputs = outputRule.OutputItem;
        if (outputs is null || outputs.Count == 0)
        {
            reason = "blocked_machine_output_item_unavailable";
            return false;
        }

        if (outputs.Any(output =>
                ConditionUsesRandomQuery(output.Condition) ||
                ConditionUsesRandomQuery(
                    ReadString(output, "PerItemCondition"))))
        {
            reason =
                "blocked_random_output_condition_read_would_advance_game_rng";
            return false;
        }

        var validOutputs = outputs
            .Where(output => GameStateQuery.CheckConditions(
                output.Condition,
                house,
                Game1.player,
                null,
                egg))
            .ToArray();
        outputData = outputRule.UseFirstValidOutput
            ? validOutputs.FirstOrDefault()
            : validOutputs.Length == 1
                ? validOutputs[0]
                : null;
        if (outputData is null)
        {
            reason = !outputRule.UseFirstValidOutput &&
                validOutputs.Length > 1
                    ? "blocked_random_output_choice"
                    : "blocked_machine_output_data_unavailable";
            return false;
        }

        if (!IsVettedIncubatorOutputMethod(
                machine,
                outputData.OutputMethod ?? string.Empty))
        {
            reason = "blocked_unvetted_incubator_output_method";
            return false;
        }

        return true;
    }

    private sealed record IncubatorCompatibilityCatalog(
        string Status,
        int NativeAnimalTypeCount,
        int NativeEggCandidateCount,
        int MatrixRowCount,
        object[] MatrixRows,
        object[] CompatibleRows);
}
