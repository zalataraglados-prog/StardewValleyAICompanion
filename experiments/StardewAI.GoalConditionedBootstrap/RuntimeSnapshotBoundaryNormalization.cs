using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static class RuntimeSnapshotBoundaryNormalization
{
    internal static void RemoveNonSemanticMetadata(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                RemoveNonSemanticMetadata(item);
            return;
        }
        if (node is not JsonObject objectNode)
            return;

        foreach (var fieldName in new[]
                 {
                     "read_at_tick",
                     "machine_probe_cache_tick",
                     "projection_tick"
                 })
        {
            objectNode.Remove(fieldName);
        }
        foreach (var child in objectNode.ToArray())
            RemoveNonSemanticMetadata(child.Value);
    }
}
