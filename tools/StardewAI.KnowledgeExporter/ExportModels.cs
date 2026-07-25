namespace StardewAI.KnowledgeExporter;

public sealed record ContentFileRecord(
    string AssetName,
    string RelativePath,
    long Bytes,
    string Sha256);

public sealed record ContentExportRecord(
    string AssetName,
    string SourceKind,
    string Loader,
    string DeclaredType,
    string Status,
    int? EntryCount,
    string? OutputFile,
    long? OutputBytes,
    string? OutputSha256,
    string? PayloadSha256,
    string? Error);

public sealed class KnowledgeExportManifest
{
    public string SchemaVersion { get; init; } = "stardewai.knowledge_manifest.v1";

    public string Status { get; set; } = "running";

    public string GameVersion { get; init; } = string.Empty;

    public string SmapiVersion { get; init; } = string.Empty;

    public string Locale { get; init; } = string.Empty;

    public string StartedAtUtc { get; init; } = string.Empty;

    public string? CompletedAtUtc { get; set; }

    public string RuntimeContentRoot { get; init; } = string.Empty;

    public string ProvenancePolicy { get; init; } = "runtime-loaded content is authoritative; decompile and wiki are verification sources";

    public List<ContentFileRecord> ContentFiles { get; init; } = new();

    public List<ContentExportRecord> Exports { get; init; } = new();

    public int ExpectedExports { get; set; }

    public int SuccessfulExports => Exports.Count(record => record.Status == "available");

    public int FailedExports => Exports.Count(record => record.Status == "error");

    public string? RuntimeSemanticsFile { get; set; }

    public string? RuntimeSemanticsSha256 { get; set; }
}

public sealed class KnowledgeExportProgress
{
    public string SchemaVersion { get; init; } = "stardewai.knowledge_progress.v1";

    public string Status { get; init; } = "running";

    public int Processed { get; init; }

    public int Total { get; init; }

    public int Failures { get; init; }

    public string? LastAsset { get; init; }

    public string UpdatedAtUtc { get; init; } = string.Empty;
}
