using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionProcessingLeadTimeSnapshotState
{
    private static SnapshotArrayState ReadArrayState(
        JsonElement state,
        string section,
        string field,
        string evidencePath)
    {
        if (!TryAvailableValue(state, section, field, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return SnapshotArrayState.Unavailable(
                evidencePath + ":missing_or_unavailable");
        }
        return new SnapshotArrayState(
            true,
            value.EnumerateArray().Select(row => row.Clone()).ToArray(),
            Array.Empty<string>(),
            evidencePath);
    }

    private static bool TryAvailableValue(
        JsonElement state,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return state.TryGetProperty(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static string RequiredAvailableString(
        JsonElement state,
        string section,
        string field)
    {
        Require(TryAvailableValue(state, section, field, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()),
            "Snapshot " + section + "." + field + " is unavailable.");
        return value.GetString()!;
    }

    private static JsonElement RequiredObject(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var result) &&
                result.ValueKind == JsonValueKind.Object,
            "Snapshot " + name + " is missing.");
        return result;
    }

    internal static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    internal static bool TryReadBool(
        JsonElement row,
        string property,
        out bool result)
    {
        result = false;
        if (!row.TryGetProperty(property, out var value) ||
            value.ValueKind is not JsonValueKind.True and
                not JsonValueKind.False)
        {
            return false;
        }
        result = value.GetBoolean();
        return true;
    }

    internal static bool TryReadInt(
        JsonElement row,
        string property,
        out int result)
    {
        result = 0;
        return row.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out result);
    }

    private static bool TryReadNullableNonNegativeInt(
        JsonElement row,
        string property,
        out int? result)
    {
        result = null;
        if (!row.TryGetProperty(property, out var value))
            return false;
        if (value.ValueKind == JsonValueKind.Null)
            return true;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) &&
            number >= 0)
        {
            result = number;
            return true;
        }
        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record SnapshotArrayState(
        bool Available,
        JsonElement[] Rows,
        string[] Reasons,
        string EvidencePath)
    {
        public static SnapshotArrayState Unavailable(string reason) => new(
            false,
            Array.Empty<JsonElement>(),
            new[] { reason },
            string.Empty);
    }
}
