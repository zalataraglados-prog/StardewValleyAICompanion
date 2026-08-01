using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class SnapshotSchemaJoiner
{
    private static readonly HashSet<string> ReadableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "available",
        "derived"
    };

    public SnapshotCoverageResult Join(string snapshotPath, IEnumerable<string> requiredFactors)
    {
        var payload = File.ReadAllBytes(snapshotPath);
        var json = payload.Length >= 3 &&
            payload[0] == 0xEF &&
            payload[1] == 0xBB &&
            payload[2] == 0xBF
                ? payload.AsMemory(3)
                : payload.AsMemory();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"Snapshot has no object-valued state: {snapshotPath}");

        var fields = requiredFactors
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Inspect(state, path))
            .ToArray();

        return new SnapshotCoverageResult(
            snapshotPath,
            ReadString(root, "schema_version"),
            ReadString(root, "bridge_version"),
            ReadString(root, "game_version"),
            ReadString(root, "state_hash"),
            ReadString(root, "completeness"),
            fields);
    }

    private static SnapshotFieldCoverage Inspect(JsonElement state, string path)
    {
        var current = state;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return new SnapshotFieldCoverage(
                    path,
                    "missing",
                    "missing_from_snapshot_schema",
                    null,
                    null,
                    null,
                    null,
                    "required factor has no exact field envelope in the full live snapshot");
            }
        }

        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty("status", out var statusElement))
        {
            return new SnapshotFieldCoverage(
                path,
                "invalid",
                "not_a_field_envelope",
                null,
                null,
                null,
                null,
                "required factor exists but does not expose FieldEnvelope provenance");
        }

        var status = statusElement.GetString() ?? "unknown";
        var sourceKind = ReadNestedString(current, "source", "kind");
        var sourcePath = ReadNestedString(current, "source", "path");
        var adapter = ReadString(current, "adapter");
        var reason = ReadOptionalString(current, "reason");
        var provenanceComplete = !string.IsNullOrWhiteSpace(sourceKind) &&
                                 !string.Equals(sourceKind, "unavailable", StringComparison.OrdinalIgnoreCase) &&
                                 !string.IsNullOrWhiteSpace(sourcePath) &&
                                 !string.Equals(sourcePath, "unknown", StringComparison.OrdinalIgnoreCase) &&
                                 !string.IsNullOrWhiteSpace(adapter) &&
                                 !string.Equals(adapter, "unknown", StringComparison.OrdinalIgnoreCase);

        var coverage = status.ToLowerInvariant() switch
        {
            "available" or "derived" when provenanceComplete => "readable_with_provenance",
            "available" or "derived" => "readable_missing_provenance",
            "unavailable" => "contextually_unavailable",
            "stale" => "stale",
            "error" => "adapter_error",
            _ => "invalid_status"
        };

        return new SnapshotFieldCoverage(
            path,
            status,
            coverage,
            sourceKind,
            sourcePath,
            adapter,
            reason,
            ReadableStatuses.Contains(status) ? "observed in full live snapshot" : "schema exists; current runtime context is not readable");
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? ReadOptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string ReadNestedString(JsonElement element, string objectProperty, string valueProperty) =>
        element.TryGetProperty(objectProperty, out var nested) && nested.ValueKind == JsonValueKind.Object
            ? ReadString(nested, valueProperty)
            : string.Empty;
}
