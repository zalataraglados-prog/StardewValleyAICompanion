using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static int? InventoryCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId,
        int minimumQuality) => ExactInventoryReceiptVerifier.InventoryCount(
        snapshot,
        qualifiedItemId,
        minimumQuality);

    private static int? ShippingBinCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        if (!TryStateValue(snapshot, "farm", "shipping_bins", out var bins) ||
            bins.ValueKind != JsonValueKind.Array)
            return null;
        int? count = null;
        string? scopeIdentity = null;
        foreach (var bin in bins.EnumerateArray())
        {
            if (!bin.TryGetProperty("contents", out var contents) ||
                contents.ValueKind != JsonValueKind.Array)
                return null;
            var binCount = 0;
            foreach (var row in contents.EnumerateArray().Where(row =>
                         ReadString(row, "qualified_item_id") == qualifiedItemId))
            {
                if (!TryReadInt(row, "count", out var stack))
                    return null;
                binCount += Math.Max(0, stack);
            }
            var currentScopeIdentity =
                ReadString(bin, "bin_scope") + ":" +
                (bin.TryGetProperty("player_id", out var playerId)
                    ? playerId.ToString()
                    : string.Empty);
            if (count.HasValue &&
                (count.Value != binCount ||
                 !string.Equals(scopeIdentity, currentScopeIdentity,
                     StringComparison.Ordinal)))
                return null;
            count = binCount;
            scopeIdentity = currentScopeIdentity;
        }
        return count ?? 0;
    }

    private static bool? ReadCollectionBoolean(
        SnapshotEnvelope snapshot,
        string field,
        string rowsProperty,
        string identityProperty,
        string identity,
        string completionProperty)
    {
        if (!TryStateValue(snapshot, "world_progress", field, out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(rowsProperty, out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return null;
        var matches = rows.EnumerateArray()
            .Where(row => ReadString(row, identityProperty) == identity)
            .ToArray();
        return matches.Length == 1 &&
            matches[0].TryGetProperty(completionProperty, out var completion) &&
            completion.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? completion.GetBoolean()
                : null;
    }

    private static bool? ReadBundleAlternative(
        SnapshotEnvelope snapshot,
        string bundleKey,
        int alternativeIndex,
        string qualifiedItemId)
    {
        if (!TryStateValue(snapshot, "world_progress", "community_center",
                out var value) ||
            value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("bundle_rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
            return null;
        var bundles = rows.EnumerateArray()
            .Where(row => ReadString(row, "bundle_data_key") == bundleKey)
            .ToArray();
        if (bundles.Length != 1 ||
            !bundles[0].TryGetProperty("ingredients", out var ingredients) ||
            ingredients.ValueKind != JsonValueKind.Array)
            return null;
        var matches = ingredients.EnumerateArray()
            .Where(row => ReadInt(row, "ingredient_index") == alternativeIndex)
            .Where(row =>
            {
                var identity = ReadString(row, "item_id_or_category");
                return qualifiedItemId.Length == 0 ||
                    identity.StartsWith("-", StringComparison.Ordinal) ||
                    qualifiedItemId == "(O)" + identity;
            })
            .ToArray();
        return matches.Length == 1 &&
            matches[0].TryGetProperty("completed", out var completed) &&
            completed.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? completed.GetBoolean()
                : null;
    }

    private static string ReadParameter(ActionQueueItem item, string name)
    {
        var values = (item.NormalizedCommand.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(value => string.Equals(
                value.Name,
                name,
                StringComparison.Ordinal))
            .Select(value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return values.Length == 1 ? values[0] : string.Empty;
    }

    private static int? ReadIntParameter(ActionQueueItem item, string name) =>
        int.TryParse(ReadParameter(item, name), out var value) ? value : null;

    private static string ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.TryGetInt32(out var result)
            ? result
            : 0;

    private static bool TryReadInt(
        JsonElement value,
        string property,
        out int result)
    {
        result = 0;
        return value.TryGetProperty(property, out var field) &&
            field.TryGetInt32(out result);
    }
}
