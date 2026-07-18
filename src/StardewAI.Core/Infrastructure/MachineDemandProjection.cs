using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineDemandProjection(
    string DemandClass,
    int Priority,
    bool PriorityTaskRequired,
    string[] PriorityTaskSources,
    bool ProductionCapacityRequired,
    int PotentialInputCount,
    int PlacedSameMachineCount,
    int IdleSameMachineCount,
    bool CollectionPathRequired,
    string CollectionPathSource)
{
    public bool HasDemand => Priority > 0;
}

internal static class MachineDemandProjectionEvaluator
{
    public static MachineDemandProjection Evaluate(SnapshotEnvelope snapshot, JsonElement recipe)
    {
        var qualifiedId = ReadString(recipe, "output_qualified_item_id");
        var itemId = ReadString(recipe, "output_item_id");
        var tags = ReadStringArray(recipe, "output_context_tags");
        var taskSources = ReadPriorityTaskSources(snapshot, qualifiedId, itemId, tags);
        var potentialInputs = Math.Max(0, ReadInt(recipe, "potential_loadable_input_count"));
        var fleet = ReadFleetCapacity(snapshot, qualifiedId);
        var productionRequired = potentialInputs > fleet.IdleCount;
        var collectionRequired = ReadInt(recipe, "times_crafted") == 0;

        var demandClass = taskSources.Length > 0
            ? "priority_task_requirement"
            : productionRequired
                ? "production_capacity_requirement"
                : collectionRequired
                    ? "collection_path_requirement"
                    : "no_proven_current_requirement";
        var priority = taskSources.Length > 0 ? 300 : productionRequired ? 200 : collectionRequired ? 100 : 0;
        return new MachineDemandProjection(
            demandClass,
            priority,
            taskSources.Length > 0,
            taskSources,
            productionRequired,
            potentialInputs,
            fleet.TotalCount,
            fleet.IdleCount,
            collectionRequired,
            collectionRequired ? "craft_master_uncompleted_learned_recipe" : "already_crafted_at_least_once");
    }

    private static string[] ReadPriorityTaskSources(
        SnapshotEnvelope snapshot,
        string qualifiedId,
        string itemId,
        IReadOnlyCollection<string> contextTags)
    {
        var sources = new List<string>();
        var quests = ReadStateFieldValue(snapshot, "quests", "active_quests");
        if (quests.HasValue && quests.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var quest in quests.Value.EnumerateArray())
            {
                if (quest.ValueKind != JsonValueKind.Object || ReadBool(quest, "completed") == true ||
                    !quest.TryGetProperty("per_type_fields", out var fields) || fields.ValueKind != JsonValueKind.Object ||
                    !SameItem(ReadString(fields, "item_id"), qualifiedId, itemId))
                {
                    continue;
                }
                sources.Add("ordinary_quest:" + ReadString(quest, "id", ReadString(quest, "runtime_type", "unknown")));
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
        return sources.Distinct(StringComparer.Ordinal).OrderBy(source => source, StringComparer.Ordinal).ToArray();
    }

    private static (int TotalCount, int IdleCount) ReadFleetCapacity(SnapshotEnvelope snapshot, string qualifiedId)
    {
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
        {
            return (0, 0);
        }
        var rows = machines.Value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "qualified_item_id"), qualifiedId, StringComparison.Ordinal))
            .ToArray();
        return (
            rows.Length,
            rows.Count(row => ReadInt(row, "minutes_until_ready") <= 0 && ReadBool(row, "ready_for_harvest") != true));
    }

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
            string.Equals(Unqualify(requiredId), itemId, StringComparison.OrdinalIgnoreCase);
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
}
