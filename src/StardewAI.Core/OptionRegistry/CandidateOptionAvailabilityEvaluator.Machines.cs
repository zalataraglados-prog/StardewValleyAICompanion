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
            return MachineServiceCandidates(snapshot, commitmentLedger)
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

        private EventCandidate[] MachineOutputCollectionCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            return MachineServiceCandidates(snapshot, commitmentLedger)
                .Where(candidate => string.Equals(
                    candidate.Kind,
                    "collect_machine_output_tile",
                    StringComparison.Ordinal))
                .ToArray();
        }

        private EventCandidate[] TaskMachineDemandCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var activeReservations = commitmentLedger?.MaterialReservations
                .Any(reservation => string.Equals(
                    reservation.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal)) == true;
            var candidates = QuestCandidates(snapshot, commitmentLedger)
                .Where(candidate => candidate.Kind is
                    "collect_machine_output_tile" or
                    "load_machine_input_tile")
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "quest_acquisition_target_step"),
                    "true",
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "quest_acquisition_source_step"),
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (!activeReservations)
            {
                return candidates;
            }

            foreach (var candidate in candidates.Where(candidate =>
                         candidate.Kind == "load_machine_input_tile"))
            {
                var exactTaskReservationProjection = string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "machine_support_demand_class"),
                        "priority_task_requirement",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "material_reservation_guard_status"),
                        "ready",
                        StringComparison.Ordinal);
                if (exactTaskReservationProjection)
                {
                    continue;
                }
                candidate.Available = false;
                candidate.BlockReasons = candidate.BlockReasons
                    .Concat(new[]
                    {
                        "task_machine_input_active_material_reservations_require_projection"
                    })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
            return candidates;
        }

        private EventCandidate[] SupportedMachineInputCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            return MachineServiceCandidates(snapshot, commitmentLedger)
                .Where(candidate => string.Equals(
                    candidate.Kind,
                    "load_machine_input_tile",
                    StringComparison.Ordinal))
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_support_continuation_status"),
                    "active",
                    StringComparison.Ordinal))
                .Where(candidate => !string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_support_demand_class"),
                    "priority_task_requirement",
                    StringComparison.Ordinal))
                .ToArray();
        }

        private EventCandidate[] SupportedMachineCapacityLifecycleCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var activeIntents = commitmentLedger?.MachineSupportIntents
                .Where(intent => string.Equals(
                    intent.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal))
                .OrderBy(intent => intent.IntentId, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<MachineSupportIntent>();
            if (activeIntents.Length == 0)
            {
                var taskCandidates =
                    TaskMachineCapacityStartCandidates(
                        snapshot,
                        commitmentLedger);
                if (taskCandidates.Length > 0)
                {
                    return taskCandidates;
                }

                return MachineCraftingCandidates(snapshot, commitmentLedger)
                    .Select(candidate => new
                    {
                        Candidate = candidate,
                        Support = ExplicitGoalSupportProjection.Read(
                            candidate.Kind,
                            candidate.ExpectedEffect,
                            "goal.economy.earn_money")
                    })
                    .Where(row => string.Equals(
                        row.Support.Status,
                        "supported_bounded_positive_net_benefit",
                        StringComparison.Ordinal))
                    .OrderByDescending(row => row.Candidate.Available)
                    .ThenByDescending(row => row.Support.NetBenefit)
                    .ThenBy(row => row.Candidate.CandidateId, StringComparer.Ordinal)
                    .Take(1)
                    .Select(row => row.Candidate)
                    .ToArray();
            }

            var intent = activeIntents[0];
            if (!MachineSupportIntentProjection.IsValid(intent))
            {
                return Array.Empty<EventCandidate>();
            }
            if (!MachineSupportIntentProjection.TaskDemandMatchesSnapshot(
                    snapshot,
                    commitmentLedger,
                    intent))
            {
                return Array.Empty<EventCandidate>();
            }

            EventCandidate[] ForIntent(IEnumerable<EventCandidate> candidates) =>
                candidates
                    .Where(candidate => string.Equals(
                        ReadParameter(
                            candidate.Parameters,
                            "machine_support_intent_id"),
                        intent.IntentId,
                        StringComparison.Ordinal))
                    .ToArray();

            if (string.Equals(
                    intent.Stage,
                    MachineSupportIntentStages.PlacementBound,
                    StringComparison.Ordinal))
            {
                var load = ForIntent(
                    SupportedMachineInputCandidates(
                        snapshot,
                        commitmentLedger));
                if (load.Length > 0)
                {
                    return load;
                }
            }

            if (string.Equals(
                    intent.Stage,
                    MachineSupportIntentStages.CraftSelected,
                    StringComparison.Ordinal) ||
                string.Equals(
                    intent.Stage,
                    MachineSupportIntentStages.PlacementBound,
                    StringComparison.Ordinal))
            {
                return ForIntent(
                    MachinePlacementCandidates(
                        snapshot,
                        commitmentLedger));
            }

            return Array.Empty<EventCandidate>();
        }

        private EventCandidate[] TaskMachineCapacityStartCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var taskRows = MachineCraftingCandidates(
                    snapshot,
                    commitmentLedger)
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_demand_class"),
                    "priority_task_requirement",
                    StringComparison.Ordinal))
                .Where(candidate =>
                    ExplicitGoalSupportProjection
                        .HasExactCollectionTaskSources(
                            candidate.ExpectedEffect))
                .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
            if (taskRows.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var placementRows = taskRows
                .Where(candidate =>
                    ReadMachineIntParameter(
                        candidate.Parameters,
                        "placed_same_machine_count") == 0 &&
                    ReadMachineIntParameter(
                        candidate.Parameters,
                        "inventory_same_machine_count") > 0)
                .SelectMany(task => MachinePlacementCandidates(
                        snapshot,
                        commitmentLedger)
                    .Where(placement => string.Equals(
                        placement.Kind,
                        "place_machine_item",
                        StringComparison.Ordinal))
                    .Where(placement => string.Equals(
                        placement.QualifiedItemId,
                        task.QualifiedItemId,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(placement => AttachTaskCapacityDemand(
                        placement,
                        task)))
                .OrderByDescending(candidate => candidate.Available)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
            if (placementRows.Length > 0)
            {
                return placementRows.Take(1).ToArray();
            }

            return taskRows
                .Where(candidate =>
                    ReadMachineIntParameter(
                        candidate.Parameters,
                        "required_additional_machine_count") > 0)
                .OrderByDescending(candidate => candidate.Available)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Take(1)
                .ToArray();
        }

        private static EventCandidate AttachTaskCapacityDemand(
            EventCandidate placement,
            EventCandidate task)
        {
            var sources = ReadParameter(
                task.Parameters,
                "priority_task_sources_json");
            var placedCount = ReadMachineIntParameter(
                task.Parameters,
                "placed_same_machine_count");
            var inventoryCount = ReadMachineIntParameter(
                task.Parameters,
                "inventory_same_machine_count");
            placement.ExpectedEffect +=
                ";machine_demand_class=priority_task_requirement" +
                ";priority_task_required=true" +
                ";priority_task_sources_json=" + sources +
                ";placed_same_machine_count=" + placedCount +
                ";inventory_same_machine_count=" + inventoryCount +
                ";required_additional_machine_count=1" +
                ";material_reservation_request_priority=300" +
                ";material_reservation_request_class=active_collection_task" +
                ";machine_task_capacity_action_required=true";
            placement.Parameters = SetParameters(
                placement.Parameters,
                Parameter(
                    "machine_demand_class",
                    "priority_task_requirement"),
                Parameter("priority_task_required", "true"),
                Parameter("priority_task_sources_json", sources),
                Parameter(
                    "placed_same_machine_count",
                    placedCount.ToString()),
                Parameter(
                    "inventory_same_machine_count",
                    inventoryCount.ToString()),
                Parameter("required_additional_machine_count", "1"),
                Parameter(
                    "material_reservation_request_priority",
                    "300"),
                Parameter(
                    "material_reservation_request_class",
                    "active_collection_task"),
                Parameter(
                    "machine_task_capacity_action_required",
                    "true"));
            return placement;
        }

        private static SmallModelActionParameter[] SetParameters(
            SmallModelActionParameter[] source,
            params SmallModelActionParameter[] updates)
        {
            var names = updates
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.Ordinal);
            return source
                .Where(parameter => !names.Contains(parameter.Name))
                .Concat(updates)
                .ToArray();
        }

        private static int ReadMachineIntParameter(
            SmallModelActionParameter[] parameters,
            string name) =>
            int.TryParse(
                ReadParameter(parameters, name),
                out var value)
                ? value
                : 0;

        private EventCandidate[] MachineServiceCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
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
                    var outputContextTags = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadStringArray(heldItem, "context_tags")
                        : Array.Empty<string>();
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
                        Parameter("machine_location_id", machineLocation),
                        Parameter("output_context_tags_json", JsonSerializer.Serialize(outputContextTags))
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
                    candidates.AddRange(MachineLoadInputCandidates(
                        snapshot,
                        machine,
                        machineLocation,
                        x,
                        y,
                        playerX,
                        playerY,
                        standTile,
                        commitmentLedger));
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

    }
}
