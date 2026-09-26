using System.Globalization;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static bool TryArrayElement(
        JsonElement owner,
        string property,
        int index,
        out JsonElement value)
    {
        value = default;
        if (!owner.TryGetProperty(property, out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            index < 0 || index >= rows.GetArrayLength())
        {
            return false;
        }
        value = rows[index];
        return value.ValueKind == JsonValueKind.Object;
    }

    private static JsonElement[] ReadMachineArray(
        JsonElement owner,
        string property,
        bool allowNull)
    {
        Require(owner.TryGetProperty(property, out var rows),
            "Machine property is missing: " + property);
        if (allowNull && rows.ValueKind == JsonValueKind.Null)
            return Array.Empty<JsonElement>();
        Require(rows.ValueKind == JsonValueKind.Array,
            "Machine property is not an array: " + property);
        var result = rows.EnumerateArray().ToArray();
        Require(result.All(value => value.ValueKind == JsonValueKind.Object),
            "Machine array contains a non-object row: " + property);
        return result;
    }

    private static AcquisitionMachineNumericModifierEvidence[] ReadMachineModifiers(
        JsonElement owner,
        string property,
        string sourceKey) => ReadMachineArray(owner, property, allowNull: true)
        .Select((modifier, index) => new AcquisitionMachineNumericModifierEvidence(
            ReadMachineString(modifier, "Id"),
            ReadMachineString(modifier, "Condition"),
            ReadRequiredMachineInt(
                modifier,
                "Modification",
                MachineKey(sourceKey, index)),
            ReadRequiredMachineDouble(
                modifier,
                "Amount",
                MachineKey(sourceKey, index)),
            ReadNullableMachineDouble(
                modifier,
                "RandomAmount",
                MachineKey(sourceKey, index))))
        .ToArray();

    private static string[] ReadMachineStringArray(JsonElement owner, string property)
    {
        Require(owner.TryGetProperty(property, out var value),
            "Machine property is missing: " + property);
        if (value.ValueKind == JsonValueKind.Null)
            return Array.Empty<string>();
        Require(value.ValueKind == JsonValueKind.Array,
            "Machine property is not a string array: " + property);
        var result = value.EnumerateArray().Select(item =>
        {
            Require(item.ValueKind == JsonValueKind.String,
                "Machine string array contains a non-string: " + property);
            return item.GetString() ?? string.Empty;
        }).ToArray();
        return result;
    }

    private static string ReadMachineString(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }
        Require(value.ValueKind == JsonValueKind.String,
            "Machine property is not a string: " + property);
        return value.GetString() ?? string.Empty;
    }

    private static string ReadMachineStringOrJson(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static int ReadRequiredMachineInt(
        JsonElement owner,
        string property,
        string sourceKey)
    {
        var result = 0;
        Require(owner.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out result),
            "Machine integer property is invalid: " + sourceKey + ":" + property);
        return result;
    }

    private static bool ReadRequiredMachineBool(
        JsonElement owner,
        string property,
        string sourceKey)
    {
        Require(owner.TryGetProperty(property, out var value) &&
                value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Machine boolean property is invalid: " + sourceKey + ":" + property);
        return value.GetBoolean();
    }

    private static double ReadRequiredMachineDouble(
        JsonElement owner,
        string property,
        string sourceKey)
    {
        var result = 0d;
        Require(owner.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out result) &&
                double.IsFinite(result),
            "Machine numeric property is invalid: " + sourceKey + ":" + property);
        return result;
    }

    private static double? ReadNullableMachineDouble(
        JsonElement owner,
        string property,
        string sourceKey)
    {
        Require(owner.TryGetProperty(property, out var value),
            "Machine property is missing: " + sourceKey + ":" + property);
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        var result = 0d;
        Require(value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out result) &&
                double.IsFinite(result),
            "Machine nullable numeric property is invalid: " + sourceKey + ":" + property);
        return result;
    }

    private static bool HasRandomMachineExpression(string value) =>
        value.Contains("RANDOM", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("CHOICE", StringComparison.OrdinalIgnoreCase);

    private static string MachineKey(string machineId, int first) =>
        machineId + ":" + first.ToString(CultureInfo.InvariantCulture);

    private static string MachineKey(string machineId, int first, int second) =>
        MachineKey(machineId, first) + ":" +
        second.ToString(CultureInfo.InvariantCulture);
}
