using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineDemandProjection(
    string DemandClass,
    string MachineScale,
    string HorizonStatus,
    string TimingStatus,
    int Priority,
    bool PriorityTaskRequired,
    string[] PriorityTaskSources,
    bool ProductionCapacityRequired,
    int PotentialInputCount,
    int BacklogInputUnits,
    string EconomicValueStatus,
    int BacklogProcessingNetValue,
    int CapacityDeficitProcessingNetValue,
    int PlacedSameMachineCount,
    int InventorySameMachineCount,
    int IdleSameMachineCount,
    int ProcessCycleMinutes,
    int NextArrivalDays,
    int NextArrivalUnits,
    int NextArrivalServiceIntervalDays,
    int CapacityBeforeNextArrival,
    int CapacityDeficitUnits,
    int CapacityBetweenArrivalWaves,
    int ArrivalWaveCapacityDeficitUnits,
    int RequiredAdditionalMachineCount,
    int LatestBuildLeadMinutes,
    int MinutesUntilNextArrival,
    bool BuildWindowOpen,
    string NextArrivalSource,
    string CommitmentLedgerId,
    int CommitmentLedgerRevision,
    string[] CommitmentIds,
    bool CollectionPathRequired,
    string CollectionPathSource)
{
    public bool HasDemand => Priority > 0;
}

internal static class MachineDemandProjectionEvaluator
{
    private const int FullGameDayMinutes = 1600;
    private const int MinimumCraftAndPlacementLeadMinutes = 60;

