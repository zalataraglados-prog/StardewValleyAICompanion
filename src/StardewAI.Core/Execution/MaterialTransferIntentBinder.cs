using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public static class MaterialTransferIntentBinder
{
    public const string PhaseParameter = "material_transfer_phase";
    public const string ApproachPhase = "approach";
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

    private static readonly IReadOnlyList<string> ReadOnlyRequiredNames = Array.AsReadOnly(RequiredNames);
    public static IReadOnlyList<string> RequiredParameterNames => ReadOnlyRequiredNames;

    public static SmallModelActionParameter[] ToParameters(MaterialTransferIntent intent) => new[]
    {
        Parameter("source_node_id", intent.SourceNodeId), Parameter("destination_node_id", intent.DestinationNodeId),
        Parameter("source_slot_index", intent.SourceSlotIndex.ToString(CultureInfo.InvariantCulture)),
        Parameter("qualified_item_id", intent.QualifiedItemId), Parameter("quality", intent.Quality.ToString(CultureInfo.InvariantCulture)),
        Parameter("quantity", intent.Quantity.ToString(CultureInfo.InvariantCulture)),
        Parameter("expected_source_stack", intent.ExpectedSourceStack.ToString(CultureInfo.InvariantCulture))
    };

    public static bool TryReadGraph(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph) =>
        MaterialInventoryGraphReader.TryRead(snapshot, out graph);

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
        string name) => int.TryParse(Read(values, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}
