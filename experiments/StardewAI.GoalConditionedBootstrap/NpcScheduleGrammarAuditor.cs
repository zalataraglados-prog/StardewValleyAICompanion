using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class NpcScheduleGrammarAuditor
{
    public static NpcScheduleGrammarAuditReport Audit(
        string contractPath,
        string manifestPath,
        string decompileRoot)
    {
        var issues = new List<string>();
        var contractFullPath = Path.GetFullPath(contractPath);
        var manifestFullPath = Path.GetFullPath(manifestPath);
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(contractFullPath));
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestFullPath));
        var contract = contractDocument.RootElement;
        var manifest = manifestDocument.RootElement;
        RequireString(contract, "schema_version", "stardewai.npc_schedule_grammar_contract.v1");
        RequireString(manifest, "schemaVersion", "stardewai.knowledge_manifest.v1");
        RequireString(manifest, "status", "complete");

        var manifestSha = ContentInventoryVerifier.HashFile(manifestFullPath);
        if (!EqualsOrdinalIgnoreCase(manifestSha, RequiredString(contract, "source_manifest_sha256")))
            issues.Add("source_manifest_sha256_mismatch");
        if (!string.Equals(RequiredString(manifest, "gameVersion"), RequiredString(contract, "game_version"), StringComparison.Ordinal))
            issues.Add("game_version_mismatch");

        var decompiledRelativePath = RequiredString(contract, "decompiled_npc_relative_path");
        var decompiledNpcPath = ResolveDescendant(decompileRoot, decompiledRelativePath);
        if (!File.Exists(decompiledNpcPath) ||
            !EqualsOrdinalIgnoreCase(
                ContentInventoryVerifier.HashFile(decompiledNpcPath),
                RequiredString(contract, "decompiled_npc_sha256")))
        {
            issues.Add("decompiled_npc_sha256_mismatch");
        }

        var rawRoot = Path.GetDirectoryName(manifestFullPath)
            ?? throw new InvalidDataException("Schedule manifest has no parent directory.");
        var stats = new ScheduleGrammarStats();
        foreach (var export in manifest.GetProperty("exports").EnumerateArray()
                     .Where(row => RequiredString(row, "assetName").StartsWith("Characters/schedules/", StringComparison.Ordinal)))
        {
            stats.AssetCount++;
            AuditExport(export, rawRoot, stats, issues);
        }

        CompareExpected(contract.GetProperty("expected"), stats, issues);
        return new NpcScheduleGrammarAuditReport
        {
            Status = issues.Count == 0 ? "pass" : "blocked",
            ContractPath = contractFullPath,
            ContractSha256 = ContentInventoryVerifier.HashFile(contractFullPath),
            ManifestPath = manifestFullPath,
            ManifestSha256 = manifestSha,
            DecompiledNpcPath = decompiledNpcPath,
            DecompiledNpcSha256 = File.Exists(decompiledNpcPath)
                ? ContentInventoryVerifier.HashFile(decompiledNpcPath)
                : string.Empty,
            Stats = stats,
            Issues = issues.ToArray(),
            ExactProjectionPolicy =
                "GOTO, NOT friendship, MAIL, bed, same-map shorthand and static endpoints are grammar-covered. aHHMM remains fail-closed until native path length is supplied."
        };
    }

    private static void AuditExport(
        JsonElement descriptor,
        string rawRoot,
        ScheduleGrammarStats stats,
        ICollection<string> issues)
    {
        var assetName = RequiredString(descriptor, "assetName");
        if (RequiredString(descriptor, "status") != "available")
        {
            issues.Add("schedule_export_unavailable:" + assetName);
            return;
        }
        var outputFile = RequiredString(descriptor, "outputFile");
        if (Path.IsPathRooted(outputFile) || Path.GetFileName(outputFile) != outputFile)
        {
            issues.Add("schedule_output_file_invalid:" + assetName);
            return;
        }
        var outputPath = Path.Combine(rawRoot, outputFile);
        if (!File.Exists(outputPath))
        {
            issues.Add("schedule_output_file_missing:" + assetName);
            return;
        }
        if (new FileInfo(outputPath).Length != descriptor.GetProperty("outputBytes").GetInt64() ||
            !EqualsOrdinalIgnoreCase(ContentInventoryVerifier.HashFile(outputPath), RequiredString(descriptor, "outputSha256")))
        {
            issues.Add("schedule_output_file_identity_mismatch:" + assetName);
            return;
        }

        using var outputDocument = JsonDocument.Parse(File.ReadAllText(outputPath));
        var root = outputDocument.RootElement;
        if (RequiredString(root, "schema_version") != "stardewai.knowledge_asset.v1" ||
            RequiredString(root, "asset_name") != assetName ||
            !EqualsOrdinalIgnoreCase(RequiredString(root, "payload_sha256"), RequiredString(descriptor, "payloadSha256")) ||
            !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            issues.Add("schedule_output_contract_mismatch:" + assetName);
            return;
        }

        var entries = payload.EnumerateObject().ToArray();
        if (!descriptor.TryGetProperty("entryCount", out var entryCount) ||
            entryCount.ValueKind != JsonValueKind.Number ||
            entryCount.GetInt32() != entries.Length)
        {
            issues.Add("schedule_entry_count_mismatch:" + assetName);
            return;
        }
        foreach (var entry in entries)
            AuditEntry(assetName, entry, stats, issues);
    }

    private static void AuditEntry(
        string assetName,
        JsonProperty entry,
        ScheduleGrammarStats stats,
        ICollection<string> issues)
    {
        stats.EntryCount++;
        if (entry.Value.ValueKind != JsonValueKind.String)
        {
            issues.Add("schedule_entry_not_string:" + assetName + "#" + entry.Name);
            return;
        }
        var raw = entry.Value.GetString() ?? string.Empty;
        var commands = raw.Split(new[] { '/' }, StringSplitOptions.None)
            .Select(command => command.Trim())
            .ToArray();
        if (commands.Length == 0 || commands.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add("schedule_entry_empty_segment:" + assetName + "#" + entry.Name);
            return;
        }
        stats.SegmentCount += commands.Length;

        if (entry.Name.EndsWith("_Replacement", StringComparison.Ordinal))
        {
            stats.ReplacementEntryCount++;
            if (commands.Length != 1 || !ValidReplacement(commands[0]))
                issues.Add("schedule_replacement_invalid:" + assetName + "#" + entry.Name);
            return;
        }

        var firstTokens = Tokens(commands[0]);
        if (firstTokens[0] == "GOTO") stats.FirstGotoCount++;
        else if (firstTokens[0] == "NOT") stats.FirstNotCount++;
        else if (firstTokens[0] == "MAIL") stats.FirstMailCount++;
        else stats.FirstEndpointCount++;

        for (var index = 0; index < commands.Length; index++)
        {
            var tokens = Tokens(commands[index]);
            var control = tokens[0];
            if (control == "GOTO")
            {
                stats.AllGotoSegmentCount++;
                if (tokens.Length != 2)
                    issues.Add("schedule_goto_invalid:" + assetName + "#" + entry.Name + ":" + index);
                continue;
            }
            if (control == "NOT")
            {
                if (index != 0 || tokens.Length < 4 || tokens.Length % 2 != 0 ||
                    !string.Equals(tokens[1], "friendship", StringComparison.OrdinalIgnoreCase) ||
                    Enumerable.Range(3, tokens.Length - 3).Where(value => value % 2 == 1)
                        .Any(value => !int.TryParse(tokens[value], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    issues.Add("schedule_not_friendship_invalid:" + assetName + "#" + entry.Name);
                }
                continue;
            }
            if (control == "MAIL")
            {
                if (index != 0 || tokens.Length != 2)
                    issues.Add("schedule_mail_invalid:" + assetName + "#" + entry.Name);
                continue;
            }
            if (!ValidEndpoint(tokens, stats))
                issues.Add("schedule_endpoint_invalid:" + assetName + "#" + entry.Name + ":" + index);
        }
    }

    private static bool ValidEndpoint(string[] tokens, ScheduleGrammarStats stats)
    {
        if (tokens.Length < 2 || !TryParseScheduleTime(tokens[0], out var arrivalTime))
            return false;
        if (arrivalTime) stats.ArrivalTimeSegmentCount++;
        if (tokens[1] == "bed")
        {
            stats.BedSegmentCount++;
            return true;
        }
        if (int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            stats.SameMapSegmentCount++;
            return tokens.Length >= 3 && int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }
        return tokens.Length >= 4 &&
            int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool ValidReplacement(string command)
    {
        var tokens = Tokens(command);
        return tokens.Length == 4 &&
            int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool TryParseScheduleTime(string value, out bool arrivalTime)
    {
        arrivalTime = value.Length > 1 && value[0] == 'a';
        var numeric = arrivalTime ? value.Substring(1) : value;
        return int.TryParse(numeric, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static string[] Tokens(string command) => command
        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    private static void CompareExpected(
        JsonElement expected,
        ScheduleGrammarStats stats,
        ICollection<string> issues)
    {
        Compare("asset_count", stats.AssetCount);
        Compare("entry_count", stats.EntryCount);
        Compare("segment_count", stats.SegmentCount);
        Compare("first_goto_count", stats.FirstGotoCount);
        Compare("first_not_count", stats.FirstNotCount);
        Compare("first_mail_count", stats.FirstMailCount);
        Compare("first_endpoint_count", stats.FirstEndpointCount);
        Compare("replacement_entry_count", stats.ReplacementEntryCount);
        Compare("all_goto_segment_count", stats.AllGotoSegmentCount);
        Compare("arrival_time_segment_count", stats.ArrivalTimeSegmentCount);
        Compare("bed_segment_count", stats.BedSegmentCount);
        Compare("same_map_segment_count", stats.SameMapSegmentCount);
        return;

        void Compare(string field, int actual)
        {
            if (expected.GetProperty(field).GetInt32() != actual)
                issues.Add("schedule_grammar_count_mismatch:" + field + ":" + actual);
        }
    }

    private static string ResolveDescendant(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Path escapes decompile root: " + relativePath);
        return path;
    }

    private static string RequiredString(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidDataException("Missing string property " + name + ".");
        }
        return property.GetString()!;
    }

    private static void RequireString(JsonElement source, string name, string expected)
    {
        if (RequiredString(source, name) != expected)
            throw new InvalidDataException("Unexpected " + name + ".");
    }

    private static bool EqualsOrdinalIgnoreCase(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class ScheduleGrammarStats
{
    [JsonPropertyName("asset_count")] public int AssetCount { get; set; }
    [JsonPropertyName("entry_count")] public int EntryCount { get; set; }
    [JsonPropertyName("segment_count")] public int SegmentCount { get; set; }
    [JsonPropertyName("first_goto_count")] public int FirstGotoCount { get; set; }
    [JsonPropertyName("first_not_count")] public int FirstNotCount { get; set; }
    [JsonPropertyName("first_mail_count")] public int FirstMailCount { get; set; }
    [JsonPropertyName("first_endpoint_count")] public int FirstEndpointCount { get; set; }
    [JsonPropertyName("replacement_entry_count")] public int ReplacementEntryCount { get; set; }
    [JsonPropertyName("all_goto_segment_count")] public int AllGotoSegmentCount { get; set; }
    [JsonPropertyName("arrival_time_segment_count")] public int ArrivalTimeSegmentCount { get; set; }
    [JsonPropertyName("bed_segment_count")] public int BedSegmentCount { get; set; }
    [JsonPropertyName("same_map_segment_count")] public int SameMapSegmentCount { get; set; }
}

public sealed class NpcScheduleGrammarAuditReport
{
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("contract_path")] public string ContractPath { get; set; } = string.Empty;
    [JsonPropertyName("contract_sha256")] public string ContractSha256 { get; set; } = string.Empty;
    [JsonPropertyName("manifest_path")] public string ManifestPath { get; set; } = string.Empty;
    [JsonPropertyName("manifest_sha256")] public string ManifestSha256 { get; set; } = string.Empty;
    [JsonPropertyName("decompiled_npc_path")] public string DecompiledNpcPath { get; set; } = string.Empty;
    [JsonPropertyName("decompiled_npc_sha256")] public string DecompiledNpcSha256 { get; set; } = string.Empty;
    [JsonPropertyName("stats")] public ScheduleGrammarStats Stats { get; set; } = new();
    [JsonPropertyName("issues")] public string[] Issues { get; set; } = Array.Empty<string>();
    [JsonPropertyName("exact_projection_policy")] public string ExactProjectionPolicy { get; set; } = string.Empty;
}
