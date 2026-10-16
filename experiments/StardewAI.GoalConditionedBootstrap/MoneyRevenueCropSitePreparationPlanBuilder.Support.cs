using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSitePreparationPlanBuilder
{
    private static SnapshotEnvelope ReadSnapshot(string path, string label)
    {
        var fullPath = RequiredFile(path, label);
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            fullPath,
            label);
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            label + " identity is invalid.");
        return snapshot;
    }

    private static bool SameActor(
        SnapshotEnvelope source,
        SnapshotEnvelope preparation) =>
        source.SaveId.Status == FieldStatus.Available &&
        preparation.SaveId.Status == FieldStatus.Available &&
        source.PlayerId.Status == FieldStatus.Available &&
        preparation.PlayerId.Status == FieldStatus.Available &&
        !string.IsNullOrWhiteSpace(source.SaveId.Value) &&
        !string.IsNullOrWhiteSpace(source.PlayerId.Value) &&
        source.SaveId.Value == preparation.SaveId.Value &&
        source.PlayerId.Value == preparation.PlayerId.Value;

    private static JsonElement ReadStateValue(
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
            status.GetString() is not
                (FieldStatus.Available or FieldStatus.Derived) ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                "Money Revenue crop site-preparation field is unavailable: " +
                section + "." + field);
        }
        return value;
    }

    private static int ReadStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadStateValue(snapshot, section, field).GetInt32();

    private static string ReadStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field) => ReadStateValue(snapshot, section, field).GetString() ??
        throw new InvalidDataException(
            "Money Revenue crop site-preparation string field is empty: " +
            section + "." + field);

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var number)
            ? number
            : int.MinValue;

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
