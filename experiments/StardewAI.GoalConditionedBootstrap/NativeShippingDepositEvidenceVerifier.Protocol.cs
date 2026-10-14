using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShippingDepositEvidenceVerifier
{
    private static string ReadUniqueParameter(
        ActionQueueItem? item,
        string name,
        ICollection<string> reasons)
    {
        var matches = (item?.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(value => string.Equals(value.Name, name,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 1 &&
            !string.IsNullOrWhiteSpace(matches[0].Value))
        {
            return matches[0].Value;
        }
        reasons.Add("shipping_deposit_parameter_invalid:" + name);
        return string.Empty;
    }

    private static int? ReadUniqueIntParameter(
        ActionQueueItem? item,
        string name,
        int minimum,
        ICollection<string> reasons)
    {
        var value = ReadUniqueParameter(item, name, reasons);
        if (int.TryParse(value, out var result) && result >= minimum)
            return result;
        reasons.Add("shipping_deposit_integer_parameter_invalid:" + name);
        return null;
    }

    private static bool ParameterEquals(
        ActionQueueItem item,
        string name,
        string expected)
    {
        var matches = (item.NormalizedCommand?.Parameters ??
                Array.Empty<SmallModelActionParameter>())
            .Where(value => string.Equals(value.Name, name,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 && string.Equals(
            matches[0].Value,
            expected,
            StringComparison.Ordinal);
    }

    private static long? ReadStateLong(
        SnapshotEnvelope snapshot,
        string section,
        string field) =>
        TryStateValue(snapshot, section, field, out var value) &&
        value.TryGetInt64(out var result)
            ? result
            : null;

    private static bool TryStateValue(
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

    private static string ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty
            : string.Empty;

    private static string ReadLongOrString(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out var field))
            return string.Empty;
        return field.ValueKind switch
        {
            JsonValueKind.String => field.GetString() ?? string.Empty,
            JsonValueKind.Number when field.TryGetInt64(out var number) =>
                number.ToString(),
            _ => string.Empty
        };
    }

    private static int ReadInt(JsonElement value, string property) =>
        TryReadInt(value, property, out var result) ? result : int.MinValue;

    private static bool TryReadInt(
        JsonElement value,
        string property,
        out int result)
    {
        result = 0;
        if (!value.TryGetProperty(property, out var field))
            return false;
        return field.ValueKind switch
        {
            JsonValueKind.Number => field.TryGetInt32(out result),
            JsonValueKind.String => int.TryParse(field.GetString(), out result),
            _ => false
        };
    }

    private static bool TryReadNonNegativeInt(
        JsonElement value,
        string property,
        out int result) =>
        TryReadInt(value, property, out result) && result >= 0;

    private static int RequiredNonNegativeInt(
        JsonElement value,
        string property) =>
        TryReadNonNegativeInt(value, property, out var result)
            ? result
            : throw new InvalidDataException(
                "Required non-negative integer is unavailable: " + property);

    private static long RequiredNonNegativeLong(
        JsonElement value,
        string property) =>
        TryReadLong(value, property, out var result) && result >= 0
            ? result
            : throw new InvalidDataException(
                "Required non-negative long is unavailable: " + property);

    private static bool TryReadLong(
        JsonElement value,
        string property,
        out long result)
    {
        result = 0;
        return value.TryGetProperty(property, out var field) &&
            field.TryGetInt64(out result);
    }

    private static bool? ReadBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? field.GetBoolean()
            : null;
}
