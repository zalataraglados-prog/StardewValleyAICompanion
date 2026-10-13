using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static JsonElement ReadEnvelopeValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not
                (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Money-threshold snapshot field {section}.{field} is unavailable.");
        }
        return value;
    }

    private static int ReadEnvelopeInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) =>
        ReadEnvelopeValue(snapshot, section, field).GetInt32();

    private static uint ReadEnvelopeUInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) =>
        ReadEnvelopeValue(snapshot, section, field).GetUInt32();

    private static string RequiredIdentity(
        FieldEnvelope<string?> field,
        string label)
    {
        if (!FieldEnvelopeValidator.IsReadableStatus(field.Status) ||
            string.IsNullOrWhiteSpace(field.Value))
        {
            throw new InvalidDataException(
                "Money-threshold " + label + " is unavailable.");
        }
        return field.Value;
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static string StableId(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private static bool EqualJson(object left, object right) =>
        JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
}
