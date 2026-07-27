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
        private EventCandidate[] MachineProcessingCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            return MachineServiceCandidates(snapshot)
                .Concat(IncubatorNamingCandidates(snapshot))
                .Concat(MachineCraftingCandidates(snapshot, commitmentLedger))
                .Concat(StorageCraftingCandidates(snapshot, commitmentLedger))
                .Concat(MachineRelocationCandidates(
                    snapshot,
                    commitmentLedger))
                .Concat(MachinePlacementCandidates(snapshot, commitmentLedger))
                .Concat(StoragePlacementCandidates(snapshot, commitmentLedger))
                .OrderBy(candidate => candidate.Kind, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate[] MachineServiceCandidates(SnapshotEnvelope snapshot)
        {
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var playerLocation = ReadStateFieldString(snapshot, "player", "location_id");
            var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue);
            return machines.Value.EnumerateArray()
                .Where(machine => machine.ValueKind == JsonValueKind.Object)
                .SelectMany(machine =>
                {
                    var x = ReadInt(machine, "tile_x");
                    var y = ReadInt(machine, "tile_y");
                    var machineLocation = ReadString(machine, "location_id");
                    if (string.IsNullOrWhiteSpace(machineLocation))
                    {
                        machineLocation = "Farm";
                    }
                    var heldItem = machine.TryGetProperty("held_item", out var held) && held.ValueKind == JsonValueKind.Object
                        ? held
                        : default;
                    var outputQualifiedId = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadString(heldItem, "qualified_item_id")
                        : string.Empty;
                    var outputItemId = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadString(heldItem, "item_id")
                        : string.Empty;
                    var outputQuality = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadInt(heldItem, "quality")
                        : 0;
                    var outputStack = heldItem.ValueKind == JsonValueKind.Object
                        ? Math.Max(1, ReadInt(heldItem, "stack"))
                        : 1;
                    var outputSalePrice = heldItem.ValueKind == JsonValueKind.Object
                        ? Math.Max(0, ReadInt(heldItem, "sale_price"))
                        : 0;
                    var outputTotalValue = outputSalePrice * outputStack;
                    var experienceProjectionStatus = ReadString(machine, "harvest_experience_projection_status");
                    var experienceDeltasJson = ReadString(machine, "harvest_experience_deltas_json");
                    var masteryExperienceDelta = ReadIntOptional(machine, "harvest_mastery_experience_delta");
                    var experienceEvidenceValid = TryReadStructuredSkillExperienceDeltas(
                        machine,
                        "harvest_experience_deltas",
                        experienceDeltasJson,
                        out var experienceDeltas);
                    var positiveExperienceDeltas = experienceDeltas
                        .Where(delta => delta.Delta > 0)
                        .ToArray();
                    if (!string.Equals(machineLocation, playerLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        return MachineRemoteRouteCandidates(
                            snapshot,
                            machine,
                            machineLocation,
                            x,
                            y,
                            outputQualifiedId,
                            outputItemId,
                            outputStack,
                            routeCandidates,
                            playerLocation);
                    }

                    var standTile = FindBestMachineStandTile(snapshot, machineLocation, x, y);
                    var blockReasons = new List<string>();
                    if (MachineUsesIncubatorCompletion(machine))
                    {
                        blockReasons.Add(
                            "machine_output_requires_incubator_hatch_flow");
                    }
                    if (ReadBool(machine, "ready_for_harvest") != true)
                    {
                        blockReasons.Add("machine_output_not_ready");
                    }

                    if (heldItem.ValueKind != JsonValueKind.Object ||
                        (string.IsNullOrWhiteSpace(outputQualifiedId) && string.IsNullOrWhiteSpace(outputItemId)))
                    {
                        blockReasons.Add("machine_output_item_unavailable");
                    }

                    if (standTile.Tile is null)
                    {
                        blockReasons.AddRange(standTile.BlockReasons);
                    }

                    if (!InventoryMayAcceptItem(snapshot, outputQualifiedId, outputItemId, outputQuality))
                    {
                        blockReasons.Add("machine_output_inventory_cannot_accept_item");
                    }

                    if (!experienceProjectionStatus.StartsWith("exact_", StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(experienceDeltasJson) ||
                        !masteryExperienceDelta.HasValue ||
                        !experienceEvidenceValid)
                    {
                        blockReasons.Add("machine_harvest_experience_projection_unavailable");
                    }

                    var distance = standTile.Tile is null ? 0 : Math.Abs(playerX - standTile.Tile.X) + Math.Abs(playerY - standTile.Tile.Y);
                    var parameters = new List<SmallModelActionParameter>
                    {
                        Parameter("machine_harvest_experience_raw", ReadString(machine, "harvest_experience_raw")),
                        Parameter("expected_skill_experience_deltas_json", experienceDeltasJson),
                        Parameter("expected_mastery_experience_delta", (masteryExperienceDelta ?? 0).ToString()),
                        Parameter("skill_experience_projection_status", experienceProjectionStatus),
                        Parameter("skill_experience_condition", "native_machine_output_collection"),
                        Parameter("machine_location_id", machineLocation)
                    };
                    if (positiveExperienceDeltas.Length == 1)
                    {
                        var delta = positiveExperienceDeltas[0];
                        parameters.Add(Parameter("skill_experience_skill_id", delta.SkillId));
                        parameters.Add(Parameter("skill_experience_on_success_min", delta.Delta.ToString()));
                        parameters.Add(Parameter("skill_experience_on_success_max", delta.Delta.ToString()));
                    }
                    var outputCandidate = new EventCandidate
                    {
                        CandidateId = "machine-output:" + machineLocation + ":" + x + "," + y + ":" + (string.IsNullOrWhiteSpace(outputQualifiedId) ? outputItemId : outputQualifiedId),
                        Kind = "collect_machine_output_tile",
                        Available = blockReasons.Count == 0,
                        LocationId = machineLocation,
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = (standTile.Tile is null ? string.Empty : "move_to_adjacent=" + standTile.Tile.X + "," + standTile.Tile.Y + ";") +
                            MachineStatePath(machineLocation, x, y) + ".held_item=null" +
                            (!string.IsNullOrWhiteSpace(outputQualifiedId) ? ";qualified_item_id=" + outputQualifiedId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(outputItemId) ? ";item_id=" + outputItemId : string.Empty) +
                            ";output_stack=" + outputStack +
                            ";output_sale_price=" + outputSalePrice +
                            ";output_total_value=" + outputTotalValue +
                            ";machine_value_basis=held_item_sale_price_times_stack" +
                            ";expected_skill_experience_deltas_json=" + experienceDeltasJson +
                            ";expected_mastery_experience_delta=" + (masteryExperienceDelta ?? 0) +
                            ";skill_experience_projection_status=" + experienceProjectionStatus +
                            ";machine_output_executor_status=runtime_collect",
                        ItemId = outputItemId,
                        QualifiedItemId = outputQualifiedId,
                        Quantity = outputStack,
                        EstimatedTicks = Math.Max(90, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_machine_output_runtime_collect",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                        Parameters = parameters.ToArray()
                    };
                    var candidates = new List<EventCandidate> { outputCandidate };
                    candidates.AddRange(MachineLoadInputCandidates(snapshot, machine, machineLocation, x, y, playerX, playerY, standTile));
                    return candidates.ToArray();
                })
                .OrderBy(candidate => candidate.TileY ?? int.MaxValue)
                .ThenBy(candidate => candidate.TileX ?? int.MaxValue)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool TryReadStructuredSkillExperienceDeltas(
            JsonElement source,
            string rowsProperty,
            string serializedDeltas,
            out StructuredSkillExperienceDelta[] deltas)
        {
            deltas = Array.Empty<StructuredSkillExperienceDelta>();
            if (!source.TryGetProperty(rowsProperty, out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            if (!TryParseStructuredSkillExperienceDeltas(rows, out var transparentDeltas))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(serializedDeltas);
                if (!TryParseStructuredSkillExperienceDeltas(document.RootElement, out var serializedRows) ||
                    !transparentDeltas.SequenceEqual(serializedRows))
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            deltas = transparentDeltas;
            return true;
        }

        private static bool TryParseStructuredSkillExperienceDeltas(
            JsonElement rows,
            out StructuredSkillExperienceDelta[] deltas)
        {
            deltas = Array.Empty<StructuredSkillExperienceDelta>();
            if (rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var parsed = new List<StructuredSkillExperienceDelta>();
            var seenSkillIndexes = new HashSet<int>();
            foreach (var row in rows.EnumerateArray())
            {
                var skillId = ReadString(row, "skillId", ReadString(row, "SkillId"));
                var skillIndex = ReadInt(row, "skillIndex", ReadInt(row, "SkillIndex", -1));
                var delta = ReadInt(row, "delta", ReadInt(row, "Delta", -1));
                if (row.ValueKind != JsonValueKind.Object ||
                    skillIndex is < 0 or > 5 ||
                    delta < 0 ||
                    !string.Equals(skillId, NativeSkillId(skillIndex), StringComparison.Ordinal) ||
                    !seenSkillIndexes.Add(skillIndex))
                {
                    return false;
                }

                parsed.Add(new StructuredSkillExperienceDelta(skillId, skillIndex, delta));
            }

            deltas = parsed.ToArray();
            return true;
        }

        private static string NativeSkillId(int skillIndex) => skillIndex switch
        {
            0 => "farming",
            1 => "fishing",
            2 => "foraging",
            3 => "mining",
            4 => "combat",
            5 => "luck",
            _ => string.Empty
        };

        private sealed record StructuredSkillExperienceDelta(string SkillId, int SkillIndex, int Delta);

        private sealed record IncubatorInputPrediction(
            string ModelId,
            string AnimalTypeId,
            string SuggestedName,
            int UnreservedSlotCount);

        private EventCandidate[] MachineLoadInputCandidates(
            SnapshotEnvelope snapshot,
            JsonElement machine,
            string machineLocation,
            int x,
            int y,
            int playerX,
            int playerY,
            MachineStandTileSelection standTile)
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

                    var distance = standTile.Tile is null ? 0 : Math.Abs(playerX - standTile.Tile.X) + Math.Abs(playerY - standTile.Tile.Y);
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
                                : string.Empty),
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
                    };
                })
                .ToArray();
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

        private static MachineOutputPrediction? PredictMachineOutputFromProbe(
            JsonElement input,
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (!input.TryGetProperty("predicted_output", out var predictedOutput) ||
                predictedOutput.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = ReadString(predictedOutput, "status");
            if (!string.Equals(status, "available", StringComparison.OrdinalIgnoreCase))
            {
                var reason = ReadString(predictedOutput, "reason");
                return string.IsNullOrWhiteSpace(reason)
                    ? null
                    : MachineOutputPrediction.Unavailable("machine_native_probe_" + SanitizeStatus(reason));
            }

            var trainingContract =
                MachinePredictionTrainingPolicy.ReadContract(
                    predictedOutput,
                    qualifiedItemId);
            var hasOutputItem =
                predictedOutput.TryGetProperty(
                    "item",
                    out var outputItem) &&
                outputItem.ValueKind ==
                    JsonValueKind.Object;
            if (!hasOutputItem &&
                trainingContract.Kind ==
                    "complete_distribution")
            {
                hasOutputItem =
                    predictedOutput.TryGetProperty(
                        "output_identity",
                        out outputItem) &&
                    outputItem.ValueKind ==
                        JsonValueKind.Object;
            }
            if (!hasOutputItem)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_output_item_unavailable");
            }

            var matchedRuleId = ReadString(predictedOutput, "matched_rule_id");
            var additionalConsumed =
                trainingContract.Kind ==
                    "complete_distribution"
                    ? ReadAdditionalConsumedSummaryFromPrediction(
                        predictedOutput,
                        inventoryStacks)
                    : ReadAdditionalConsumedSummaryForRequiredItem(
                        machineData,
                        qualifiedItemId,
                        itemId,
                        matchedRuleId,
                        inventoryStacks);
            if (!additionalConsumed.HasValue)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_additional_consumption_unpriced");
            }

            var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
            var outputItemId = ReadString(outputItem, "item_id");
            var outputStack = ReadInt(
                predictedOutput,
                "stack");
            if (outputStack <= 0)
            {
                outputStack = Math.Max(1, ReadInt(outputItem, "stack"));
            }

            var outputSalePrice = Math.Max(0, ReadInt(predictedOutput, "sale_price"));
            if (outputSalePrice <= 0)
            {
                outputSalePrice = Math.Max(0, ReadInt(outputItem, "sale_price"));
            }

            var totalValue = outputSalePrice * Math.Max(1, outputStack);
            var additionalValue = additionalConsumed.Value.TotalValue;
            var netValue = totalValue - inputSalePrice - additionalValue;
            var suffix = string.Empty;
            if (!string.IsNullOrWhiteSpace(outputQualifiedId))
            {
                suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
            }
            if (!string.IsNullOrWhiteSpace(outputItemId))
            {
                suffix += ";predicted_output_item_id=" + outputItemId;
            }

            suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                ";predicted_output_sale_price=" + outputSalePrice +
                ";predicted_output_price_source=" +
                (trainingContract.Kind ==
                    "complete_distribution"
                    ? "distribution_output_identity_sale_price"
                    : "machine_native_probe_sale_price") +
                ";predicted_output_total_value=" + totalValue +
                ";machine_additional_consumed_total_value=" + additionalValue +
                ";predicted_output_net_value=" + netValue;
            if (trainingContract.Kind ==
                "complete_distribution")
            {
                var utility =
                    AnvilReforgeUtilityProjection.Read(
                        predictedOutput);
                if (!utility.Supported)
                {
                    return MachineOutputPrediction
                        .Unavailable(
                            "machine_distribution_utility_unavailable");
                }
                suffix +=
                    ";anvil_reforge_utility_status=" +
                    utility.Status +
                    ";anvil_reforge_utility_metric=" +
                    utility.MetricId +
                    ";anvil_reforge_utility_ordering=" +
                    utility.Ordering +
                    ";anvil_reforge_current_utility=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.CurrentUtility) +
                    ";anvil_reforge_expected_utility=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ExpectedUtility) +
                    ";anvil_reforge_expected_utility_delta=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ExpectedDelta) +
                    ";anvil_reforge_improvement_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.ImprovementProbability) +
                    ";anvil_reforge_equal_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.EqualProbability) +
                    ";anvil_reforge_degradation_probability=" +
                    AnvilReforgeUtilityProjection.Format(
                        utility.DegradationProbability) +
                    ";anvil_reforge_decision_class=" +
                    utility.DecisionClass;
            }
            var requiredItemId = ReadString(predictedOutput, "required_item_id");
            if (!string.IsNullOrWhiteSpace(requiredItemId))
            {
                suffix += ";predicted_output_rule_required_item_id=" + requiredItemId;
            }
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                suffix += ";predicted_output_rule_id=" + matchedRuleId;
            }
            var preserveType = ReadString(predictedOutput, "preserve_type");
            if (!string.IsNullOrWhiteSpace(preserveType))
            {
                suffix += ";predicted_output_preserve_type=" + preserveType;
            }
            var preservedItemId = ReadString(predictedOutput, "preserved_item_id");
            if (!string.IsNullOrWhiteSpace(preservedItemId))
            {
                suffix += ";predicted_output_preserved_item_id=" + preservedItemId;
            }
            if (!string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
            {
                suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                    ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
            }
            var minutesUntilReady = ReadInt(predictedOutput, "effective_minutes_until_ready");
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "override_minutes_until_ready");
            }
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "rule_minutes_until_ready");
            }
            if (minutesUntilReady > 0)
            {
                suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
            }
            var daysUntilReady = ReadInt(
                predictedOutput,
                "effective_days_until_ready");
            if (daysUntilReady > 0)
            {
                suffix += ";predicted_days_until_ready=" +
                    daysUntilReady;
            }
            var daysToNextQuality = ReadInt(
                predictedOutput,
                "effective_days_to_next_quality");
            if (daysToNextQuality > 0)
            {
                suffix += ";predicted_days_to_next_quality=" +
                    daysToNextQuality;
            }
            var specialModelId = ReadString(
                predictedOutput,
                "special_prediction_model_id");
            if (!string.IsNullOrWhiteSpace(specialModelId))
            {
                suffix += ";machine_special_prediction_model_id=" +
                    specialModelId;
            }
            if (string.Equals(
                    specialModelId,
                    "incubator_animal_hatch.v1",
                    StringComparison.Ordinal))
            {
                suffix +=
                    ";incubator_hatch_animal_type_id=" +
                    ReadString(
                        predictedOutput,
                        "hatch_animal_type_id") +
                    ";incubator_suggested_hatch_name=" +
                    ReadString(
                        predictedOutput,
                        "suggested_hatch_name") +
                    ";incubator_unreserved_hatch_slot_count=" +
                    ReadInt(
                        predictedOutput,
                        "unreserved_hatch_slot_count") +
                    ";incubator_animal_house_occupant_count=" +
                    ReadInt(
                        predictedOutput,
                        "animal_house_occupant_count") +
                    ";incubator_animal_house_occupant_limit=" +
                    ReadInt(
                        predictedOutput,
                        "animal_house_occupant_limit") +
                    ";incubator_animal_purchase_equivalent_value=" +
                    ReadInt(
                        predictedOutput,
                        "animal_purchase_equivalent_value");
            }
            var initialQuality = ReadInt(
                predictedOutput,
                "initial_quality");
            var projectedFinalQuality = ReadInt(
                predictedOutput,
                "projected_final_quality");
            if (projectedFinalQuality > 0)
            {
                suffix += ";predicted_initial_quality=" +
                    initialQuality +
                    ";predicted_final_quality=" +
                    projectedFinalQuality;
            }
            var agingRate = ReadDouble(
                predictedOutput,
                "aging_rate_per_day");
            if (agingRate > 0)
            {
                suffix += ";predicted_aging_rate_per_day=" +
                    agingRate.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
            }

            return new MachineOutputPrediction(
                trainingContract.Kind ==
                    "complete_distribution"
                    ? "machine_distribution_probe_available"
                    : "machine_native_probe_available",
                additionalValue > 0
                    ? trainingContract.Kind ==
                        "complete_distribution"
                        ? "distribution_identity_value_minus_transparent_input_and_additional_consumed_sale_price"
                        : "machine_native_probe_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                    : "machine_native_probe_total_value_minus_transparent_input_sale_price",
                suffix);
        }

        private static MachineOutputPrediction PredictMachineOutputFromSummary(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object ||
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return MachineOutputPrediction.Unavailable("machine_data_summary_unavailable");
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (!MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ReadString(rule, "condition")) ||
                    !string.IsNullOrWhiteSpace(ReadString(rule, "per_item_condition")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_condition_not_evaluated");
                }

                var additionalSource = ReadInt(machineData, "additional_consumed_item_count") > 0
                    ? machineData
                    : rule;
                var additionalConsumed = ReadAdditionalConsumedSummary(additionalSource, inventoryStacks);
                if (ReadInt(additionalSource, "additional_consumed_item_count") > 0 && !additionalConsumed.HasValue)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_additional_consumption_unpriced");
                }

                if (!rule.TryGetProperty("output_item", out var outputItem) || outputItem.ValueKind != JsonValueKind.Object)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_without_output_item");
                }

                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "condition")) ||
                    !string.IsNullOrWhiteSpace(ReadString(outputItem, "per_item_condition")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_output_condition_not_evaluated");
                }
                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "output_method")) ||
                    (outputItem.TryGetProperty("random_item_ids", out var randomIds) &&
                     randomIds.ValueKind == JsonValueKind.Array && randomIds.GetArrayLength() > 0))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_dynamic_output_not_priced");
                }

                var copyPrice = ReadBool(outputItem, "copy_price") == true;
                if (ReadBool(outputItem, "copy_quality") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_quality_not_priced");
                }

                if (ReadBool(outputItem, "copy_color") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_color_not_priced");
                }

                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_type")) ||
                    !string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_id")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_preserve_not_priced");
                }

                var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
                var outputItemId = ReadString(outputItem, "item_id");
                var outputStack = ReadInt(outputItem, "stack");
                if (outputStack <= 0)
                {
                    outputStack = Math.Max(1, ReadInt(outputItem, "min_stack"));
                }

                var outputSalePrice = copyPrice ? inputSalePrice : Math.Max(0, ReadInt(outputItem, "sale_price"));
                var totalValue = outputSalePrice * Math.Max(1, outputStack);
                var additionalValue = additionalConsumed.HasValue ? additionalConsumed.Value.TotalValue : 0;
                var netValue = totalValue - inputSalePrice - additionalValue;
                var suffix = string.Empty;
                if (!string.IsNullOrWhiteSpace(outputQualifiedId))
                {
                    suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
                }
                if (!string.IsNullOrWhiteSpace(outputItemId))
                {
                    suffix += ";predicted_output_item_id=" + outputItemId;
                }

                suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                    ";predicted_output_sale_price=" + outputSalePrice +
                    ";predicted_output_price_source=" + (copyPrice ? "copy_price_from_transparent_input_sale_price" : "output_item_sale_price") +
                    ";predicted_output_total_value=" + totalValue +
                    ";machine_additional_consumed_total_value=" + additionalValue +
                    ";predicted_output_net_value=" + netValue +
                    ";predicted_output_rule_required_item_id=" + requiredItemId;
                if (additionalConsumed.HasValue && !string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
                {
                    suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                        ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
                }

                var minutesUntilReady = ReadInt(rule, "minutes_until_ready");
                if (minutesUntilReady <= 0 && ReadInt(rule, "days_until_ready") > 0)
                {
                    minutesUntilReady = ReadInt(rule, "days_until_ready") * 1600;
                }
                if (minutesUntilReady > 0)
                {
                    suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
                }

                return new MachineOutputPrediction(
                    "machine_data_exact_required_item_match",
                    additionalValue > 0
                        ? "predicted_output_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                        : "predicted_output_total_value_minus_transparent_input_sale_price",
                    suffix);
            }

            return MachineOutputPrediction.Unavailable("machine_data_no_exact_required_item_match");
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummary(JsonElement rule, IReadOnlyDictionary<string, int> inventoryStacks)
        {
            var count = ReadInt(rule, "additional_consumed_item_count");
            if (count <= 0)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            if (!rule.TryGetProperty("additional_consumed_items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var pricedCount = 0;
            var total = 0;
            var consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var amount = Math.Max(1, ReadInt(item, "amount"));
                var salePrice = ReadInt(item, "sale_price");
                if (salePrice <= 0)
                {
                    return null;
                }

                var qualifiedId = NormalizeObjectQualifiedId(ReadString(item, "qualified_item_id"), ReadString(item, "item_id"));
                if (string.IsNullOrWhiteSpace(qualifiedId))
                {
                    return null;
                }

                total += amount * salePrice;
                consumed[qualifiedId] = consumed.TryGetValue(qualifiedId, out var current) ? current + amount : amount;
                pricedCount++;
            }

            if (pricedCount != count)
            {
                return null;
            }

            var consumedItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value));
            var availableItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                {
                    inventoryStacks.TryGetValue(pair.Key, out var available);
                    return pair.Key + ":" + available;
                }));
            return new AdditionalConsumedSummary(total, consumedItems, availableItems);
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummaryForRequiredItem(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            string matchedRuleId,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }
            if (ReadInt(machineData, "additional_consumed_item_count") > 0)
            {
                return ReadAdditionalConsumedSummary(machineData, inventoryStacks);
            }
            if (
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                foreach (var rule in rules.EnumerateArray())
                {
                    if (rule.ValueKind == JsonValueKind.Object &&
                        string.Equals(ReadString(rule, "id"), matchedRuleId, StringComparison.Ordinal))
                    {
                        return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                    }
                }
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                }
            }

            return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
        }

        private static bool MachineRuleRequiredItemMatches(string requiredItemId, string normalizedQualifiedItemId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(requiredItemId))
            {
                return false;
            }

            return string.Equals(requiredItemId, normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requiredItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeObjectQualifiedId(requiredItemId, requiredItemId), normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeStatus(string value)
        {
            var chars = value
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
                .ToArray();
            var status = new string(chars).Trim('_');
            while (status.Contains("__", StringComparison.Ordinal))
            {
                status = status.Replace("__", "_", StringComparison.Ordinal);
            }

            return string.IsNullOrWhiteSpace(status) ? "unavailable" : status;
        }

        private static string NormalizeObjectQualifiedId(string qualifiedItemId, string itemId)
        {
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return qualifiedItemId;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return string.Empty;
            }

            return itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId;
        }

        private readonly struct MachineOutputPrediction
        {
            public MachineOutputPrediction(string status, string valueBasis, string expectedEffectSuffix)
            {
                Status = status;
                ValueBasis = valueBasis;
                ExpectedEffectSuffix = expectedEffectSuffix;
            }

            public string Status { get; }

            public string ValueBasis { get; }

            public string ExpectedEffectSuffix { get; }

            public static MachineOutputPrediction Unavailable(string status)
            {
                return new MachineOutputPrediction(status, "transparent_input_sale_price_output_unknown", string.Empty);
            }
        }

        private readonly struct AdditionalConsumedSummary
        {
            public AdditionalConsumedSummary(int totalValue, string consumedItems, string availableItems)
            {
                TotalValue = totalValue;
                ConsumedItems = consumedItems;
                AvailableItems = availableItems;
            }

            public int TotalValue { get; }

            public string ConsumedItems { get; }

            public string AvailableItems { get; }
        }

        private static (int X, int Y)? FirstDebrisChunkTile(JsonElement debris)
        {
            if (!debris.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Object)
                {
                    return (ReadInt(chunk, "tile_x"), ReadInt(chunk, "tile_y"));
                }
            }

            return null;
        }

        private static bool InventoryMayAcceptItem(SnapshotEnvelope snapshot, string qualifiedItemId, string itemId, int quality)
        {
            var normalizedQualifiedId = !string.IsNullOrWhiteSpace(qualifiedItemId)
                ? qualifiedItemId
                : string.IsNullOrWhiteSpace(itemId)
                    ? string.Empty
                    : itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
            if (string.IsNullOrWhiteSpace(normalizedQualifiedId))
            {
                return false;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), normalizedQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
