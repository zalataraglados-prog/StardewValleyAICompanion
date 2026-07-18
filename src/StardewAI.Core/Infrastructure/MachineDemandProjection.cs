using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
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
    int PlacedSameMachineCount,
    int IdleSameMachineCount,
    int ProcessCycleMinutes,
    int NextArrivalDays,
    int NextArrivalUnits,
    int CapacityBeforeNextArrival,
    int CapacityDeficitUnits,
    int RequiredAdditionalMachineCount,
    int LatestBuildLeadMinutes,
    int MinutesUntilNextArrival,
    bool BuildWindowOpen,
    bool CollectionPathRequired,
    string CollectionPathSource)
{
    public bool HasDemand => Priority > 0;
}

internal static class MachineDemandProjectionEvaluator
{
    private const int FullGameDayMinutes = 1600;
    private const int MinimumCraftAndPlacementLeadMinutes = 60;

    public static MachineDemandProjection Evaluate(SnapshotEnvelope snapshot, JsonElement recipe)
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
        var cycleMinutes = ReadConservativeCycleMinutes(predictedOutputs);
        var cropWave = ReadNextCropWave(snapshot, inputs);
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
        var requiredAdditional = deficit <= 0
            ? 0
            : capacityPerNewMachine > 0
                ? DivideRoundUp(deficit, capacityPerNewMachine)
                : deficit;
        var latestBuildLead = requiredAdditional > 0 && cycleMinutes > 0
            ? DivideRoundUp(deficit, requiredAdditional) * cycleMinutes
            : 0;
        var buildWindowOpen = deficit > 0 && (cropWave.Days < 0 ||
            windowMinutes <= latestBuildLead + MinimumCraftAndPlacementLeadMinutes);
        var horizonComplete = inputs.Length > 0 && backlogUnits > 0 && cycleMinutes > 0;
        var productionRequired = horizonComplete && deficit > 0 && buildWindowOpen;
        var collectionRequired = ReadInt(recipe, "times_crafted") == 0;

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
                    : deficit > 0 && horizonComplete && !buildWindowOpen
                        ? "deferred_until_latest_build_window"
                        : !horizonComplete && potentialInputCount > 0
                            ? "blocked_incomplete_capacity_horizon"
                            : "no_proven_current_requirement";
        var priority = taskSources.Length > 0 ? 300 : productionRequired ? 200 : collectionRequired ? 100 : 0;
        var horizonStatus = inputs.Length == 0
            ? potentialInputCount > 0
                ? "incomplete_legacy_input_count_without_lossless_rows"
                : "complete_no_current_probed_input_backlog"
            : cycleMinutes <= 0
                ? "incomplete_native_output_duration_unavailable"
                : cropWave.Days >= 0
                    ? "live_crop_wave_exact_if_required_growth_updates_continue"
                    : "bounded_one_day_workshop_service_horizon_no_live_crop_wave";
        var timingStatus = taskSources.Length > 0
            ? "open_priority_task"
            : collectionRequired && !productionRequired
                ? "open_first_craft_collection_path"
                : deficit <= 0
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
            fleet.Rows.Length,
            fleet.Rows.Count(row => row.BusyMinutes == 0),
            cycleMinutes,
            cropWave.Days,
            cropWave.Units,
            capacity,
            deficit,
            requiredAdditional,
            latestBuildLead,
            windowMinutes,
            buildWindowOpen,
            collectionRequired,
            collectionRequired ? "craft_master_uncompleted_learned_recipe" : "already_crafted_at_least_once");
    }

    private static PotentialInput[] ReadPotentialInputs(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("potential_loadable_inputs", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PotentialInput>();
        }

        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new PotentialInput(
                ReadString(row, "qualified_item_id"),
                ReadString(row, "item_id"),
                Math.Max(0, ReadInt(row, "stack")),
                ReadPredictedOutputs(row)))
            .ToArray();
    }

    private static PredictedOutput[] ReadPredictedOutputs(JsonElement input)
    {
        if (!input.TryGetProperty("accepting_contexts", out var contexts) || contexts.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictedOutput>();
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

    private static PredictedOutput[] ReadCapabilityOutputs(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("output_machine_data", out var machineData) || machineData.ValueKind != JsonValueKind.Object ||
            !machineData.TryGetProperty("output_rules", out var rules) || rules.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PredictedOutput>();
        }

        var outputs = new List<PredictedOutput>();
        foreach (var rule in rules.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object))
        {
            var minutes = Math.Max(0, ReadInt(rule, "minutes_until_ready"));
            if (rule.TryGetProperty("output_item", out var outputItem) && outputItem.ValueKind == JsonValueKind.Object)
            {
                outputs.Add(ReadCapabilityOutput(outputItem, minutes));
            }
            if (rule.TryGetProperty("output_items", out var outputItems) && outputItems.ValueKind == JsonValueKind.Array)
            {
                outputs.AddRange(outputItems.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object)
                    .Select(item => ReadCapabilityOutput(item, minutes)));
            }
        }
        return outputs.Where(output => !string.IsNullOrWhiteSpace(output.ItemId)).ToArray();
    }

    private static PredictedOutput ReadCapabilityOutput(JsonElement output, int processMinutes) => new(
        ReadString(output, "qualified_item_id"),
        ReadString(output, "item_id"),
        ReadString(output, "preserve_type"),
        ReadString(output, "preserve_id"),
        processMinutes);

    private static PredictedOutput ReadPredictedOutput(JsonElement output)
    {
        var item = output.TryGetProperty("item", out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
        return new PredictedOutput(
            item.ValueKind == JsonValueKind.Object ? ReadString(item, "qualified_item_id") : string.Empty,
            item.ValueKind == JsonValueKind.Object ? ReadString(item, "item_id") : string.Empty,
            ReadString(output, "preserve_type"),
            ReadString(output, "preserved_item_id"),
            Math.Max(0, ReadInt(output, "effective_minutes_until_ready",
                ReadInt(output, "override_minutes_until_ready", ReadInt(output, "rule_minutes_until_ready")))));
    }

    private static int ReadConservativeCycleMinutes(IReadOnlyCollection<PredictedOutput> outputs)
    {
        var durations = outputs.Select(output => output.ProcessMinutes).Where(minutes => minutes > 0).Distinct().ToArray();
        return durations.Length == 0 ? 0 : durations.Max();
    }

    private static CropWave ReadNextCropWave(SnapshotEnvelope snapshot, IReadOnlyCollection<PotentialInput> inputs)
    {
        var acceptedIds = inputs
            .SelectMany(input => new[] { input.QualifiedItemId, input.ItemId, Unqualify(input.QualifiedItemId) })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var crops = ReadStateFieldValue(snapshot, "farm", "crops");
        if (acceptedIds.Count == 0 || !crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array)
        {
            return new CropWave(-1, 0);
        }

        var matching = crops.Value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object && ReadBool(row, "dead") != true &&
                (acceptedIds.Contains(ReadString(row, "harvest_item_id")) ||
                 acceptedIds.Contains(ReadString(row, "harvest_item_qualified_id"))))
            .Select(row => new
            {
                Days = ReadInt(row, "days_until_next_harvest_if_watered", -1),
                Units = Math.Max(1, ReadInt(row, "harvest_min_stack", 1))
            })
            .Where(row => row.Days >= 0)
            .ToArray();
        if (matching.Length == 0)
        {
            return new CropWave(-1, 0);
        }
        var nextDays = matching.Min(row => row.Days);
        return new CropWave(nextDays, matching.Where(row => row.Days == nextDays).Sum(row => row.Units));
    }

    private static string[] ReadPriorityTaskSources(
        SnapshotEnvelope snapshot,
        string qualifiedId,
        string itemId,
        IReadOnlyCollection<string> contextTags,
        IReadOnlyCollection<PredictedOutput> outputs)
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

    private sealed record PotentialInput(string QualifiedItemId, string ItemId, int Stack, PredictedOutput[] Outputs);
    private sealed record PredictedOutput(string QualifiedItemId, string ItemId, string PreserveType, string PreservedItemId, int ProcessMinutes);
    private sealed record FleetRow(int BusyMinutes);
    private sealed record FleetProjection(FleetRow[] Rows);
    private sealed record CropWave(int Days, int Units);
}
