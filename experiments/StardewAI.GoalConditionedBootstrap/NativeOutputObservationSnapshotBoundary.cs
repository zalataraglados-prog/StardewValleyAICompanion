using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

// Quantity continuity across capture gaps, not slot metadata or producer provenance.
public static class NativeOutputObservationSnapshotBoundary
{
    public static NativeOutputObservationSummary Read(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        NativeExecutionSourceSupport.ValidateSnapshot(before);
        NativeExecutionSourceSupport.ValidateSnapshot(after);
        Require(NativeExecutionSourceSupport.Identity(before.SaveId) == NativeExecutionSourceSupport.Identity(after.SaveId) &&
                NativeExecutionSourceSupport.Identity(before.PlayerId) == NativeExecutionSourceSupport.Identity(after.PlayerId) &&
                before.GameVersion == after.GameVersion && before.StateHash == request.BeforeStateHash &&
                before.GameTick <= after.GameTick && NativeExecutionSourceSupport.TotalDay(before) == NativeExecutionSourceSupport.TotalDay(after) &&
                Location(before) == locationId && Location(after) == locationId,
            "original snapshot identity/day/location changed");
        var summary = NativeOutputObservationBoundary.Read(request, receipt, before.GameTick, after.GameTick, locationId);
        var first = Inventory(before);
        var last = Inventory(after);
        var observed = summary.Inventory.ToDictionary(row => (row.QualifiedItemId, row.Quality));
        foreach (var key in first.Keys.Concat(last.Keys).Concat(observed.Keys).Distinct())
        {
            observed.TryGetValue(key, out var row);
            Require(first.GetValueOrDefault(key) == (row?.InventoryBefore ?? 0) &&
                    last.GetValueOrDefault(key) == (row?.InventoryAfter ?? 0),
                "inventory quantities changed outside the captured native interval");
        }
        return summary;
    }

    private static Dictionary<(string Id, int Quality), int> Inventory(SnapshotEnvelope snapshot)
    {
        var rows = NativeExecutionSourceSupport.Value(snapshot, "player", "inventory");
        Require(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() <= 512,
            "snapshot inventory is missing or exceeds the original observation bound");
        var slots = new HashSet<int>();
        var result = new Dictionary<(string Id, int Quality), int>();
        foreach (var row in rows.EnumerateArray())
        {
            Require(row.ValueKind == JsonValueKind.Object &&
                    row.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == row.EnumerateObject().Count() &&
                    row.TryGetProperty("slot_index", out var slot) && slot.ValueKind == JsonValueKind.Number &&
                    slot.TryGetInt32(out var index) && index is >= 0 and < 512 && slots.Add(index) &&
                    row.TryGetProperty("is_empty", out var empty) && empty.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "snapshot inventory slot identity/availability is ambiguous");
            if (row.GetProperty("is_empty").GetBoolean())
            {
                Require(new[] { "qualified_item_id", "quality", "stack" }.All(name =>
                        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Null),
                    "empty snapshot slot contains an item or incomplete quantity echo");
                continue;
            }
            Require(row.TryGetProperty("qualified_item_id", out var id) && id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()) && row.TryGetProperty("quality", out var quality) &&
                    quality.ValueKind == JsonValueKind.Number && quality.TryGetInt32(out _) &&
                    row.TryGetProperty("stack", out var stack) && stack.ValueKind == JsonValueKind.Number &&
                    stack.TryGetInt32(out var quantity) && quantity > 0,
                "occupied snapshot slot has unreadable item quantities");
            var key = (row.GetProperty("qualified_item_id").GetString()!, row.GetProperty("quality").GetInt32());
            var sum = (long)result.GetValueOrDefault(key) + row.GetProperty("stack").GetInt32();
            Require(sum <= int.MaxValue, "snapshot inventory aggregate exceeds the native count range");
            result[key] = (int)sum;
        }
        Require(slots.SetEquals(Enumerable.Range(0, rows.GetArrayLength())), "snapshot inventory slot sequence is incomplete");
        return result;
    }

    private static string Location(SnapshotEnvelope snapshot) =>
        NativeExecutionSourceSupport.Value(snapshot, "player", "location_id").GetString() ?? string.Empty;
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Native snapshot inventory continuity: " + reason + ".");
    }
}
