using System;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class MaterialInventoryGraphReader
{
    public static bool TryRead(
        SnapshotEnvelope snapshot,
        out MaterialInventoryGraph? graph)
    {
        graph = null;
        if (!ReadableStatus(ReadStateFieldStatus(
                snapshot,
                "farm",
                "material_inventory_graph")))
        {
            return false;
        }

        var value = ReadStateFieldValue(
            snapshot,
            "farm",
            "material_inventory_graph");
        if (!value.HasValue || value.Value.ValueKind != JsonValueKind.Object)
            return false;

        try
        {
            graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.Value.GetRawText());
            return graph is not null &&
                string.Equals(
                    graph.SchemaVersion,
                    "material_inventory_graph.v1",
                    StringComparison.Ordinal) &&
                string.Equals(
                    graph.Status,
                    "available",
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