    public static MachineDemandProjection Evaluate(
        SnapshotEnvelope snapshot,
        JsonElement recipe,
        StrategyCommitmentLedger? commitmentLedger = null)
    {
        var qualifiedId = ReadString(recipe, "output_qualified_item_id");
        var itemId = ReadString(recipe, "output_item_id");
        var tags = ReadStringArray(recipe, "output_context_tags");
        var inputs = ReadPotentialInputs(recipe);
        var predictedOutputs = inputs.SelectMany(input => input.Outputs).Distinct().ToArray();
        var outputs = predictedOutputs
            .Concat(ReadCapabilityOutputs(recipe))
            .Distinct()
            .ToArray();
        var taskSources = ReadPriorityTaskSources(snapshot, qualifiedId, itemId, tags, outputs);
        var potentialInputCount = Math.Max(0, ReadInt(recipe, "potential_loadable_input_count"));
        var backlogUnits = inputs.Sum(input => Math.Max(0, input.Stack));
        var fleet = ReadFleetCapacity(snapshot, qualifiedId);
        var cropWave = MachineCropWaveProjectionEvaluator.Evaluate(
            snapshot,
            recipe,
            inputs.SelectMany(input => new[]
            {
                input.QualifiedItemId,
                input.ItemId,
                Unqualify(input.QualifiedItemId)
            }).ToArray(),
            commitmentLedger);
        var cycleMinutes = Math.Max(ReadConservativeCycleMinutes(predictedOutputs), cropWave.ProcessMinutes);
        var windowMinutes = cropWave.Days >= 0
            ? MinutesUntilFutureMorning(snapshot, cropWave.Days)
            : backlogUnits > 0
                ? FullGameDayMinutes
                : 0;
        var capacity = cycleMinutes > 0 && windowMinutes > 0
            ? fleet.Rows.Sum(row => Math.Max(0, windowMinutes - row.BusyMinutes) / cycleMinutes)
            : 0;
        var deficit = Math.Max(0, backlogUnits - capacity);
        var capacityPerNewMachine = cycleMinutes > 0 && windowMinutes > 0
            ? windowMinutes / cycleMinutes
            : 0;
        var backlogAdditional = deficit <= 0
            ? 0
            : capacityPerNewMachine > 0
                ? DivideRoundUp(deficit, capacityPerNewMachine)
                : deficit;
        var serviceWindowMinutes = cropWave.ServiceIntervalDays > 0
            ? cropWave.ServiceIntervalDays * FullGameDayMinutes
            : 0;
        var capacityBetweenWaves = cycleMinutes > 0 && serviceWindowMinutes > 0
            ? fleet.Rows.Sum(row =>
                Math.Max(0, serviceWindowMinutes - Math.Max(0, row.BusyMinutes - windowMinutes)) / cycleMinutes)
            : 0;
        var arrivalWaveDeficit = serviceWindowMinutes > 0
            ? Math.Max(0, cropWave.Units - capacityBetweenWaves)
            : 0;
        var capacityPerNewMachineBetweenWaves = cycleMinutes > 0 && serviceWindowMinutes > 0
            ? serviceWindowMinutes / cycleMinutes
            : 0;
        var arrivalAdditional = arrivalWaveDeficit <= 0
            ? 0
            : capacityPerNewMachineBetweenWaves > 0
                ? DivideRoundUp(arrivalWaveDeficit, capacityPerNewMachineBetweenWaves)
                : arrivalWaveDeficit;
        var inventorySameMachineCount =
            ReadInventorySameMachineCount(
                snapshot,
                qualifiedId);
        var requiredAdditional = Math.Max(
            0,
            Math.Max(
                backlogAdditional,
                arrivalAdditional) -
            inventorySameMachineCount);
        var latestBuildLead = backlogAdditional > 0 && cycleMinutes > 0
            ? DivideRoundUp(deficit, backlogAdditional) * cycleMinutes
            : 0;
        var buildWindowOpen = requiredAdditional > 0 && (cropWave.Days < 0 ||
            windowMinutes <= latestBuildLead + MinimumCraftAndPlacementLeadMinutes);
        var horizonComplete = cycleMinutes > 0 &&
            ((inputs.Length > 0 && backlogUnits > 0) || (cropWave.Days >= 0 && cropWave.ServiceIntervalDays > 0));
        var productionRequired = horizonComplete && requiredAdditional > 0 && buildWindowOpen;
        var collectionRequired = ReadInt(recipe, "times_crafted") == 0;
        var machineData =
            recipe.TryGetProperty(
                "output_machine_data",
                out var outputMachineData) &&
            outputMachineData.ValueKind == JsonValueKind.Object
                ? outputMachineData
                : default;
        var economicValue =
            MachineDemandEconomicValueProjection.Evaluate(
                inputs,
                deficit,
                ReadInt(
                    machineData,
                    "additional_consumed_item_count",
                    -1) == 0);

        var machineScale = taskSources.Length > 0
            ? "collection_scale_one_off"
            : cropWave.Days >= 0
                ? "factory_scale_batch"
                : backlogUnits > 0
                    ? "workshop_scale_recurring_or_bounded"
                    : collectionRequired
                        ? "collection_scale_one_off"
                        : "no_current_scale";
        var demandClass = taskSources.Length > 0
            ? "priority_task_requirement"
            : productionRequired
                ? "production_capacity_requirement"
                : collectionRequired
                    ? "collection_path_requirement"
                    : requiredAdditional > 0 && horizonComplete && !buildWindowOpen
                        ? "deferred_until_latest_build_window"
                        : !horizonComplete && potentialInputCount > 0
                            ? "blocked_incomplete_capacity_horizon"
                            : "no_proven_current_requirement";
        var priority = taskSources.Length > 0 ? 300 : productionRequired ? 200 : collectionRequired ? 100 : 0;
        var horizonStatus = cropWave.Days >= 0 && cropWave.ProcessMinutes > 0
            ? cropWave.Source == "committed_strategy_ledger"
                ? "committed_crop_wave_static_native_machine_trigger_and_conservative_base_growth"
                : cropWave.Source == "live_and_committed_crop_wave"
                    ? "live_and_committed_crop_wave_static_native_machine_trigger_earliest_boundary"
                    : "live_crop_wave_static_native_machine_trigger_or_current_probe"
            : inputs.Length == 0
            ? potentialInputCount > 0
                ? "incomplete_legacy_input_count_without_lossless_rows"
                : "complete_no_current_probed_input_backlog"
            : cycleMinutes <= 0
                ? "incomplete_native_output_duration_unavailable"
                : "bounded_one_day_workshop_service_horizon_no_live_crop_wave";
        var timingStatus = taskSources.Length > 0
            ? "open_priority_task"
            : collectionRequired && !productionRequired
                ? "open_first_craft_collection_path"
                : requiredAdditional <= 0
                    ? "deferred_existing_fleet_clears_backlog_within_horizon"
                    : !horizonComplete
                        ? "blocked_projection_incomplete"
                        : buildWindowOpen
                            ? "open_latest_build_window_reached"
                            : "deferred_too_early_machine_would_idle";

        return new MachineDemandProjection(
            demandClass,
            machineScale,
            horizonStatus,
            timingStatus,
            priority,
            taskSources.Length > 0,
            taskSources,
            productionRequired,
            potentialInputCount,
            backlogUnits,
            economicValue.Status,
            economicValue.BacklogNetValue,
            economicValue.CapacityDeficitNetValue,
            fleet.Rows.Length,
            inventorySameMachineCount,
            fleet.Rows.Count(row => row.BusyMinutes == 0),
            cycleMinutes,
            cropWave.Days,
            cropWave.Units,
            cropWave.ServiceIntervalDays,
            capacity,
            deficit,
            capacityBetweenWaves,
            arrivalWaveDeficit,
            requiredAdditional,
            latestBuildLead,
            windowMinutes,
            buildWindowOpen,
            cropWave.Source,
            commitmentLedger?.LedgerId ?? string.Empty,
            commitmentLedger?.Revision ?? 0,
            cropWave.CommitmentIds,
            collectionRequired,
            collectionRequired ? "craft_master_uncompleted_learned_recipe" : "already_crafted_at_least_once");
    }

