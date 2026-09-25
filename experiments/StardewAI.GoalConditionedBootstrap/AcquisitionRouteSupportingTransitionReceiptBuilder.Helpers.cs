using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    private static bool TryStateFieldValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool TryStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out int value)
    {
        value = 0;
        return TryStateFieldValue(snapshot, section, field, out var element) &&
            element.TryGetInt32(out value);
    }

    private static bool TryStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out string value)
    {
        value = string.Empty;
        if (!TryStateFieldValue(snapshot, section, field, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasUniqueParameter(
        SmallModelActionParameter[]? parameters,
        string name,
        string expected) => string.Equals(
            UniqueParameter(parameters, name),
            expected,
            StringComparison.Ordinal);

    private static string UniqueParameter(
        SmallModelActionParameter[]? parameters,
        string name)
    {
        var matches = (parameters ?? Array.Empty<SmallModelActionParameter>())
            .Where(value => value.Name == name)
            .Select(value => value.Value)
            .ToArray();
        return matches.Length == 1 ? matches[0] : string.Empty;
    }

    private static int? UniqueIntParameter(
        SmallModelActionParameter[]? parameters,
        string name) => int.TryParse(
            UniqueParameter(parameters, name),
            out var value)
                ? value
                : null;

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

    private static bool TryReadBool(
        JsonElement value,
        string property,
        out bool result)
    {
        result = false;
        if (!value.TryGetProperty(property, out var field) ||
            field.ValueKind is not
                (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        result = field.GetBoolean();
        return true;
    }
}
