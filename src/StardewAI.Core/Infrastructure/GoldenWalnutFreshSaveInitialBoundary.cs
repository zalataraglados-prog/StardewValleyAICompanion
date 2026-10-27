using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutFreshSaveInitialBoundary(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("training_label_eligible")] bool TrainingLabelEligible,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("save_id")] string SaveId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("source_game_tick")] long SourceGameTick,
    [property: JsonPropertyName("source_total_day")] int SourceTotalDay,
    [property: JsonPropertyName("snapshot_path")] string SnapshotPath,
    [property: JsonPropertyName("snapshot_logical_sha256")] string SnapshotLogicalSha256);

public static class GoldenWalnutFreshSaveInitialBoundaryValidator
{
    public const string SchemaVersion =
        "golden_walnut_fresh_save_initial_boundary.v1";
    public const string SearchPattern =
        "golden-walnut-fresh-save-initial-boundary-*.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string PathFor(string artifactDirectory, string runId)
    {
        if (string.IsNullOrWhiteSpace(artifactDirectory))
            throw new ArgumentException("Artifact directory is required.", nameof(artifactDirectory));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));
        return Path.Combine(
            Path.GetFullPath(artifactDirectory),
            "golden-walnut-fresh-save-initial-boundary-" +
            Sha256(Encoding.UTF8.GetBytes(runId))[..12] + ".json");
    }

    public static GoldenWalnutFreshSaveInitialBoundary Build(
        string snapshotPath,
        string runId,
        string sourceStateHash)
    {
        var snapshot = ReadSnapshot(snapshotPath, out var snapshotSha256);
        ValidateSnapshot(snapshot);
        Require(snapshot.StateHash == sourceStateHash,
            "Golden Walnut initial snapshot state hash does not match the first episode.");
        return new GoldenWalnutFreshSaveInitialBoundary(
            SchemaVersion,
            false,
            runId,
            snapshot.SaveId.Value!,
            snapshot.GameVersion,
            snapshot.StateHash,
            snapshot.GameTick,
            ReadStateFieldInt(snapshot, "time", "total_days"),
            Path.GetFileName(Path.GetFullPath(snapshotPath)),
            snapshotSha256);
    }

    public static SnapshotEnvelope Validate(
        GoldenWalnutFreshSaveInitialBoundary boundary,
        string boundaryPath,
        string expectedRunId)
    {
        Require(
            boundary.SchemaVersion == SchemaVersion &&
            !boundary.TrainingLabelEligible &&
            boundary.RunId == expectedRunId &&
            boundary.GameVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion &&
            boundary.SourceTotalDay == 0 &&
            !string.IsNullOrWhiteSpace(boundary.SaveId) &&
            !string.IsNullOrWhiteSpace(boundary.SourceStateHash) &&
            !string.IsNullOrWhiteSpace(boundary.SnapshotPath) &&
            boundary.SnapshotLogicalSha256.Length == 64,
            "Golden Walnut initial boundary metadata is invalid.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(boundaryPath)) ??
            Directory.GetCurrentDirectory();
        var snapshotPath = Path.GetFullPath(Path.IsPathRooted(boundary.SnapshotPath)
            ? boundary.SnapshotPath
            : Path.Combine(directory, boundary.SnapshotPath));
        var snapshot = ReadSnapshot(snapshotPath, out var snapshotSha256);
        ValidateSnapshot(snapshot);
        Require(
            snapshotSha256 == boundary.SnapshotLogicalSha256 &&
            snapshot.SaveId.Value == boundary.SaveId &&
            snapshot.GameVersion == boundary.GameVersion &&
            snapshot.StateHash == boundary.SourceStateHash &&
            snapshot.GameTick == boundary.SourceGameTick &&
            ReadStateFieldInt(snapshot, "time", "total_days") ==
                boundary.SourceTotalDay,
            "Golden Walnut initial boundary snapshot identity changed.");
        return snapshot;
    }

    private static void ValidateSnapshot(SnapshotEnvelope snapshot)
    {
        Require(
            snapshot.SchemaVersion == "snapshot.v1" &&
            snapshot.GameVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion &&
            snapshot.SaveId.Value is { Length: > 0 } &&
            !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
            ReadStateFieldInt(snapshot, "time", "total_days") == 0,
            "Golden Walnut evidence must begin at a readable vanilla fresh-save day-zero boundary.");

        var access = ReadStateFieldValue(
            snapshot,
            "locations",
            "ginger_island_access");
        Require(
            ReadString(access, "schema_version") == "ginger_island_access.v1" &&
            !ReadNestedBool(access, "journey", "island_reached") &&
            !ReadNestedBool(access, "first_parrot", "complete_or_pending") &&
            !ReadNestedBool(access, "western_turtle_parrot", "complete_or_pending") &&
            !ReadNestedBool(access, "island_house_parrot", "complete_or_pending"),
            "Golden Walnut evidence must begin before Island access and parrot unlocks.");

        var catalog = JsonSerializer.Deserialize<GoldenWalnutSourceCatalogRef>(
            ReadStateFieldValue(
                snapshot,
                "world_progress",
                "golden_walnut_sources").GetRawText(),
            JsonOptions) ?? throw new InvalidDataException(
                "Golden Walnut initial source catalog is unreadable.");
        Require(
            catalog.SchemaVersion == GoldenWalnutNativeSourceCatalog.SchemaVersion &&
            catalog.ProjectionStatus == GoldenWalnutNativeSourceCatalog.ProjectionStatus &&
            catalog.NativeSourceKeyCount == GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount &&
            catalog.NativeWalnutTotal == GoldenWalnutNativeSourceCatalog.NativeWalnutTotal &&
            catalog.TrackedCollectedQuantity == 0 &&
            catalog.WorldGoldenWalnutsFound == 0 &&
            catalog.Sources.Length == GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount,
            "Golden Walnut initial source catalog metadata is incomplete or progressed.");
        Require(
            catalog.Sources.All(row => !string.IsNullOrWhiteSpace(row.SourceKey)) &&
            catalog.Sources.Select(row => row.SourceKey)
                .Distinct(StringComparer.Ordinal).Count() == catalog.Sources.Length,
            "Golden Walnut initial source catalog contains missing or duplicate source keys.");
        var rows = catalog.Sources.ToDictionary(
            row => row.SourceKey,
            StringComparer.Ordinal);
        foreach (var source in GoldenWalnutNativeSourceCatalog.Sources)
        {
            Require(
                rows.TryGetValue(source.SourceKey, out var row) &&
                row.NativeQuantity == source.NativeQuantity &&
                row.CollectedQuantity == 0 &&
                row.RemainingQuantity == source.NativeQuantity &&
                row.AcquisitionOptionId == source.AcquisitionOptionId,
                "Golden Walnut initial source row is missing or progressed: " +
                source.SourceKey);
        }
    }

    private static SnapshotEnvelope ReadSnapshot(
        string path,
        out string logicalSha256)
    {
        Require(File.Exists(path),
            "Golden Walnut initial snapshot is missing: " + path);
        var logicalJson = ContentAddressedJsonArtifactReader.ReadAllText(path);
        logicalSha256 = Sha256(Encoding.UTF8.GetBytes(logicalJson));
        return JsonSerializer.Deserialize<SnapshotEnvelope>(logicalJson, JsonOptions) ??
            throw new InvalidDataException(
                "Golden Walnut initial snapshot is unreadable: " + path);
    }

    private static JsonElement ReadStateFieldValue(
        SnapshotEnvelope snapshot,
        string domain,
        string key)
    {
        if (!snapshot.State.TryGetValue(domain, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Snapshot field {domain}.{key} is unavailable.");
        }
        return value;
    }

    private static int ReadStateFieldInt(
        SnapshotEnvelope snapshot,
        string domain,
        string key)
    {
        var value = ReadStateFieldValue(snapshot, domain, key);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw new InvalidDataException($"Snapshot field {domain}.{key} is not an integer.");
        return result;
    }

    private static bool ReadNestedBool(
        JsonElement value,
        string section,
        string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(section, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        nested.TryGetProperty(key, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : throw new InvalidDataException(
                $"Ginger Island initial field {section}.{key} is unavailable.");

    private static string ReadString(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(key, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static string Sha256(byte[] value)
    {
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(value))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
