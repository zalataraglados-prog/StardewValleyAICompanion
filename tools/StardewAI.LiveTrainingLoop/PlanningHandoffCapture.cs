using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed record PlanningHandoffCaptureResult(
    string ManifestPath,
    string SourceStateHash,
    int MapWidth,
    int MapHeight,
    int LayoutSnapshotCount);

public static class PlanningHandoffCapture
{
    private const int MaximumMapDimension = 512;
    private const int LayoutChunkSize = 32;

    public static bool TryReadCropProductionTeacherHandoff(
        JsonObject? ranking,
        out JsonObject handoff)
    {
        handoff = new JsonObject();
        var resolution = ranking?["goal_resolution"] as JsonObject;
        var candidate = resolution?["planning_handoff"] as JsonObject;
        if (!string.Equals(
                ReadString(resolution, "status"),
                "planning_handoff_required",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(candidate, "handoff_kind"),
                "crop_production_teacher",
                StringComparison.Ordinal) ||
            !ReadBool(candidate, "automatic_dispatch_required") ||
            candidate?["required_snapshot_profiles"] is not JsonArray profiles ||
            !profiles.Any(profile => string.Equals(
                profile?.GetValue<string>(),
                "farm_layout",
                StringComparison.Ordinal)))
        {
            return false;
        }

        handoff = JsonNode.Parse(candidate.ToJsonString())!.AsObject();
        return true;
    }

