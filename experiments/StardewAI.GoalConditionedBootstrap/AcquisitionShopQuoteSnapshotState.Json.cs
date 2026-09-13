using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed partial class AcquisitionShopQuoteSnapshotState
{
    private static bool TryFieldValue(
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
            status.GetString() == "available" &&
            envelope.TryGetProperty("confidence", out var confidence) &&
            confidence.TryGetDouble(out var confidenceValue) &&
            confidenceValue == 1d &&
            envelope.TryGetProperty("value", out value);
    }

    private static string ReadString(JsonElement value, string name) =>
        ReadNullableString(value, name) ?? string.Empty;

    private static string? ReadNullableString(
        JsonElement value,
        string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? ReadInt(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var result)
            ? result
            : null;

    private static bool? ReadBool(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
}
