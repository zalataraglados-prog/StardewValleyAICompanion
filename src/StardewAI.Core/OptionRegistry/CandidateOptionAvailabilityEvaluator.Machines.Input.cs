using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] MachineLoadInputCandidates(
            SnapshotEnvelope snapshot,
            JsonElement machine,
            string machineLocation,
            int x,
            int y,
            int playerX,
            int playerY,
            MachineStandTileSelection standTile,
            StrategyCommitmentLedger? commitmentLedger)
        {
            if (!machine.TryGetProperty("loadable_inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var machineBusy = ReadInt(machine, "minutes_until_ready") > 0 || ReadBool(machine, "ready_for_harvest") == true;
            var machineData = machine.TryGetProperty("machine_data", out var data) && data.ValueKind == JsonValueKind.Object
                ? data
                : default;
            var outputRuleCount = machineData.ValueKind == JsonValueKind.Object ? Math.Max(0, ReadInt(machineData, "output_rule_count")) : 0;
            var hasMachineDataOutput = machineData.ValueKind == JsonValueKind.Object && ReadBool(machineData, "has_output") == true;
            var machineUsesIncubatorCompletion =
                MachineUsesIncubatorCompletion(machine);
            var machineExecutionSemantics =
                machine.TryGetProperty("machine_execution_semantics", out var semantics) &&
                semantics.ValueKind == JsonValueKind.Object
                    ? semantics
                    : default;
            var machineExecutionStatus = machineExecutionSemantics.ValueKind == JsonValueKind.Object
                ? ReadString(machineExecutionSemantics, "execution_status")
                : string.Empty;
            var inputDispatchKind = machineExecutionSemantics.ValueKind == JsonValueKind.Object
                ? ReadString(machineExecutionSemantics, "input_dispatch_kind")
                : string.Empty;
            var inventoryStacks = InventoryStacksByQualifiedId(snapshot);
            var machineQualifiedItemId = ReadString(
                machine,
                "qualified_item_id");
            var supportIntent =
                MachineSupportIntentProjection.SelectForLoad(
                    commitmentLedger,
                    machineQualifiedItemId,
                    machineLocation,
                    x,
                    y);
            return inputs.EnumerateArray()
                .Where(input => input.ValueKind == JsonValueKind.Object)
                .Select(input =>
                {
                    var slotIndex = ReadInt(input, "slot_index");
                    var qualifiedItemId = ReadString(input, "qualified_item_id");
                    var itemId = ReadString(input, "item_id");
                    var inputStack = Math.Max(1, ReadInt(input, "stack"));
                    var inputSalePrice = Math.Max(0, ReadInt(input, "sale_price"));
                    var prediction = PredictMachineOutputFromProbe(input, machineData, qualifiedItemId, itemId, inputSalePrice, inventoryStacks) ??
                        PredictMachineOutputFromSummary(machineData, qualifiedItemId, itemId, inputSalePrice, inventoryStacks);
                    var loadExecutorStatus = ReadString(input, "load_executor_status");
                    var predictionTrainingStatus = ReadMachinePredictionTrainingStatus(input);
                    var predictionTrainingContract =
                        ReadMachinePredictionTrainingContract(
                            input,
                            qualifiedItemId);
                    var anvilLoadout =
                        predictionTrainingContract.Kind ==
                            "complete_distribution"
                            ? AnvilReforgeLoadoutProjection
                                .Read(
                                    snapshot,
                                    predictionTrainingContract
                                        .OutcomeKind)
                            : AnvilReforgeLoadout.Blocked;
                    var incubatorPrediction =
                        ReadIncubatorPrediction(input);
                    var blockReasons = new List<string>();
                    if (machineBusy)
                    {
                        blockReasons.Add("machine_input_target_busy");
                    }
                    if (machineUsesIncubatorCompletion &&
                        incubatorPrediction is null)
                    {
                        blockReasons.Add(
                            "machine_input_requires_incubator_hatch_value_model");
                    }

                    if (standTile.Tile is null)
                    {
                        blockReasons.AddRange(standTile.BlockReasons);
                    }

                    if (slotIndex < 0)
                    {
                        blockReasons.Add("machine_input_slot_unavailable");
                    }

                    if (string.IsNullOrWhiteSpace(qualifiedItemId) && string.IsNullOrWhiteSpace(itemId))
                    {
                        blockReasons.Add("machine_input_item_id_unavailable");
                    }
                    if (machineExecutionStatus is not ("available_data_driven" or "available_native_runtime_override"))
                    {
                        blockReasons.Add("machine_execution_semantics_not_supported");
                    }
                    if (!string.Equals(loadExecutorStatus, "covered_for_runtime_load", StringComparison.Ordinal))
                    {
                        blockReasons.Add("machine_input_runtime_load_not_verified");
                    }
                    if (!predictionTrainingContract.Supported)
                    {
                        blockReasons.Add(
                            "machine_output_not_trainable");
                    }
                    if (predictionTrainingContract.Kind ==
                            "complete_distribution" &&
                        !anvilLoadout.Supported)
                    {
                        blockReasons.Add(
                            "anvil_reforge_loadout_context_unavailable");
                    }

                    var distance = standTile.Tile is null ? 0 : Math.Abs(playerX - standTile.Tile.X) + Math.Abs(playerY - standTile.Tile.Y);
                    var anvilLoadoutEffect =
                        anvilLoadout.Supported
                            ? AnvilLoadoutExpectedEffect(
                                anvilLoadout)
                            : string.Empty;
                    var supportContinuation =
                        MachineSupportIntentProjection.Load(
                            supportIntent,
                            MachineSupportIntentProjection
                                .CurrentInputNetValue(
                                    machine,
                                    input));
                    var reservationGuard = supportIntent is null ||
                        commitmentLedger is null
                            ? null
                            : new MachineInputMaterialReservationGuard()
                                .Evaluate(
                                    snapshot,
                                    commitmentLedger,
                                    slotIndex,
                                    qualifiedItemId,
                                    MachineSupportIntentProjection
                                        .RequiredInputCount(input));
                    if (supportIntent is not null &&
                        !string.Equals(
                            supportContinuation.Status,
                            "active",
                            StringComparison.Ordinal))
                    {
                        blockReasons.Add(
                            "machine_support_current_input_not_positive");
                    }
                    if (reservationGuard is not null &&
                        !reservationGuard.Ready)
                    {
                        blockReasons.AddRange(
                            reservationGuard.BlockingReasons);
                    }
                    return new EventCandidate
                    {
                        CandidateId = "machine-input:" + machineLocation + ":" + x + "," + y + ":slot" + slotIndex + ":" + (string.IsNullOrWhiteSpace(qualifiedItemId) ? itemId : qualifiedItemId),
                        Kind = "load_machine_input_tile",
                        Available = blockReasons.Count == 0,
                        LocationId = machineLocation,
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = (standTile.Tile is null ? string.Empty : "move_to_adjacent=" + standTile.Tile.X + "," + standTile.Tile.Y + ";") +
                            MachineStatePath(machineLocation, x, y) + ".minutes_until_ready>0_or_ready=true" +
                            ";input_slot_index=" + slotIndex +
                            (!string.IsNullOrWhiteSpace(qualifiedItemId) ? ";qualified_item_id=" + qualifiedItemId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(itemId) ? ";item_id=" + itemId : string.Empty) +
                            ";input_stack_available=" + inputStack +
                            ";input_sale_price=" + inputSalePrice +
                            ";machine_input_opportunity_cost=" + inputSalePrice +
                            ";machine_input_value_basis=" + prediction.ValueBasis +
                            ";machine_output_rule_count=" + outputRuleCount +
                            ";machine_has_output_rule=" + hasMachineDataOutput.ToString().ToLowerInvariant() +
                            ";machine_output_prediction_status=" + prediction.Status +
                            prediction.ExpectedEffectSuffix +
                            ";machine_input_probe_source=Object.performObjectDropInAction(probe:true)" +
                            ";machine_input_executor_status=" + loadExecutorStatus +
                            ";machine_execution_status=" + machineExecutionStatus +
                            ";machine_input_dispatch_kind=" + inputDispatchKind +
                            ";machine_prediction_training_status=" + predictionTrainingStatus +
                            ";machine_prediction_training_kind=" +
                            predictionTrainingContract.Kind +
                            (!string.IsNullOrWhiteSpace(
                                predictionTrainingContract.Fingerprint)
                                ? ";machine_prediction_contract_fingerprint=" +
                                  predictionTrainingContract.Fingerprint
                                : string.Empty) +
                            (!string.IsNullOrWhiteSpace(
                                predictionTrainingContract.OutcomeKind)
                                ? ";machine_output_distribution_outcome_kind=" +
                                  predictionTrainingContract.OutcomeKind
                                : string.Empty) +
                            anvilLoadoutEffect +
                            (supportIntent is null
                                ? string.Empty
                                : MachineSupportIntentProjection
                                    .ExpectedEffectSuffix(
                                        supportContinuation) +
                                  MachineInputReservationExpectedEffect(
                                      reservationGuard!,
                                      MachineSupportIntentProjection
                                          .RequiredInputCount(input))),
                        ItemId = itemId,
                        QualifiedItemId = qualifiedItemId,
                        SlotIndex = slotIndex,
                        Quantity = inputStack,
                        EstimatedTicks = Math.Max(90, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_machine_input_runtime_load",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = new[]
                        {
                            Parameter("machine_location_id", machineLocation),
                            Parameter(
                                "machine_output_prediction_status",
                                prediction.Status),
                            Parameter(
                                "predicted_output_qualified_item_id",
                                prediction.OutputQualifiedItemId),
                            Parameter(
                                "predicted_output_item_id",
                                prediction.OutputItemId),
                            Parameter(
                                "predicted_output_context_tags_json",
                                JsonSerializer.Serialize(
                                    prediction.OutputContextTags)),
                            Parameter(
                                "predicted_output_additional_consumed_item_count",
                                prediction.AdditionalConsumedItemCount
                                    .ToString()),
                            Parameter(
                                "machine_special_prediction_model_id",
                                incubatorPrediction?.ModelId ??
                                predictionTrainingContract.ModelId),
                            Parameter(
                                "machine_prediction_training_kind",
                                predictionTrainingContract.Kind),
                            Parameter(
                                "machine_prediction_contract_fingerprint",
                                predictionTrainingContract.Fingerprint),
                            Parameter(
                                "machine_output_distribution_outcome_kind",
                                predictionTrainingContract.OutcomeKind),
                            Parameter(
                                "incubator_hatch_animal_type_id",
                                incubatorPrediction?.AnimalTypeId ??
                                string.Empty),
                            Parameter(
                                "incubator_suggested_hatch_name",
                                incubatorPrediction?.SuggestedName ??
                                string.Empty),
                            Parameter(
                                "incubator_unreserved_hatch_slot_count",
                                incubatorPrediction?.UnreservedSlotCount
                                    .ToString() ??
                                string.Empty)
                        }
                        .Concat(
                            AnvilLoadoutParameters(
                                anvilLoadout))
                        .Concat(
                            supportIntent is null
                                ? Array.Empty<
                                    SmallModelActionParameter>()
                                : MachineSupportIntentProjection
                                    .Parameters(
                                        supportContinuation)
                                    .Concat(
                                        MachineInputReservationParameters(
                                            reservationGuard!,
                                            MachineSupportIntentProjection
                                                .RequiredInputCount(input))))
                        .ToArray()
                    };
                })
                .ToArray();
        }

        private static string MachineInputReservationExpectedEffect(
            MachineInputMaterialReservationGuardResult guard,
            int requiredCount) =>
            ";machine_input_required_count=" + requiredCount +
            ";commitment_ledger_id=" + guard.LedgerId +
            ";commitment_ledger_revision=" + guard.LedgerRevision +
            ";material_reservation_guard_status=" + guard.Status +
            ";material_reservation_ledger_id=" + guard.LedgerId +
            ";material_reservation_ledger_revision=" + guard.LedgerRevision +
            ";material_reservation_ids_json=" +
            JsonSerializer.Serialize(guard.ReservationIds);

        private static SmallModelActionParameter[]
            MachineInputReservationParameters(
                MachineInputMaterialReservationGuardResult guard,
                int requiredCount) =>
            [
                Parameter("machine_input_required_count", requiredCount.ToString()),
                Parameter("commitment_ledger_id", guard.LedgerId),
                Parameter("commitment_ledger_revision", guard.LedgerRevision.ToString()),
                Parameter("material_reservation_guard_status", guard.Status),
                Parameter("material_reservation_ledger_id", guard.LedgerId),
                Parameter("material_reservation_ledger_revision", guard.LedgerRevision.ToString()),
                Parameter(
                    "material_reservation_ids_json",
                    JsonSerializer.Serialize(guard.ReservationIds))
            ];

        private static string
            AnvilLoadoutExpectedEffect(
                AnvilReforgeLoadout loadout)
        {
            return
                ";anvil_reforge_loadout_status=" +
                loadout.Status +
                ";anvil_reforge_capability_class=" +
                loadout.CapabilityClass +
                ";anvil_reforge_kill_credit_policy=" +
                loadout.KillCreditPolicy +
                ";anvil_reforge_loot_policy=" +
                loadout.LootPolicy +
                ";anvil_reforge_unlocked_slot_count=" +
                loadout.UnlockedSlotCount +
                ";anvil_reforge_occupied_slot_count=" +
                loadout.OccupiedSlotCount +
                ";anvil_reforge_empty_unlocked_slot_count=" +
                loadout.EmptyUnlockedSlotCount +
                ";anvil_reforge_same_type_equipped_count=" +
                loadout.SameTypeEquippedCount +
                ";anvil_reforge_other_type_equipped_count=" +
                loadout.OtherTypeEquippedCount +
                ";anvil_reforge_loadout_relation=" +
                loadout.Relation;
        }

        private static SmallModelActionParameter[]
            AnvilLoadoutParameters(
                AnvilReforgeLoadout loadout)
        {
            if (!loadout.Supported)
            {
                return Array.Empty<
                    SmallModelActionParameter>();
            }

            return new[]
            {
                Parameter(
                    "anvil_reforge_loadout_status",
                    loadout.Status),
                Parameter(
                    "anvil_reforge_capability_class",
                    loadout.CapabilityClass),
                Parameter(
                    "anvil_reforge_kill_credit_policy",
                    loadout.KillCreditPolicy),
                Parameter(
                    "anvil_reforge_loot_policy",
                    loadout.LootPolicy),
                Parameter(
                    "anvil_reforge_unlocked_slot_count",
                    loadout.UnlockedSlotCount
                        .ToString()),
                Parameter(
                    "anvil_reforge_occupied_slot_count",
                    loadout.OccupiedSlotCount
                        .ToString()),
                Parameter(
                    "anvil_reforge_empty_unlocked_slot_count",
                    loadout.EmptyUnlockedSlotCount
                        .ToString()),
                Parameter(
                    "anvil_reforge_same_type_equipped_count",
                    loadout.SameTypeEquippedCount
                        .ToString()),
                Parameter(
                    "anvil_reforge_other_type_equipped_count",
                    loadout.OtherTypeEquippedCount
                        .ToString()),
                Parameter(
                    "anvil_reforge_loadout_relation",
                    loadout.Relation)
            };
        }

        private static IncubatorInputPrediction?
            ReadIncubatorPrediction(JsonElement input)
        {
            if (!input.TryGetProperty(
                    "predicted_output",
                    out var prediction) ||
                prediction.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(
                        prediction,
                        "training_eligibility_status"),
                    "exact_current_snapshot_probe_supported",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(
                        prediction,
                        "special_prediction_model_id"),
                    "incubator_animal_hatch.v1",
                    StringComparison.Ordinal) ||
                ReadInt(
                    prediction,
                    "unreserved_hatch_slot_count") <= 0)
            {
                return null;
            }

            var animalTypeId = ReadString(
                prediction,
                "hatch_animal_type_id");
            var suggestedName = ReadString(
                prediction,
                "suggested_hatch_name");
            if (string.IsNullOrWhiteSpace(animalTypeId) ||
                string.IsNullOrWhiteSpace(suggestedName))
            {
                return null;
            }

            return new IncubatorInputPrediction(
                "incubator_animal_hatch.v1",
                animalTypeId,
                suggestedName,
                ReadInt(
                    prediction,
                    "unreserved_hatch_slot_count"));
        }

        private EventCandidate[] IncubatorNamingCandidates(
            SnapshotEnvelope snapshot)
        {
            if (!string.Equals(
                    ActiveMenuTypeForCandidate(snapshot),
                    "NamingMenu",
                    StringComparison.Ordinal))
            {
                return Array.Empty<EventCandidate>();
            }

            var menuState = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            var machines = ReadStateFieldValue(
                snapshot,
                "farm",
                "machines");
            var playerLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            if (!menuState.HasValue ||
                menuState.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(menuState.Value, "kind"),
                    "naming",
                    StringComparison.Ordinal) ||
                ReadBool(menuState.Value, "done_callback_present") !=
                    true ||
                ReadBool(menuState.Value, "done_button_present") !=
                    true ||
                !machines.HasValue ||
                machines.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return machines.Value.EnumerateArray()
                .Where(machine =>
                    machine.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(machine, "location_id"),
                        playerLocation,
                        StringComparison.OrdinalIgnoreCase) &&
                    MachineUsesIncubatorCompletion(machine) &&
                    machine.TryGetProperty(
                        "machine_special_state",
                        out var special) &&
                    special.ValueKind == JsonValueKind.Object &&
                    string.Equals(
                        ReadString(special, "status"),
                        "ready_requires_native_naming_event",
                        StringComparison.Ordinal) &&
                    ReadBool(special, "native_ready_selected") == true &&
                    ReadBool(
                        special,
                        "animal_house_has_capacity") == true &&
                    !string.IsNullOrWhiteSpace(
                        ReadString(
                            special,
                            "hatch_animal_type_id")) &&
                    !string.IsNullOrWhiteSpace(
                        ReadString(
                            special,
                            "suggested_hatch_name")))
                .Select(machine =>
                {
                    var special =
                        machine.GetProperty(
                            "machine_special_state");
                    var x = ReadInt(machine, "tile_x");
                    var y = ReadInt(machine, "tile_y");
                    var eggId = ReadString(
                        special,
                        "held_egg_qualified_item_id");
                    var animalType = ReadString(
                        special,
                        "hatch_animal_type_id");
                    var targetName = ReadString(
                        special,
                        "suggested_hatch_name");
                    var occupantCount = ReadInt(
                        special,
                        "animal_house_occupant_count");
                    var occupantLimit = ReadInt(
                        special,
                        "animal_house_occupant_limit");
                    return new EventCandidate
                    {
                        CandidateId =
                            "incubator-hatch-name:" +
                            playerLocation +
                            ":" +
                            x +
                            "," +
                            y +
                            ":" +
                            eggId,
                        Kind = "name_hatched_animal",
                        Available = true,
                        LocationId = playerLocation,
                        TileX = x,
                        TileY = y,
                        EstimatedTicks = 30,
                        EnergyCost = 0,
                        AvailabilityClass =
                            "transparent_native_incubator_naming",
                        ExpectedEffect =
                            MachineStatePath(
                                playerLocation,
                                x,
                                y) +
                            ".held_item=null" +
                            ";animal_house_occupant_count_before=" +
                            occupantCount +
                            ";animal_house_occupant_limit=" +
                            occupantLimit +
                            ";held_egg_qualified_item_id=" +
                            eggId +
                            ";target_runtime_type=" +
                            animalType +
                            ";target_name=" +
                            targetName +
                            ";machine_special_prediction_model_id=incubator_animal_hatch.v1" +
                            ";native_ready_selection_ordinal=0" +
                            ";native_contract=NamingMenu.receiveLeftClick_doneNamingButton_then_textBoxEnter_then_doneNaming_AnimalHouse.addNewHatchedAnimal",
                        BlockReasons = Array.Empty<string>(),
                        Parameters = new[]
                        {
                            Parameter(
                                "machine_location_id",
                                playerLocation),
                            Parameter(
                                "held_egg_qualified_item_id",
                                eggId),
                            Parameter(
                                "target_runtime_type",
                                animalType),
                            Parameter(
                                "target_name",
                                targetName),
                            Parameter(
                                "animal_house_occupant_count_before",
                                occupantCount.ToString()),
                            Parameter(
                                "animal_house_occupant_limit",
                                occupantLimit.ToString()),
                            Parameter(
                                "machine_special_prediction_model_id",
                                "incubator_animal_hatch.v1"),
                            Parameter(
                                "native_ready_selection_ordinal",
                                "0"),
                            Parameter(
                                "native_contract",
                                "NamingMenu.receiveLeftClick_doneNamingButton_then_textBoxEnter_then_doneNaming_AnimalHouse.addNewHatchedAnimal")
                        }
                    };
                })
                .Take(1)
                .ToArray();
        }

        private static bool MachineUsesIncubatorCompletion(
            JsonElement machine)
        {
            if (ReadBool(machine, "machine_is_incubator") ==
                true)
            {
                return true;
            }

            return machine.TryGetProperty(
                    "machine_data",
                    out var machineData) &&
                machineData.ValueKind == JsonValueKind.Object &&
                ReadBool(machineData, "is_incubator") == true;
        }

        private static string ReadMachinePredictionTrainingStatus(JsonElement input)
        {
            if (!input.TryGetProperty("predicted_output", out var predictedOutput) ||
                predictedOutput.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            return ReadString(predictedOutput, "training_eligibility_status");
        }

        private static string MachineStatePath(string locationId, int x, int y)
        {
            return "farm.machines[" + locationId + ":" + x + "," + y + "]";
        }

    }
}
