using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSharedShippingSettlementProofBuilder
{
    private static WaveState LoadWave(
        MoneyRevenueCropSharedShippingWaveRequest request)
    {
        var path = RequiredFile(
            request.OutputRetentionProofPath,
            "Money Revenue shared crop shipping retention proof");
        return new WaveState(
            request,
            path,
            CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropOutputRetentionProof>(
                    path,
                    "Money Revenue shared crop shipping retention proof"));
    }

    private static int InventoryCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        var inventory = AvailableValue(snapshot, "player", "inventory");
        Require(inventory.ValueKind == JsonValueKind.Array,
            "Money Revenue shared crop shipping inventory is unavailable.");
        return inventory.EnumerateArray().Where(row =>
                row.TryGetProperty("qualified_item_id", out var itemId) &&
                itemId.ValueKind == JsonValueKind.String &&
                itemId.GetString() == qualifiedItemId &&
                row.TryGetProperty("stack", out var stack) &&
                stack.ValueKind == JsonValueKind.Number)
            .Sum(row => row.GetProperty("stack").GetInt32());
    }

    private static int ReadAvailableInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = AvailableValue(snapshot, section, field);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException(
                "Money Revenue shared crop shipping field is not an integer: " +
                section + "." + field);
        }
        return result;
    }

    private static JsonElement AvailableValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.GetString() != "available" ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                "Money Revenue shared crop shipping field is unavailable: " +
                section + "." + field);
        }
        return value;
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record WaveState(
        MoneyRevenueCropSharedShippingWaveRequest Request,
        string RetentionPath,
        MoneyRevenueCropOutputRetentionProof Retention);
}
