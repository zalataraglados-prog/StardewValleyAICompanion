using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static Dictionary<int, JsonElement> VerifyPickupSnapshotSlots(SnapshotEnvelope snapshot, NativeObservedInventoryPayloadState payload)
    {
        var rows = NativeExecutionSourceSupport.Value(snapshot, "player", "inventory");
        Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == payload.Slots.Count,
            "pickup payload slot denominator differs from its raw snapshot");
        var indexed = rows.EnumerateArray().ToDictionary(row => row.GetProperty("slot_index").GetInt32());
        foreach (var slot in payload.Slots)
        {
            Require(indexed.TryGetValue(slot.SlotIndex, out var row) && row.GetProperty("is_empty").GetBoolean() == slot.IsEmpty,
                "pickup raw slot identity/availability differs from the original payload");
            if (slot.IsEmpty) continue;
            Require(row.TryGetProperty("runtime_type", out var type) && type.GetString() == slot.RuntimeType &&
                    row.GetProperty("qualified_item_id").GetString() == slot.QualifiedItemId &&
                    row.GetProperty("quality").GetInt32() == slot.Quality && row.GetProperty("stack").GetInt32() == slot.Stack &&
                    row.TryGetProperty("maximum_stack_size", out var maximum) && maximum.TryGetInt32(out var limit) &&
                    limit > 0 && slot.Stack <= limit,
                "pickup raw slot item, metadata header or stack bound differs from its original payload");
        }
        return indexed;
    }

    private static void VerifyPickupRawRow(JsonElement first, JsonElement next, int stack)
    {
        var expected = JsonNode.Parse(first.GetRawText())!.AsObject();
        expected["stack"] = stack;
        Require(JsonNode.DeepEquals(expected, JsonNode.Parse(next.GetRawText())),
            "pickup changed unexplained raw slot metadata");
    }

    private static void VerifyPickupRawEmpty(JsonElement first, JsonElement next) =>
        Require(JsonNode.DeepEquals(JsonNode.Parse(first.GetRawText()), JsonNode.Parse(next.GetRawText())),
            "pickup changed unexplained empty-slot metadata");
}