    public static async Task<PlanningHandoffCaptureResult> CaptureAsync(
        HttpClient http,
        string bridgeSnapshotUrl,
        string outputDirectory,
        string sourceSnapshotJson,
        JsonObject handoff,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                ReadString(handoff, "handoff_kind"),
                "crop_production_teacher",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "planning_handoff_kind_not_supported");
        }
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
        {
            throw new IOException(
                "planning_handoff_output_already_exists: " +
                Path.GetFullPath(outputDirectory));
        }

        var source = JsonNode.Parse(sourceSnapshotJson)?.AsObject() ??
            throw new InvalidDataException(
                "planning_handoff_source_snapshot_invalid");
        var sourceStateHash = ReadString(source, "state_hash");
        var sourceSaveId = ReadSnapshotString(source, "identity", "save_id");
        var sourcePlayerId = ReadSnapshotString(
            source,
            "identity",
            "player_id");
        var sourceTotalDay = ReadSnapshotInt(source, "time", "total_days");
        var sourceTime = ReadSnapshotInt(source, "time", "time");
        if (string.IsNullOrWhiteSpace(sourceStateHash) ||
            string.IsNullOrWhiteSpace(sourceSaveId) ||
            string.IsNullOrWhiteSpace(sourcePlayerId) ||
            sourceTotalDay < 0 ||
            sourceTime < 0)
        {
            throw new InvalidDataException(
                "planning_handoff_source_identity_or_time_unavailable");
        }

        Directory.CreateDirectory(outputDirectory);
        var layoutDirectory = Path.Combine(outputDirectory, "farm-layout");
        Directory.CreateDirectory(layoutDirectory);
        var sourcePath = Path.Combine(
            outputDirectory,
            "source-snapshot.json");
        await WriteTextAtomicallyAsync(
            sourcePath,
            sourceSnapshotJson,
            cancellationToken);

        var probeJson = await http.GetStringAsync(
            BuildFarmLayoutSnapshotUrl(
                bridgeSnapshotUrl,
                0,
                0,
                0,
                0),
            cancellationToken);
        var probe = JsonNode.Parse(probeJson)?.AsObject() ??
            throw new InvalidDataException(
                "planning_handoff_farm_layout_probe_invalid");
        ValidateSnapshotBoundary(
            probe,
            sourceSaveId,
            sourcePlayerId,
            sourceTotalDay,
            sourceTime);
        var probeContext = ReadSnapshotObject(
            probe,
            "farm",
            "site_preparation_context");
        var mapWidth = ReadInt(probeContext, "map_width", -1);
        var mapHeight = ReadInt(probeContext, "map_height", -1);
        if (mapWidth is < 1 or > MaximumMapDimension ||
            mapHeight is < 1 or > MaximumMapDimension)
        {
            throw new InvalidDataException(
                $"planning_handoff_farm_dimensions_invalid:{mapWidth}x{mapHeight}");
        }

        var probePath = Path.Combine(
            outputDirectory,
            "farm-layout-probe.json");
        await WriteTextAtomicallyAsync(
            probePath,
            probeJson,
            cancellationToken);

        var bindings = new JsonArray();
        var sequence = 0;
        for (var minY = 0; minY < mapHeight; minY += LayoutChunkSize)
        {
            var maxY = Math.Min(mapHeight - 1, minY + LayoutChunkSize - 1);
            for (var minX = 0; minX < mapWidth; minX += LayoutChunkSize)
            {
                var maxX = Math.Min(
                    mapWidth - 1,
                    minX + LayoutChunkSize - 1);
                sequence++;
                var layoutJson = await http.GetStringAsync(
                    BuildFarmLayoutSnapshotUrl(
                        bridgeSnapshotUrl,
                        minX,
                        minY,
                        maxX,
                        maxY),
                    cancellationToken);
                var layout = JsonNode.Parse(layoutJson)?.AsObject() ??
                    throw new InvalidDataException(
                        "planning_handoff_farm_layout_snapshot_invalid");
                ValidateSnapshotBoundary(
                    layout,
                    sourceSaveId,
                    sourcePlayerId,
                    sourceTotalDay,
                    sourceTime);
                var context = ReadSnapshotObject(
                    layout,
                    "farm",
                    "site_preparation_context");
                ValidateLayoutContext(
                    context,
                    mapWidth,
                    mapHeight,
                    minX,
                    minY,
                    maxX,
                    maxY);

                var fileName =
                    $"layout-{sequence:D3}-x{minX}-y{minY}-x{maxX}-y{maxY}.json";
                var path = Path.Combine(layoutDirectory, fileName);
                await WriteTextAtomicallyAsync(
                    path,
                    layoutJson,
                    cancellationToken);
                bindings.Add(new JsonObject
                {
                    ["path"] = Path.GetFullPath(path),
                    ["sha256"] = HashText(layoutJson),
                    ["state_hash"] = ReadString(layout, "state_hash"),
                    ["min_x"] = minX,
                    ["min_y"] = minY,
                    ["max_x"] = maxX,
                    ["max_y"] = maxY,
                    ["tile_count"] = checked(
                        (maxX - minX + 1) * (maxY - minY + 1))
                });
            }
        }

        var expectedCount = checked(
            (int)Math.Ceiling(mapWidth / (double)LayoutChunkSize) *
            (int)Math.Ceiling(mapHeight / (double)LayoutChunkSize));
        if (sequence != expectedCount)
        {
            throw new InvalidDataException(
                "planning_handoff_farm_layout_partition_incomplete");
        }

        var manifestPath = Path.Combine(
            outputDirectory,
            "planning-handoff-capture-manifest.json");
        var manifest = new JsonObject
        {
            ["schema_version"] = "planning_handoff_capture_manifest.v1",
            ["status"] = "farm_layout_capture_complete",
            ["source_state_hash"] = sourceStateHash,
            ["source_save_id"] = sourceSaveId,
            ["source_player_id"] = sourcePlayerId,
            ["source_total_day"] = sourceTotalDay,
            ["source_time"] = sourceTime,
            ["source_snapshot_path"] = Path.GetFullPath(sourcePath),
            ["source_snapshot_sha256"] = HashText(sourceSnapshotJson),
            ["probe_snapshot_path"] = Path.GetFullPath(probePath),
            ["probe_snapshot_sha256"] = HashText(probeJson),
            ["map_width"] = mapWidth,
            ["map_height"] = mapHeight,
            ["layout_chunk_size"] = LayoutChunkSize,
            ["layout_snapshot_count"] = sequence,
            ["layout_snapshots"] = bindings,
            ["planning_handoff"] = JsonNode.Parse(
                handoff.ToJsonString())
        };
        await WriteTextAtomicallyAsync(
            manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }),
            cancellationToken);
        return new PlanningHandoffCaptureResult(
            Path.GetFullPath(manifestPath),
            sourceStateHash,
            mapWidth,
            mapHeight,
            sequence);
    }

    public static string BuildFarmLayoutSnapshotUrl(
        string bridgeSnapshotUrl,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        if (minX < 0 || minY < 0 || maxX < minX || maxY < minY ||
            maxX - minX >= LayoutChunkSize ||
            maxY - minY >= LayoutChunkSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minX),
                "farm_layout bounds must describe one positive <=32x32 chunk");
        }

        var uri = new Uri(bridgeSnapshotUrl, UriKind.Absolute);
        var excluded = new HashSet<string>(
            new[]
            {
                "profile",
                "fresh",
                "min_x",
                "min_y",
                "max_x",
                "max_y",
                "expected_state_hash",
                "expected_game_tick"
            },
            StringComparer.OrdinalIgnoreCase);
        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !excluded.Contains(
                Uri.UnescapeDataString(pair.Split('=', 2)[0])))
            .Append("profile=farm_layout")
            .Append("min_x=" + minX)
            .Append("min_y=" + minY)
            .Append("max_x=" + maxX)
            .Append("max_y=" + maxY)
            .Append("fresh=true");
        return new UriBuilder(uri)
        {
            Query = string.Join("&", query)
        }.Uri.AbsoluteUri;
    }

    private static void ValidateSnapshotBoundary(
        JsonObject snapshot,
        string sourceSaveId,
        string sourcePlayerId,
        int sourceTotalDay,
        int sourceTime)
    {
        if (!string.Equals(
                ReadSnapshotString(snapshot, "identity", "save_id"),
                sourceSaveId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadSnapshotString(snapshot, "identity", "player_id"),
                sourcePlayerId,
                StringComparison.Ordinal) ||
            ReadSnapshotInt(snapshot, "time", "total_days") != sourceTotalDay ||
            ReadSnapshotInt(snapshot, "time", "time") != sourceTime)
        {
            throw new InvalidDataException(
                "planning_handoff_farm_layout_boundary_drifted");
        }
    }

    private static void ValidateLayoutContext(
        JsonObject context,
        int mapWidth,
        int mapHeight,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        var bounds = context["bounds"] as JsonObject;
        var expectedTileCount = checked(
            (maxX - minX + 1) * (maxY - minY + 1));
        if (ReadString(context, "schema_version") !=
                "farm_site_preparation_context.v1" ||
            ReadString(context, "projection_status") !=
                "complete_bounded_current_farm_region" ||
            ReadString(context, "location_id") != "Farm" ||
            !ReadBool(context, "is_farm") ||
            ReadInt(context, "map_width", -1) != mapWidth ||
            ReadInt(context, "map_height", -1) != mapHeight ||
            ReadInt(bounds, "min_x", -1) != minX ||
            ReadInt(bounds, "min_y", -1) != minY ||
            ReadInt(bounds, "max_x", -1) != maxX ||
            ReadInt(bounds, "max_y", -1) != maxY ||
            ReadInt(context, "tile_count", -1) != expectedTileCount)
        {
            throw new InvalidDataException(
                "planning_handoff_farm_layout_context_mismatch");
        }
    }

    private static JsonObject ReadSnapshotObject(
        JsonObject snapshot,
        string section,
        string field) =>
        snapshot["state"]?[section]?[field]?["value"] as JsonObject ??
        throw new InvalidDataException(
            $"planning_handoff_snapshot_field_unavailable:{section}.{field}");

    private static string ReadSnapshotString(
        JsonObject snapshot,
        string section,
        string field) =>
        snapshot["state"]?[section]?[field]?["value"]?
            .GetValue<string>() ?? string.Empty;

    private static int ReadSnapshotInt(
        JsonObject snapshot,
        string section,
        string field) =>
        snapshot["state"]?[section]?[field]?["value"] is JsonValue value &&
        value.TryGetValue<int>(out var result)
            ? result
            : -1;

    private static string ReadString(JsonObject? value, string property) =>
        value?[property]?.GetValue<string>() ?? string.Empty;

    private static int ReadInt(
        JsonObject? value,
        string property,
        int fallback) =>
        value?[property] is JsonValue json &&
        json.TryGetValue<int>(out var result)
            ? result
            : fallback;

    private static bool ReadBool(JsonObject? value, string property) =>
        value?[property] is JsonValue json &&
        json.TryGetValue<bool>(out var result) &&
        result;

    private static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static async Task WriteTextAtomicallyAsync(
        string path,
        string value,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            throw new IOException(
                "planning_handoff_artifact_already_exists: " + fullPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var pending = fullPath + ".pending-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(
            pending,
            value,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        File.Move(pending, fullPath, overwrite: false);
    }
}
