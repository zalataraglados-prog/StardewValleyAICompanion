using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropProductionTeacherDecisionBuilder
{
    private static JsonElement ReadStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field) => TryReadStateValue(snapshot, section, field) ??
        throw new InvalidDataException(
            $"Money Revenue crop-production Teacher field is unavailable: {section}.{field}");

    private static JsonElement? TryReadStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            !FieldEnvelopeValidator.IsReadableStatus(status.GetString()) ||
            !envelope.TryGetProperty("value", out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return value;
    }

    private static int ReadStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadStateValue(snapshot, section, field);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                $"Money Revenue crop-production Teacher integer is invalid: {section}.{field}");
        }
        return result;
    }

    private static string ReadStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadStateValue(snapshot, section, field);
        Require(value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()),
            $"Money Revenue crop-production Teacher string is invalid: {section}.{field}");
        return value.GetString()!;
    }

    private static JsonElement RequiredProperty(
        JsonElement value,
        string name)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(name, out var result))
        {
            throw new InvalidDataException(
                "Money Revenue crop-production Teacher property is missing: " +
                name);
        }
        return result;
    }

    private static string ReadString(JsonElement value, string name)
    {
        var property = RequiredProperty(value, name);
        Require(property.ValueKind == JsonValueKind.String,
            "Money Revenue crop-production Teacher string is invalid: " + name);
        return property.GetString() ?? string.Empty;
    }

    private static string[] ReadStringArray(JsonElement value, string name)
    {
        var property = RequiredProperty(value, name);
        Require(property.ValueKind == JsonValueKind.Array &&
                property.EnumerateArray().All(row =>
                    row.ValueKind == JsonValueKind.String),
            "Money Revenue crop-production Teacher array is invalid: " + name);
        return property.EnumerateArray()
            .Select(row => row.GetString() ?? string.Empty)
            .ToArray();
    }

    private static int ReadInt(JsonElement value, string name) =>
        ReadOptionalInt(value, name) ?? throw new InvalidDataException(
            "Money Revenue crop-production Teacher integer is missing: " + name);

    private static int? ReadOptionalInt(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(name, out var property) ||
            property.ValueKind == JsonValueKind.Null)
            return null;
        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                "Money Revenue crop-production Teacher integer is invalid: " +
                name);
        }
        return result;
    }

    private static bool? ReadOptionalBool(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(name, out var property) ||
            property.ValueKind == JsonValueKind.Null)
            return null;
        Require(property.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Money Revenue crop-production Teacher boolean is invalid: " + name);
        return property.GetBoolean();
    }
}
