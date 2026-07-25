using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class ExportManifest
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string SmapiVersion { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public int ExpectedExports { get; set; }
    public int SuccessfulExports { get; set; }
    public int FailedExports { get; set; }
    public string? RuntimeSemanticsFile { get; set; }
    public string? RuntimeSemanticsSha256 { get; set; }
    public List<ContentFileRow> ContentFiles { get; set; } = new();
    public List<ExportRow> Exports { get; set; } = new();
}

internal sealed class ContentFileRow
{
    public string AssetName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

internal sealed class ExportRow
{
    public string AssetName { get; set; } = string.Empty;
    public string SourceKind { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string DeclaredType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? EntryCount { get; set; }
    public string? OutputFile { get; set; }
    public long? OutputBytes { get; set; }
    public string? OutputSha256 { get; set; }
    public string? PayloadSha256 { get; set; }
    public string? Error { get; set; }
}

internal sealed record ValidationIssue(string Severity, string Code, string Subject, string Detail);

internal sealed record AssetCoverageRow(
    string AssetName,
    string RelativePath,
    string Classification,
    bool SemanticallyDecoded,
    bool RequiresRuntimeProjection,
    bool BlocksDependencyCompleteness,
    string Evidence);

internal sealed record SnapshotFieldCoverage(
    string Path,
    string Status,
    string Coverage,
    string? SourceKind,
    string? SourcePath,
    string? Adapter,
    string? Reason,
    string Evidence);

internal sealed record SnapshotCoverageResult(
    string SnapshotPath,
    string SchemaVersion,
    string BridgeVersion,
    string GameVersion,
    string StateHash,
    string Completeness,
    IReadOnlyList<SnapshotFieldCoverage> Fields)
{
    public SnapshotFieldCoverage GetRequired(string path) =>
        Fields.FirstOrDefault(row => string.Equals(row.Path, path, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Snapshot coverage was not computed for '{path}'.");
}

internal sealed record RuntimeSemanticSummary(
    string SourceFile,
    string Sha256,
    int HandlerCount,
    int ParsedConditionCount,
    int ConditionParseErrorCount,
    int ParsedEventCount,
    int UnresolvedEventPreconditionCount,
    int UnresolvedEventCommandCount,
    int ParsedTriggerActionCount,
    int UnresolvedTriggerActionCount,
    IReadOnlyDictionary<string, int> HandlerFamilies);

internal sealed record GraphNode(
    string Id,
    string Kind,
    string SourceAsset,
    string SourceKey,
    IReadOnlyDictionary<string, object?> Attributes);

internal sealed record GraphEdge(
    string From,
    string To,
    string Kind,
    string SourceAsset,
    string SourcePath,
    IReadOnlyDictionary<string, object?> Attributes);

internal sealed class PayloadAsset : IDisposable
{
    private readonly JsonDocument document;

    public PayloadAsset(string assetName, string path, JsonDocument document)
    {
        AssetName = assetName;
        Path = path;
        this.document = document;
        Payload = document.RootElement.GetProperty("payload");
    }

    public string AssetName { get; }
    public string Path { get; }
    public JsonElement Payload { get; }

    public void Dispose() => document.Dispose();
}
