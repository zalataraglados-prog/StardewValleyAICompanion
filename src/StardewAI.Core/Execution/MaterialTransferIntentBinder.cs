using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

internal static class MaterialTransferIntentBinder
{
    private static readonly string[] RequiredNames =
    {
        "source_node_id",
        "destination_node_id",
        "source_slot_index",
        "qualified_item_id",
        "quality",
        "quantity",
        "expected_source_stack"
    };

    public static IReadOnlyList<string> RequiredParameterNames => RequiredNames;

    public static bool TryReadGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "material_inventory_graph")))
        {
            return false;
        }

        var value = ReadStateFieldValue(snapshot, "farm", "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(value.Value.GetRawText());
            return graph is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryBuildIntent(
        IEnumerable<SmallModelActionParameter> parameters,
        out MaterialTransferIntent? intent)
    {
        intent = null;
        var parameterRows = parameters.ToArray();
        if (RequiredNames.Any(name =>
                parameterRows.Count(parameter => parameter.Name == name) != 1))
        {
            return false;
        }

        var values = parameterRows
            .GroupBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        var sourceNodeId = Read(values, "source_node_id");
        var destinationNodeId = Read(values, "destination_node_id");
        var sourceSlotIndex = ReadInt(values, "source_slot_index");
        var qualifiedItemId = Read(values, "qualified_item_id");
        var quality = ReadInt(values, "quality");
        var quantity = ReadInt(values, "quantity");
        var expectedSourceStack = ReadInt(values, "expected_source_stack");
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId) ||
            !sourceSlotIndex.HasValue ||
            string.IsNullOrWhiteSpace(qualifiedItemId) ||
            !quality.HasValue ||
            !quantity.HasValue ||
            !expectedSourceStack.HasValue)
        {
            return false;
        }

        intent = new MaterialTransferIntent
        {
            SourceNodeId = sourceNodeId,
            DestinationNodeId = destinationNodeId,
            SourceSlotIndex = sourceSlotIndex.Value,
            QualifiedItemId = qualifiedItemId,
            Quality = quality.Value,
            Quantity = quantity.Value,
            ExpectedSourceStack = expectedSourceStack.Value
        };
        return true;
    }

    private static string Read(
        IReadOnlyDictionary<string, string> values,
        string name) => values.TryGetValue(name, out var value) ? value : string.Empty;

    private static int? ReadInt(
        IReadOnlyDictionary<string, string> values,
        string name) => int.TryParse(Read(values, name), out var value) ? value : null;
}
