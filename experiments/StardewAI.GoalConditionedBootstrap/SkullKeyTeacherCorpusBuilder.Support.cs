using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class SkullKeyTeacherCorpusBuilder
{
    private static JsonElement ReadEnvelopeValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!TryReadEnvelopeValue(snapshot, section, field, out var value))
        {
            throw new InvalidDataException(
                $"Skull Key snapshot field {section}.{field} is unavailable.");
        }
        return value;
    }

    private static bool TryReadEnvelopeValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not
                (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out value))
            return false;
        return true;
    }

    private static JsonElement ReadEnvelopeObject(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadEnvelopeValue(snapshot, section, field);
        return value.ValueKind == JsonValueKind.Object
            ? value
            : throw new InvalidDataException(
                $"Skull Key snapshot field {section}.{field} is not an object.");
    }

    private static int ReadEnvelopeInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadEnvelopeValue(snapshot, section, field).GetInt32();

    private static bool ReadEnvelopeBool(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadEnvelopeValue(snapshot, section, field).GetBoolean();

    private static int ReadSnapshotTotalDay(SnapshotEnvelope snapshot)
    {
        if (TryReadEnvelopeValue(
                snapshot,
                "time",
                "total_days",
                out var totalDays) &&
            totalDays.TryGetInt32(out var directTotalDay))
        {
            return directTotalDay;
        }

        var year = ReadEnvelopeInt(snapshot, "time", "year");
        var day = ReadEnvelopeInt(snapshot, "time", "day");
        var seasonValue = ReadEnvelopeValue(snapshot, "time", "season");
        var season = seasonValue.ValueKind == JsonValueKind.String
            ? seasonValue.GetString()
            : null;
        var seasonIndex = season switch
        {
            "spring" => 0,
            "summer" => 1,
            "fall" => 2,
            "winter" => 3,
            _ => -1
        };
        if (year < 1 || day is < 1 or > 28 || seasonIndex < 0)
        {
            throw new InvalidDataException(
                "Skull Key snapshot calendar identity is invalid.");
        }
        return ((year - 1) * 112) + (seasonIndex * 28) + day - 1;
    }

    private static string RequiredIdentity(
        FieldEnvelope<string?> field,
        string label)
    {
        if (!FieldEnvelopeValidator.IsReadableStatus(field.Status) ||
            string.IsNullOrWhiteSpace(field.Value))
        {
            throw new InvalidDataException(
                "Skull Key " + label + " is unavailable.");
        }
        return field.Value;
    }

    private static JsonElement RequiredObject(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out var field) ||
            field.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Skull Key object is missing: " + property);
        }
        return field;
    }

    private static string RequiredConsistentParameter(
        JsonElement command,
        string name)
    {
        if (!command.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Skull Key queue-item parameters are unavailable.");
        }
        var matches = parameters.EnumerateArray()
            .Where(parameter => RequiredString(parameter, "name") == name)
            .Select(parameter => RequiredString(parameter, "value"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                "Skull Key queue item has a missing or conflicting parameter: " +
                name);
    }

    private static IEnumerable<JsonElement> RequiredArray(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out var field) ||
            field.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Skull Key array is missing: " + property);
        }
        return field.EnumerateArray();
    }

    private static string RequiredString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(field.GetString())
            ? field.GetString()!
            : throw new InvalidDataException(
                "Skull Key property is missing: " + property);

    private static int RequiredInt(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "Skull Key integer is missing: " + property);

    private static bool RequiredBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var field) &&
        field.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? field.GetBoolean()
            : throw new InvalidDataException(
                "Skull Key boolean is missing: " + property);

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
            throw new FileNotFoundException(label + " does not exist.", fullPath);
        return fullPath;
    }

    private static string StableId(object value)
    {
        var json = JsonSerializer.Serialize(value, JsonDefaults.Compact);
        return "skull-key-" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant()[..24];
    }

    private static bool EqualJson(object left, object right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Compact),
        JsonSerializer.Serialize(right, JsonDefaults.Compact),
        StringComparison.Ordinal);

    private static readonly string[] RequiredPartitions =
    {
        PolicyDatasetPartitions.Train,
        PolicyDatasetPartitions.Validation,
        PolicyDatasetPartitions.Test
    };
}