    private static PotentialMachineDemandInput[] ReadPotentialInputs(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("potential_loadable_inputs", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PotentialMachineDemandInput>();
        }

        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new PotentialMachineDemandInput(
                ReadString(row, "qualified_item_id"),
                ReadString(row, "item_id"),
                Math.Max(0, ReadInt(row, "stack")),
                TryReadNonnegativeInt(
                    row,
                    "unit_sale_price",
                    out var inputSalePrice),
                inputSalePrice,
                ReadPredictedOutputs(row)))
            .ToArray();
    }

    private static int ReadInventorySameMachineCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "machine_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            !placement.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        long total = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
            {
                total += Math.Max(0, ReadInt(row, "stack"));
                if (total >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }
        }
        return (int)total;
    }

    private static PredictedMachineDemandOutput[] ReadPredictedOutputs(JsonElement input)
    {
        if (!input.TryGetProperty("accepting_contexts", out var contexts) || contexts.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictedMachineDemandOutput>();
        }

        return contexts.EnumerateArray()
            .Where(context => context.ValueKind == JsonValueKind.Object &&
                context.TryGetProperty("predicted_output", out var output) &&
                output.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(output, "status"), "available", StringComparison.Ordinal))
            .Select(context => ReadPredictedOutput(context.GetProperty("predicted_output")))
            .Where(output => output.ProcessMinutes > 0 &&
                (!string.IsNullOrWhiteSpace(output.QualifiedItemId) || !string.IsNullOrWhiteSpace(output.ItemId)))
            .Distinct()
            .ToArray();
    }

    private static PredictedMachineDemandOutput[] ReadCapabilityOutputs(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("output_machine_data", out var machineData) || machineData.ValueKind != JsonValueKind.Object ||
            !machineData.TryGetProperty("output_rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictedMachineDemandOutput>();
        }

        var outputs = new List<PredictedMachineDemandOutput>();
        foreach (var rule in rules.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
        {
            var minutes = ReadRuleProcessMinutes(rule);
            if (rule.TryGetProperty("output_item", out var outputItem) && outputItem.ValueKind == JsonValueKind.Object &&
                IsStaticCapabilityOutput(outputItem))
            {
                outputs.Add(ReadCapabilityOutput(outputItem, minutes));
            }
            if (rule.TryGetProperty("output_items", out var outputItems) && outputItems.ValueKind == JsonValueKind.Array)
            {
                outputs.AddRange(outputItems.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && IsStaticCapabilityOutput(item))
                    .Select(item => ReadCapabilityOutput(item, minutes)));
            }
        }
        return outputs.Where(output => !string.IsNullOrWhiteSpace(output.ItemId)).ToArray();
    }

    private static int ReadRuleProcessMinutes(JsonElement rule)
    {
        var minutes = ReadInt(rule, "minutes_until_ready", -1);
        if (minutes >= 0)
        {
            return minutes;
        }
        var days = ReadInt(rule, "days_until_ready", -1);
        return days > 0 ? checked(days * FullGameDayMinutes) : 0;
    }

    private static bool IsStaticCapabilityOutput(JsonElement output) =>
        string.IsNullOrWhiteSpace(ReadString(output, "condition")) &&
        string.IsNullOrWhiteSpace(ReadString(output, "per_item_condition")) &&
        string.IsNullOrWhiteSpace(ReadString(output, "output_method"));

    private static PredictedMachineDemandOutput ReadCapabilityOutput(JsonElement output, int processMinutes) => new(
        ReadString(output, "qualified_item_id"),
        ReadString(output, "item_id"),
        ReadString(output, "preserve_type"),
        ReadString(output, "preserve_id"),
        processMinutes,
        false,
        0,
        0,
        0);

    private static PredictedMachineDemandOutput ReadPredictedOutput(JsonElement output)
    {
        var item = output.TryGetProperty("item", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        var salePriceKnown = TryReadNonnegativeInt(
            output,
            "sale_price",
            out var salePrice);
        var stackKnown = TryReadPositiveInt(
            output,
            "stack",
            out var stack);
        var requiredCountKnown = TryReadPositiveInt(
            output,
            "required_count",
            out var requiredCount);
        return new PredictedMachineDemandOutput(
            item.ValueKind == JsonValueKind.Object ? ReadString(item, "qualified_item_id") : string.Empty,
            item.ValueKind == JsonValueKind.Object ? ReadString(item, "item_id") : string.Empty,
            ReadString(output, "preserve_type"),
            ReadString(output, "preserved_item_id"),
            Math.Max(0, ReadInt(output, "effective_minutes_until_ready",
                ReadInt(output, "override_minutes_until_ready", ReadInt(output, "rule_minutes_until_ready")))),
            salePriceKnown,
            salePrice,
            stackKnown ? stack : 0,
            requiredCountKnown ? requiredCount : 0);
    }

    private static bool TryReadNonnegativeInt(
        JsonElement row,
        string property,
        out int value)
    {
        value = 0;
        return row.TryGetProperty(property, out var element) &&
            element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out value) &&
            value >= 0;
    }

    private static bool TryReadPositiveInt(
        JsonElement row,
        string property,
        out int value)
    {
        return TryReadNonnegativeInt(
                   row,
                   property,
                   out value) &&
               value > 0;
    }

    private static int ReadConservativeCycleMinutes(IReadOnlyCollection<PredictedMachineDemandOutput> outputs)
    {
        var durations = outputs.Select(output => output.ProcessMinutes).Where(minutes => minutes > 0).Distinct().ToArray();
        return durations.Length == 0 ? 0 : durations.Max();
    }

    private static string[] ReadPriorityTaskSources(
        SnapshotEnvelope snapshot,
        string qualifiedId,
        string itemId,
        IReadOnlyCollection<string> contextTags,
        IReadOnlyCollection<PredictedMachineDemandOutput> outputs)
    {
        var sources = new List<string>();
        var quests = ReadStateFieldValue(snapshot, "quests", "active_quests");
        if (quests.HasValue && quests.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var quest in quests.Value.EnumerateArray())
            {
                if (quest.ValueKind != JsonValueKind.Object || ReadBool(quest, "completed") == true ||
                    !quest.TryGetProperty("per_type_fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var required = ReadString(fields, "item_id");
                if (SameItem(required, qualifiedId, itemId) || outputs.Any(output => SameItem(required, output.QualifiedItemId, output.ItemId)))
                {
                    sources.Add("ordinary_quest:" + ReadString(quest, "id", ReadString(quest, "runtime_type", "unknown")));
                }
            }
        }

        var orders = ReadStateFieldValue(snapshot, "quests", "special_orders");
        if (orders.HasValue && orders.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var order in orders.Value.EnumerateArray())
            {
                if (order.ValueKind != JsonValueKind.Object ||
                    !order.TryGetProperty("objectives", out var objectives) || objectives.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                var index = 0;
                foreach (var objective in objectives.EnumerateArray())
                {
                    if (objective.ValueKind == JsonValueKind.Object && ReadBool(objective, "complete") != true &&
                        objective.TryGetProperty("per_type_fields", out var fields) && fields.ValueKind == JsonValueKind.Object &&
                        fields.TryGetProperty("acceptable_context_tag_sets", out var sets) && sets.ValueKind == JsonValueKind.Array &&
                        sets.EnumerateArray().Any(set => set.ValueKind == JsonValueKind.String && TagSetMatches(set.GetString(), contextTags)))
                    {
                        sources.Add("special_order:" + ReadString(order, "quest_key", "unknown") + ":objective:" + index);
                    }
                    index++;
                }
            }
        }

        var raccoon = ReadStateFieldValue(snapshot, "world_progress", "raccoon_request");
        if (raccoon.HasValue && raccoon.Value.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(raccoon.Value, "projection_status"), "exact_native_Raccoon.GetBundle", StringComparison.Ordinal) &&
            ReadBool(raccoon.Value, "request_available") == true &&
            raccoon.Value.TryGetProperty("ingredients", out var ingredients) && ingredients.ValueKind == JsonValueKind.Array)
        {
            foreach (var ingredient in ingredients.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object && ReadBool(row, "completed") != true))
            {
                var requiredId = ReadString(ingredient, "item_id");
                var preservesId = ReadString(ingredient, "preserves_item_id");
                if (outputs.Any(output => SameItem(requiredId, output.QualifiedItemId, output.ItemId) &&
                    (string.IsNullOrWhiteSpace(preservesId) ||
                     string.Equals(output.PreservedItemId, "DROP_IN", StringComparison.OrdinalIgnoreCase) ||
                     SameItem(preservesId, output.PreservedItemId, output.PreservedItemId))))
                {
                    sources.Add("raccoon_bundle:ingredient:" + ReadInt(ingredient, "ingredient_index"));
                }
            }
        }
        return sources.Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal).ToArray();
    }

    private static FleetProjection ReadFleetCapacity(SnapshotEnvelope snapshot, string qualifiedId)
    {
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
        {
            return new FleetProjection(Array.Empty<FleetRow>());
        }
        return new FleetProjection(machines.Value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "qualified_item_id"), qualifiedId, StringComparison.Ordinal))
            .Select(row => new FleetRow(ReadBool(row, "ready_for_harvest") == true ? 0 : Math.Max(0, ReadInt(row, "minutes_until_ready"))))
            .ToArray());
    }

    private static int MinutesUntilFutureMorning(SnapshotEnvelope snapshot, int days)
    {
        if (days <= 0)
        {
            return 0;
        }
        var time = ReadStateFieldInt(snapshot, "time", "time", 600);
        var hour = Math.Clamp(time / 100, 6, 26);
        var minute = Math.Clamp(time % 100, 0, 59);
        var untilTwoAm = Math.Max(0, 26 * 60 - (hour * 60 + minute));
        return untilTwoAm + 400 + (days - 1) * FullGameDayMinutes;
    }

    private static int DivideRoundUp(int value, int divisor) => divisor <= 0 ? 0 : (value + divisor - 1) / divisor;

    private static bool TagSetMatches(string? query, IReadOnlyCollection<string> contextTags)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }
        return query.Split(',').All(requiredGroup =>
            requiredGroup.Split('/').Any(requiredTag =>
                contextTags.Contains(requiredTag.Trim(), StringComparer.OrdinalIgnoreCase)));
    }

    private static bool SameItem(string requiredId, string qualifiedId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(requiredId))
        {
            return false;
        }
        return string.Equals(requiredId, qualifiedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requiredId, itemId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Unqualify(requiredId), Unqualify(itemId), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Unqualify(requiredId), Unqualify(qualifiedId), StringComparison.OrdinalIgnoreCase);
    }

    private static string Unqualify(string itemId)
    {
        var close = itemId.IndexOf(')');
        return itemId.StartsWith("(", StringComparison.Ordinal) && close >= 0 && close + 1 < itemId.Length
            ? itemId.Substring(close + 1)
            : itemId;
    }

    private static string[] ReadStringArray(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

    private sealed record FleetRow(int BusyMinutes);
    private sealed record FleetProjection(FleetRow[] Rows);
}
