using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[]
            CompileNameHatchedAnimalStep(
                SmallModelAction action)
        {
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var location = ReadParameter(
                action,
                "machine_location_id");
            var animalType = ReadParameter(
                action,
                "target_runtime_type");
            var targetName = ReadParameter(action, "target_name");
            if (!x.HasValue ||
                !y.HasValue ||
                string.IsNullOrWhiteSpace(location) ||
                string.IsNullOrWhiteSpace(animalType) ||
                string.IsNullOrWhiteSpace(targetName))
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "name_hatched_animal",
                    location +
                    "(" +
                    x.Value +
                    "," +
                    y.Value +
                    "):" +
                    animalType +
                    ":" +
                    targetName,
                    "farm.machines[" +
                    location +
                    ":" +
                    x.Value +
                    "," +
                    y.Value +
                    "].held_item=null;animal_house.animals.count+=1;animal.name=" +
                    targetName +
                    ";animal.type=" +
                    animalType +
                    ";menus.active_menu.is_open=false",
                    30)
            };
        }

        private static string[] ValidateNameHatchedAnimalPlan(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (action.OptionId !=
                "executor.name_hatched_animal")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            var x = ReadIntParameter(action, "target_tile_x");
            var y = ReadIntParameter(action, "target_tile_y");
            var targetLocation = ReadParameter(
                action,
                "target_location");
            var machineLocation = ReadParameter(
                action,
                "machine_location_id");
            var animalType = ReadParameter(
                action,
                "target_runtime_type");
            var targetName = ReadParameter(action, "target_name");
            var eggId = ReadParameter(
                action,
                "held_egg_qualified_item_id");
            var occupantCount = ReadIntParameter(
                action,
                "animal_house_occupant_count_before");
            var occupantLimit = ReadIntParameter(
                action,
                "animal_house_occupant_limit");
            if (!x.HasValue ||
                !y.HasValue ||
                string.IsNullOrWhiteSpace(targetLocation) ||
                !string.Equals(
                    targetLocation,
                    machineLocation,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    targetLocation,
                    ReadStateFieldString(
                        snapshot,
                        "player",
                        "location_id"),
                    StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(
                    "incubator_hatch_target_location_or_tile_invalid");
            }

            if (string.IsNullOrWhiteSpace(targetName) ||
                targetName.Length > 32 ||
                string.IsNullOrWhiteSpace(animalType) ||
                string.IsNullOrWhiteSpace(eggId) ||
                !occupantCount.HasValue ||
                !occupantLimit.HasValue ||
                occupantCount.Value >= occupantLimit.Value ||
                !string.Equals(
                    ReadParameter(
                        action,
                        "machine_special_prediction_model_id"),
                    "incubator_animal_hatch.v1",
                    StringComparison.Ordinal) ||
                ReadIntParameter(
                    action,
                    "native_ready_selection_ordinal") != 0 ||
                !string.Equals(
                    ReadParameter(action, "native_contract"),
                    "NamingMenu.receiveLeftClick_doneNamingButton_then_textBoxEnter_then_doneNaming_AnimalHouse.addNewHatchedAnimal",
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "incubator_hatch_typed_contract_incomplete");
            }

            if (!string.Equals(
                    ActiveMenuType(snapshot),
                    "NamingMenu",
                    StringComparison.Ordinal))
            {
                reasons.Add(
                    "incubator_native_naming_menu_not_open");
            }

            var menuState = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            if (!menuState.HasValue ||
                menuState.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(menuState.Value, "kind"),
                    "naming",
                    StringComparison.Ordinal) ||
                ReadBool(
                    menuState.Value,
                    "done_callback_present") != true ||
                ReadBool(
                    menuState.Value,
                    "done_button_present") != true)
            {
                reasons.Add(
                    "incubator_naming_menu_contract_unavailable");
            }

            JsonElement? machine = null;
            if (x.HasValue && y.HasValue)
            {
                machine = MachineAt(
                    snapshot,
                    targetLocation,
                    x.Value,
                    y.Value);
            }

            if (!machine.HasValue ||
                !MachineUsesIncubatorCompletion(machine.Value) ||
                !machine.Value.TryGetProperty(
                    "machine_special_state",
                    out var special) ||
                special.ValueKind != JsonValueKind.Object)
            {
                reasons.Add(
                    "incubator_hatch_machine_not_transparently_bound");
                return reasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            if (!string.Equals(
                    ReadString(special, "status"),
                    "ready_requires_native_naming_event",
                    StringComparison.Ordinal) ||
                ReadBool(special, "native_ready_selected") != true ||
                ReadInt(
                    special,
                    "native_ready_selection_ordinal") != 0 ||
                ReadBool(
                    special,
                    "animal_house_has_capacity") != true ||
                !string.Equals(
                    ReadString(
                        special,
                        "held_egg_qualified_item_id"),
                    eggId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(
                        special,
                        "hatch_animal_type_id"),
                    animalType,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(
                        special,
                        "suggested_hatch_name"),
                    targetName,
                    StringComparison.Ordinal) ||
                ReadInt(
                    special,
                    "animal_house_occupant_count") !=
                    occupantCount ||
                ReadInt(
                    special,
                    "animal_house_occupant_limit") !=
                    occupantLimit)
            {
                reasons.Add(
                    "incubator_hatch_projection_drifted");
            }

            return reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
