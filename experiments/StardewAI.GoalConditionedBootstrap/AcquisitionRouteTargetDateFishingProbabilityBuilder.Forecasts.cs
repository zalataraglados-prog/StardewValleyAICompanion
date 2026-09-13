using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFishingProbabilityBuilder
{
    private const long MaximumForecastTickLag = 30;

    private static LoadedFishingForecast[] LoadForecasts(
        string manifestPath,
        string baseSnapshotPath,
        string gameVersion,
        int targetTotalDay,
        string expectedBaseStateHash)
    {
        var manifest = CurrentTeacherFrontierSupport.Read<
            FishingForecastSnapshotManifest>(
            manifestPath,
            "Fishing forecast snapshot manifest");
        Require(manifest.SchemaVersion ==
                "fishing_forecast_snapshot_manifest.v1",
            "Fishing forecast manifest schema is unsupported.");
        Require(manifest.Snapshots.All(reference =>
                    !string.IsNullOrWhiteSpace(reference.RequestId) &&
                    !string.IsNullOrWhiteSpace(reference.TargetLocationId) &&
                    reference.RodSlotIndex >= 0 &&
                    !string.IsNullOrWhiteSpace(reference.SnapshotPath) &&
                    IsSha256(reference.SnapshotSha256)) &&
                manifest.Snapshots.Select(reference => reference.RequestId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                manifest.Snapshots.Length &&
                manifest.Snapshots.Select(reference =>
                        reference.TargetLocationId + "\n" +
                        reference.RodSlotIndex)
                    .Distinct(StringComparer.Ordinal).Count() ==
                manifest.Snapshots.Length,
            "Fishing forecast manifest identities are invalid or duplicate.");

        using var baseDocument = JsonDocument.Parse(
            File.ReadAllText(baseSnapshotPath));
        var baseRoot = baseDocument.RootElement;
        var baseStateHash = AcquisitionTargetDateSnapshotValidator.Validate(
            baseRoot,
            gameVersion,
            targetTotalDay);
        Require(string.Equals(
                baseStateHash,
                expectedBaseStateHash,
                StringComparison.Ordinal),
            "Base snapshot state hash disagrees with processing evidence.");
        var baseTick = RequiredLong(baseRoot, "game_tick");
        var baseSaveId = RequiredEnvelopeString(baseRoot, "save_id");
        var basePlayerId = RequiredEnvelopeString(baseRoot, "player_id");
        var baseTime = RequiredAvailableInt(baseRoot, "time", "time");

        var manifestDirectory = Path.GetDirectoryName(manifestPath) ??
                                Directory.GetCurrentDirectory();
        var loaded = new List<LoadedFishingForecast>();
        foreach (var reference in manifest.Snapshots
                     .OrderBy(value => value.TargetLocationId, StringComparer.Ordinal)
                     .ThenBy(value => value.RodSlotIndex))
        {
            var snapshotPath = Path.GetFullPath(reference.SnapshotPath,
                manifestDirectory);
            Require(File.Exists(snapshotPath),
                "Fishing forecast snapshot does not exist: " + snapshotPath);
            var digest = CurrentTeacherFrontierSupport.HashFile(snapshotPath);
            Require(string.Equals(
                    digest,
                    reference.SnapshotSha256,
                    StringComparison.OrdinalIgnoreCase),
                "Fishing forecast snapshot digest mismatch: " +
                reference.RequestId);
            using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            var root = document.RootElement;
            AcquisitionTargetDateSnapshotValidator.Validate(
                root,
                gameVersion,
                targetTotalDay);
            ValidateSnapshotStateHash(root, reference.RequestId);
            var forecastTick = RequiredLong(root, "game_tick");
            Require(forecastTick >= baseTick &&
                    forecastTick - baseTick <= MaximumForecastTickLag,
                "Fishing forecast is stale relative to the base snapshot: " +
                reference.RequestId);
            Require(string.Equals(
                        RequiredEnvelopeString(root, "save_id"),
                        baseSaveId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        RequiredEnvelopeString(root, "player_id"),
                        basePlayerId,
                        StringComparison.Ordinal) &&
                    RequiredAvailableInt(root, "time", "time") == baseTime,
                "Fishing forecast save, player, or in-game time identity mismatch: " +
                reference.RequestId);
            var request = RequiredAvailableObject(
                root,
                "fishing",
                "forecast_request");
            Require(Bool(request, "request_complete") == true &&
                    string.Equals(
                        String(request, "target_location_id"),
                        reference.TargetLocationId,
                        StringComparison.Ordinal) &&
                    Int(request, "rod_slot_index") == reference.RodSlotIndex,
                "Fishing forecast request identity mismatch: " +
                reference.RequestId);
            loaded.Add(new LoadedFishingForecast(
                reference.RequestId,
                reference.TargetLocationId,
                reference.RodSlotIndex,
                snapshotPath,
                digest,
                root.Clone()));
        }
        return loaded.ToArray();
    }

    private static void ValidateSnapshotStateHash(
        JsonElement root,
        string requestId)
    {
        var envelope = JsonSerializer.Deserialize<SnapshotEnvelope>(
            root.GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Require(envelope is not null &&
                string.Equals(
                    envelope.StateHash,
                    SnapshotHash.ComputeStateHash(envelope.State),
                    StringComparison.OrdinalIgnoreCase),
            "Fishing forecast state hash mismatch: " + requestId);
    }

    private static int ReadFishableTileCount(JsonElement snapshot)
    {
        var tiles = RequiredAvailableArray(snapshot, "fishing", "fishable_tiles");
        return tiles.GetArrayLength();
    }

    private static (int X, int Y) ReadFishableTile(
        JsonElement snapshot,
        int tileIndex)
    {
        var tiles = RequiredAvailableArray(snapshot, "fishing", "fishable_tiles");
        var tile = tiles[tileIndex];
        var x = Int(tile, "tile_x");
        var y = Int(tile, "tile_y");
        Require(x.HasValue && y.HasValue,
            "Fishing forecast contains a malformed fishable tile.");
        return (x.GetValueOrDefault(), y.GetValueOrDefault());
    }

    private static JsonElement RequiredAvailableArray(
        JsonElement root,
        string section,
        string field)
    {
        var value = RequiredAvailableValue(root, section, field);
        Require(value.ValueKind == JsonValueKind.Array,
            $"Snapshot field {section}.{field} is not an array.");
        return value;
    }

    private static JsonElement RequiredAvailableObject(
        JsonElement root,
        string section,
        string field)
    {
        var value = RequiredAvailableValue(root, section, field);
        Require(value.ValueKind == JsonValueKind.Object,
            $"Snapshot field {section}.{field} is not an object.");
        return value;
    }

    private static int RequiredAvailableInt(
        JsonElement root,
        string section,
        string field)
    {
        var value = RequiredAvailableValue(root, section, field);
        Require(value.TryGetInt32(out var result),
            $"Snapshot field {section}.{field} is not an integer.");
        return result;
    }

    private static JsonElement RequiredAvailableValue(
        JsonElement root,
        string section,
        string field)
    {
        var value = default(JsonElement);
        Require(root.TryGetProperty("state", out var state) &&
                state.TryGetProperty(section, out var sectionValue) &&
                sectionValue.TryGetProperty(field, out var envelope) &&
                string.Equals(
                    String(envelope, "status"),
                    "available",
                    StringComparison.Ordinal) &&
                envelope.TryGetProperty("value", out value),
            $"Snapshot field {section}.{field} is unavailable.");
        return value;
    }

    private static string RequiredEnvelopeString(
        JsonElement root,
        string property)
    {
        var value = default(JsonElement);
        Require(root.TryGetProperty(property, out var envelope) &&
                envelope.ValueKind == JsonValueKind.Object &&
                envelope.TryGetProperty("value", out value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()),
            "Snapshot envelope identity is missing: " + property);
        return value.GetString() ?? string.Empty;
    }

    private static long RequiredLong(JsonElement root, string property)
    {
        var parsed = 0L;
        Require(root.TryGetProperty(property, out var value) &&
                value.TryGetInt64(out parsed),
            "Snapshot long field is missing: " + property);
        return parsed;
    }

    private static string String(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int? Int(JsonElement owner, string property) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(property, out var value) &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static bool? Bool(JsonElement owner, string property)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(property, out var value))
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.True ? true :
            value.ValueKind == JsonValueKind.False ? false : null;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private sealed record LoadedFishingForecast(
        string RequestId,
        string TargetLocationId,
        int RodSlotIndex,
        string SnapshotPath,
        string SnapshotSha256,
        JsonElement Snapshot);
}
