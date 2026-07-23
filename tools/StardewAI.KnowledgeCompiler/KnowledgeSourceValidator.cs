using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StardewAI.KnowledgeCompiler;

internal sealed class KnowledgeSourceValidator
{
    private static readonly Regex LocaleSuffix = new(
        @"\.[a-z]{2}-[A-Z]{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string exportRoot;
    private readonly string? contentRoot;

    public KnowledgeSourceValidator(string exportRoot, string? contentRoot)
    {
        this.exportRoot = exportRoot;
        this.contentRoot = contentRoot;
    }

    public ExportManifest LoadManifest()
    {
        var path = System.IO.Path.Combine(exportRoot, "manifest.json");
        return JsonSerializer.Deserialize<ExportManifest>(File.ReadAllBytes(path), JsonOptions.Read)
               ?? throw new InvalidDataException($"Manifest is empty: {path}");
    }

    public IReadOnlyList<ValidationIssue> Validate(ExportManifest manifest)
    {
        var issues = new List<ValidationIssue>();
        if (!string.Equals(manifest.Status, "complete", StringComparison.Ordinal))
            issues.Add(new("blocking", "manifest_not_complete", "manifest", manifest.Status));
        if (manifest.ExpectedExports != manifest.Exports.Count ||
            manifest.SuccessfulExports != manifest.ExpectedExports ||
            manifest.FailedExports != 0)
        {
            issues.Add(new(
                "blocking",
                "export_count_mismatch",
                "manifest",
                $"expected={manifest.ExpectedExports};records={manifest.Exports.Count};successful={manifest.SuccessfulExports};failed={manifest.FailedExports}"));
        }

        foreach (var duplicate in ExportGroups(manifest).Where(group => group.Count() > 1))
        {
            var hashes = duplicate.Select(row => row.PayloadSha256).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            issues.Add(hashes.Length == 1
                ? new("warning", "duplicate_equivalent_export_asset", duplicate.Key,
                    $"count={duplicate.Count()};payload_sha256={hashes[0]};last_record_is_canonical")
                : new("blocking", "duplicate_conflicting_export_asset", duplicate.Key,
                    $"count={duplicate.Count()};payload_sha256={string.Join(',', hashes)}"));
        }

        foreach (var row in ExportGroups(manifest).Select(group => group.Last()))
        {
            ValidateExport(row, issues);
        }

        ValidateRuntimeSemantics(manifest, issues);

        if (!string.IsNullOrWhiteSpace(contentRoot))
        {
            ValidateContentFiles(manifest.ContentFiles, issues);
        }
        else
        {
            issues.Add(new("warning", "content_root_not_supplied", "content_inventory", "XNB hashes were recorded but not independently rehashed."));
        }

        return issues;
    }

    public RuntimeSemanticSummary LoadRuntimeSemantics(ExportManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.RuntimeSemanticsFile))
            throw new InvalidDataException("Manifest has no runtime semantics file.");
        var path = System.IO.Path.Combine(exportRoot, manifest.RuntimeSemanticsFile);
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        var families = root.GetProperty("handlers").EnumerateArray()
            .Select(row => row.GetProperty("family").GetString() ?? string.Empty)
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new RuntimeSemanticSummary(
            manifest.RuntimeSemanticsFile,
            manifest.RuntimeSemanticsSha256 ?? string.Empty,
            root.GetProperty("handler_count").GetInt32(),
            root.GetProperty("parsed_condition_count").GetInt32(),
            root.GetProperty("condition_parse_error_count").GetInt32(),
            root.GetProperty("parsed_event_count").GetInt32(),
            root.GetProperty("unresolved_event_precondition_count").GetInt32(),
            root.GetProperty("unresolved_event_command_count").GetInt32(),
            root.TryGetProperty("parsed_trigger_action_count", out var triggerCount)
                ? triggerCount.GetInt32()
                : 0,
            root.TryGetProperty("unresolved_trigger_action_count", out var unresolvedTriggerCount)
                ? unresolvedTriggerCount.GetInt32()
                : 0,
            families);
    }

    public IReadOnlyList<AssetCoverageRow> BuildCoverage(ExportManifest manifest)
    {
        var decoded = manifest.Exports
            .Where(row => string.Equals(row.Status, "available", StringComparison.Ordinal))
            .Select(row => row.AssetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return manifest.ContentFiles
            .OrderBy(row => row.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(row => Classify(row, decoded))
            .ToArray();
    }

    public Dictionary<string, PayloadAsset> LoadPayloads(ExportManifest manifest)
    {
        var result = new Dictionary<string, PayloadAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ExportGroups(manifest)
                     .Select(group => group.Last())
                     .Where(row => row.Status == "available" && row.OutputFile is not null))
        {
            var path = System.IO.Path.Combine(exportRoot, row.OutputFile!);
            result.Add(row.AssetName, new PayloadAsset(row.AssetName, path, JsonDocument.Parse(File.ReadAllBytes(path))));
        }

        return result;
    }

    private static IEnumerable<IGrouping<string, ExportRow>> ExportGroups(ExportManifest manifest)
    {
        return manifest.Exports.GroupBy(row => row.AssetName, StringComparer.OrdinalIgnoreCase);
    }

    private void ValidateExport(ExportRow row, ICollection<ValidationIssue> issues)
    {
        if (!string.Equals(row.Status, "available", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(row.OutputFile))
        {
            issues.Add(new("blocking", "export_unavailable", row.AssetName, row.Error ?? row.Status));
            return;
        }

        var path = System.IO.Path.Combine(exportRoot, row.OutputFile);
        if (!File.Exists(path))
        {
            issues.Add(new("blocking", "payload_file_missing", row.AssetName, row.OutputFile));
            return;
        }

        var info = new FileInfo(path);
        if (row.OutputBytes != info.Length)
            issues.Add(new("blocking", "payload_size_mismatch", row.AssetName, $"manifest={row.OutputBytes};actual={info.Length}"));
        if (!string.IsNullOrWhiteSpace(row.OutputSha256))
        {
            using var stream = File.OpenRead(path);
            var outputHash = Hash(stream);
            if (!string.Equals(outputHash, row.OutputSha256, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("blocking", "payload_file_hash_mismatch", row.AssetName, $"manifest={row.OutputSha256};actual={outputHash}"));
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (!document.RootElement.TryGetProperty("payload", out var payload))
        {
            issues.Add(new("blocking", "payload_property_missing", row.AssetName, row.OutputFile));
            return;
        }

        var root = document.RootElement;
        var embeddedAsset = root.TryGetProperty("asset_name", out var assetProperty)
            ? assetProperty.GetString()
            : null;
        var embeddedHash = root.TryGetProperty("payload_sha256", out var hashProperty)
            ? hashProperty.GetString()
            : null;
        if (!string.Equals(embeddedAsset, row.AssetName, StringComparison.OrdinalIgnoreCase))
            issues.Add(new("blocking", "payload_asset_name_mismatch", row.AssetName, $"embedded={embeddedAsset}"));
        if (!string.Equals(embeddedHash, row.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            issues.Add(new("blocking", "payload_embedded_hash_mismatch", row.AssetName, $"manifest={row.PayloadSha256};embedded={embeddedHash}"));
    }

    private void ValidateRuntimeSemantics(ExportManifest manifest, ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.RuntimeSemanticsFile) ||
            string.IsNullOrWhiteSpace(manifest.RuntimeSemanticsSha256))
        {
            issues.Add(new("blocking", "runtime_semantics_missing", "runtime-semantics", "manifest has no runtime parser evidence"));
            return;
        }

        var path = System.IO.Path.Combine(exportRoot, manifest.RuntimeSemanticsFile);
        if (!File.Exists(path))
        {
            issues.Add(new("blocking", "runtime_semantics_file_missing", "runtime-semantics", manifest.RuntimeSemanticsFile));
            return;
        }

        using (var stream = File.OpenRead(path))
        {
            var actualHash = Hash(stream);
            if (!string.Equals(actualHash, manifest.RuntimeSemanticsSha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new("blocking", "runtime_semantics_hash_mismatch", "runtime-semantics",
                    $"manifest={manifest.RuntimeSemanticsSha256};actual={actualHash}"));
                return;
            }
        }

        try
        {
            var summary = LoadRuntimeSemantics(manifest);
            if (summary.ConditionParseErrorCount != 0 ||
                summary.UnresolvedEventPreconditionCount != 0 ||
                summary.UnresolvedEventCommandCount != 0 ||
                summary.UnresolvedTriggerActionCount != 0)
            {
                issues.Add(new("blocking", "runtime_semantics_unresolved", "runtime-semantics",
                    $"condition_errors={summary.ConditionParseErrorCount};event_preconditions={summary.UnresolvedEventPreconditionCount};event_commands={summary.UnresolvedEventCommandCount};trigger_actions={summary.UnresolvedTriggerActionCount}"));
            }
        }
        catch (Exception ex)
        {
            issues.Add(new("blocking", "runtime_semantics_invalid", "runtime-semantics", ex.Message));
        }
    }

    private void ValidateContentFiles(IEnumerable<ContentFileRow> rows, ICollection<ValidationIssue> issues)
    {
        foreach (var row in rows)
        {
            var path = System.IO.Path.Combine(contentRoot!, row.RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                issues.Add(new("blocking", "runtime_content_missing", row.AssetName, path));
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length != row.Bytes)
            {
                issues.Add(new("blocking", "runtime_content_size_mismatch", row.AssetName, $"manifest={row.Bytes};actual={info.Length}"));
                continue;
            }

            using var stream = File.OpenRead(path);
            var actualHash = Hash(stream);
            if (!string.Equals(actualHash, row.Sha256, StringComparison.OrdinalIgnoreCase))
                issues.Add(new("blocking", "runtime_content_hash_mismatch", row.AssetName, $"manifest={row.Sha256};actual={actualHash}"));
        }
    }

    private static AssetCoverageRow Classify(ContentFileRow row, IReadOnlySet<string> decoded)
    {
        if (decoded.Contains(row.AssetName))
            return new(row.AssetName, row.RelativePath, "runtime_semantic_payload", true, false, false, "matching manifest export");
        if (LocaleSuffix.IsMatch(row.AssetName))
            return new(row.AssetName, row.RelativePath, "localized_variant_inventory_only", false, false, false, "logic uses locale-neutral base asset; translation remains hash-inventoried");
        if (row.AssetName.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
            return new(row.AssetName, row.RelativePath, "runtime_map_projection_required", false, true, false, "map collision, warps, tile actions, and properties require the live map adapter");
        if (row.AssetName.StartsWith("Data/", StringComparison.OrdinalIgnoreCase) ||
            row.AssetName.StartsWith("Characters/schedules/", StringComparison.OrdinalIgnoreCase) ||
            row.AssetName.StartsWith("Characters/Dialogue/", StringComparison.OrdinalIgnoreCase))
        {
            return new(row.AssetName, row.RelativePath, "undecoded_semantic_asset", false, false, true, "semantic XNB has no matching decoded export");
        }

        return new(row.AssetName, row.RelativePath, "binary_media_inventory", false, false, false, "hash-inventoried visual, audio, font, or animation asset");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Hash(Stream stream)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Read = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static readonly JsonSerializerOptions Write = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
